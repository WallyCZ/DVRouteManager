using CommandTerminal;
using DV.HUD;
using DV.Simulation.Cars;
using DV.Simulation.Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRouteManager
{
    public class LocoCruiseControl : IDisposable
    {
        protected ILocomotiveRemoteControl remoteControl;
        protected TrainCar trainCar;

        public float TargetSpeed { get; protected set; } = 20.0f;
        protected bool running;

        private static LocoCruiseControl CruiseControl;

        public static bool IsSet { get => CruiseControl != null && CruiseControl.Running; }
        protected bool Running { get => running; }

        // ── DM3 gear state ──────────────────────────────────────────────────
        private const string LOCO_DM3 = "LocoDM3";
        private const float RPM_SHIFT_UP   = 800f;
        private const float RPM_SHIFT_DOWN = 600f;
        private const float SHIFT_COOLDOWN = 3.0f; // seconds between shifts
        private const float DM3_MAX_SPEED  = 70f;  // km/h

        private float _lastGearRpm    = 0f;
        private float _lastShiftTime  = -999f;
        private bool  _awaitingShift  = false; // throttle zeroed, waiting for RPM to drop before moving lever
        private int   _pendingShiftDir = 0;    // +1 up, -1 down

        // Cached components (lazy-initialised per loco session)
        private LocoIndicatorReader        _indicators;
        private InteriorControlsManager    _interiorControls;
        private bool                       _isDM3;

        // ── Steam (S060 / S282) state ────────────────────────────────────────
        private bool                       _isSteam;
        private BaseControlsOverrider      _steamOverrider;

        // Smoothed control positions
        private float _steamRegulator      = 0f;
        private float _steamCutoff         = 0.5f;
        private float _steamBrakeTarget    = 0f;

        // Pulse-braking state
        private bool  _steamPulseBraking   = false;
        private float _steamPulseTimer     = 0f;
        private bool  _steamPulseHigh      = false;

        // Averaged steam-chest pressure (sampled every 0.1 s, window = 10)
        private readonly Queue<float> _steamPressureSamples = new Queue<float>();
        private float _steamPressureTimer = 0f;

        // Reflection cache for simFlow.TryGetPort
        private SimController _steamSimCtrl;
        private MethodInfo    _tryGetPortMI;
        private PropertyInfo  _portValueProp;

        // ────────────────────────────────────────────────────────────────────

        public LocoCruiseControl(ILocomotiveRemoteControl remoteControl, TrainCar car = null)
        {
            this.remoteControl = remoteControl;
            this.trainCar      = car;

            if (car != null)
            {
                _isDM3        = car.carLivery?.parentType?.id == LOCO_DM3;
                _indicators   = car.GetComponentInChildren<LocoIndicatorReader>();
                // Interior controls loaded lazily (may be null until player enters cab)

                string locoId = car.carLivery?.parentType?.id ?? "";
                _isSteam = locoId.Contains("S060") || locoId.Contains("S282");
                if (_isSteam)
                    _steamOverrider = car.SimController?.controlsOverrider;
            }
        }

        public bool StartCruiseControl(float targetSpeed)
        {
            this.TargetSpeed = targetSpeed;
            running = true;
            Module.StartCoroutine(CruiseControlCoroutine());
            return true;
        }

        private const float Kp_SPEED             = 1.5f;
        private const float Ki_SPEED             = 0.0005f;
        private const float Kd_SPEED             = 7f;
        private const float Kp_SPEED_RETURN_FACTOR = 1.5f;

        float integral      = 0;
        float previousError = 0;

        // ── Main speed controller ────────────────────────────────────────────
        //https://en.wikipedia.org/wiki/PID_controller
        protected float MaintainSpeed(float targetAcceleration, float dt, float speed, float acceleration)
        {
            // Steam locos use direct regulator/cutoff/brake control, not the PID
            if (_isSteam)
                return MaintainSpeedSteam(dt, speed);

            if (remoteControl.GetReverserSymbol() == "N")
            {
                running = false;
                return 0.0f;
            }

            // ── DM3: cap speed and handle gear shift ─────────────────────────
            if (_isDM3)
            {
                if (TargetSpeed > DM3_MAX_SPEED)
                    TargetSpeed = DM3_MAX_SPEED;
                if (HandleDM3GearShift(dt))
                    return targetAcceleration; // PID skipped during shift
            }

            // ── Temperature: back off throttle when overheating ──────────────
            var tempState = remoteControl.GetEngineTemperatureState(false);
            bool tempCritical = tempState.HasFlag(MultipleUnitStateObserver.TemperatureState.Critical);
            bool tempWarning  = tempState.HasFlag(MultipleUnitStateObserver.TemperatureState.Warning);

            // ── PID ──────────────────────────────────────────────────────────
            float error = TargetSpeed - speed;
            integral += error * dt;

            if (error < 0)
                integral = 0;

            float derivative = (error - previousError) / dt;

            float Kp = error < 0 ? Kp_SPEED * Kp_SPEED_RETURN_FACTOR : Kp_SPEED;

            float controlValue = Kp * error + Ki_SPEED * integral + Kd_SPEED * derivative;

            previousError = error;

#if DEBUG2
            Terminal.Log($"targetSpeed {TargetSpeed} error {error} accel {acceleration} controlValue {controlValue} throttle {remoteControl.GetTargetThrottle()} temp {tempState}");
#endif

            if (controlValue > 20.0f)
                controlValue = 20.0f;

            if (acceleration > targetAcceleration)
                controlValue = 0f;

            if (TargetSpeed < Mathf.Epsilon)
                controlValue = -10f;

            // Temperature limiting: Critical → force reduce; Warning → no increase
            if (tempCritical)
            {
                controlValue = Mathf.Min(controlValue, -5f);
            }
            else if (tempWarning)
            {
                controlValue = Mathf.Min(controlValue, 0f);
            }

            if (remoteControl.IsWheelslipping())
            {
                targetAcceleration -= 0.5f * dt;
                controlValue = -30.0f;
            }

            if (error < -3.0f || TargetSpeed < Mathf.Epsilon)
                remoteControl.UpdateBrake(0.3f * error * -1.0f * dt);
            else if (remoteControl.GetTargetBrake() > Mathf.Epsilon)
                remoteControl.UpdateBrake(-30.0f * dt);

            remoteControl.UpdateThrottle(ThrottleCurveFactor(remoteControl.GetTargetThrottle(), controlValue > 0.0f) * controlValue);

            return targetAcceleration;
        }

        // ── DM3 gear management ──────────────────────────────────────────────
        // Returns true while a shift is in progress (PID caller should skip output).
        private bool HandleDM3GearShift(float dt)
        {
            float rpm = _indicators?.engineRpm?.Value ?? 0f;
            float now = Time.time;

            if (_awaitingShift)
            {
                // Zero throttle and wait for RPM to settle before moving the lever
                remoteControl.UpdateThrottle(-100f);

                if (rpm < 750f)
                {
                    // RPM low enough — move the gear lever
                    var controls = GetInteriorControls();
                    if (controls != null)
                    {
                        controls.MoveScrollable(InteriorControlsManager.ControlType.GearboxA, _pendingShiftDir);
                        controls.MoveScrollable(InteriorControlsManager.ControlType.GearboxB, _pendingShiftDir);
#if DEBUG
                        Terminal.Log($"DM3: gear lever moved {(_pendingShiftDir > 0 ? "up" : "down")} (RPM {rpm:0})");
#endif
                    }
                    _awaitingShift = false;
                    _lastShiftTime = now;
                }

                _lastGearRpm = rpm;
                return true; // PID suppressed
            }

            // Cooldown between shifts
            if (now - _lastShiftTime < SHIFT_COOLDOWN)
            {
                _lastGearRpm = rpm;
                return false;
            }

            // Decide whether a shift is needed
            if (rpm > RPM_SHIFT_UP)
            {
                _awaitingShift  = true;
                _pendingShiftDir = 1;
#if DEBUG
                Terminal.Log($"DM3: shift-up queued (RPM {rpm:0})");
#endif
            }
            else if (rpm < RPM_SHIFT_DOWN && rpm <= _lastGearRpm)
            {
                _awaitingShift  = true;
                _pendingShiftDir = -1;
#if DEBUG
                Terminal.Log($"DM3: shift-down queued (RPM {rpm:0})");
#endif
            }

            _lastGearRpm = rpm;
            return _awaitingShift;
        }

        private InteriorControlsManager GetInteriorControls()
        {
            if (_interiorControls != null) return _interiorControls;
            if (trainCar?.interior == null) return null;
            _interiorControls = trainCar.interior.GetComponentInChildren<InteriorControlsManager>(true);
            return _interiorControls;
        }

        // ── Steam loco controller ────────────────────────────────────────────
        // Mirrors the algorithm from SteamCruiseControl mod's CruiseControlAI:
        //   - Direct absolute Set() on regulator, cutoff, brake (no PID)
        //   - Pressure-aware cutoff: reduce steam admission as chest pressure rises
        //   - Pulse braking: on 2.5 s / off 1.5 s cycle when overspeeding
        private float MaintainSpeedSteam(float dt, float speed)
        {
            if (_steamOverrider == null) return 0f;

            bool forward  = TargetSpeed >= 0f;
            float absTarget = Mathf.Abs(TargetSpeed);
            float absSpeed  = Mathf.Abs(speed);

            float pressure    = GetSteamChestPressure();
            float avgPressure = UpdateSteamPressureAvg(pressure, dt);

            // ── Full stop ────────────────────────────────────────────────────
            if (absTarget < Mathf.Epsilon)
            {
                _steamPulseBraking = false;
                SteamSetBrake(1f);
                SteamSetRegulatorSmooth(0f);
                SteamSetCutoffSmooth(0.5f, dt);
                return 0f;
            }

            float speedDiff = absSpeed - absTarget; // positive → too fast

            if (speedDiff > 0.1f)
            {
                // ── Pulse braking ─────────────────────────────────────────────
                if (!_steamPulseBraking)
                {
                    _steamPulseBraking = true;
                    _steamPulseHigh    = true;
                    _steamPulseTimer   = 0f;
                }

                _steamPulseTimer += dt;
                float overshootFactor = Mathf.Clamp01(speedDiff / 5f);

                const float PULSE_HIGH_DURATION = 2.5f;
                const float PULSE_LOW_DURATION  = 1.5f;

                if (_steamPulseHigh)
                {
                    if (_steamPulseTimer >= PULSE_HIGH_DURATION)
                    { _steamPulseHigh = false; _steamPulseTimer = 0f; }
                }
                else
                {
                    if (_steamPulseTimer >= PULSE_LOW_DURATION)
                    { _steamPulseHigh = true; _steamPulseTimer = 0f; }
                }

                float brakeVal = _steamPulseHigh ? Mathf.Lerp(0.7f, 1f, overshootFactor) : 0f;
                SteamSetBrake(brakeVal);
                SteamSetRegulatorSmooth(0f);
                // Cutoff to neutral while braking — avoids steam fighting brakes
                SteamSetCutoffSmooth(forward ? 0.5f : 0.49f, dt);
            }
            else
            {
                // ── Not braking ───────────────────────────────────────────────
                _steamPulseBraking = false;
                _steamPulseHigh    = false;
                SteamSetBrake(0f);

                if (absSpeed < absTarget - 0.1f)
                {
                    // ── Accelerating ──────────────────────────────────────────
                    float targetRegulator, targetCutoff;

                    if (absSpeed < 10f)
                    {
                        // Gentle start — ramp up regulator, full cutoff
                        targetRegulator = 0.1f + 0.9f * (absSpeed / 10f);
                        targetCutoff    = forward ? 1f : 0f;
                    }
                    else if (absSpeed < 20f)
                    {
                        // Full regulator, full cutoff until up to speed
                        targetRegulator = 1f;
                        targetCutoff    = forward ? 1f : 0f;
                    }
                    else if (avgPressure < 5f)
                    {
                        // Low chest pressure: wide-open cutoff to admit more steam
                        targetRegulator = 1f;
                        targetCutoff    = forward ? 0.35f : 0.15f;
                    }
                    else if (avgPressure < 8f)
                    {
                        targetRegulator = 1f;
                        targetCutoff    = forward
                            ? (0.4f + 0.1f * (avgPressure - 5f) / 3f)
                            : 0.25f;
                    }
                    else if (avgPressure < 12f)
                    {
                        targetRegulator = 1f;
                        float t = (avgPressure - 8f) / 4f;
                        targetCutoff    = forward ? (0.55f + 0.25f * t) : 0.35f;
                    }
                    else
                    {
                        // Good pressure: efficient cutoff (~75%)
                        targetRegulator = 1f;
                        targetCutoff    = forward ? 0.75f : 0.25f;
                    }

                    SteamSetRegulatorSmooth(targetRegulator);
                    SteamSetCutoffSmooth(targetCutoff, dt);
                }
                else
                {
                    // ── Coasting at target speed ──────────────────────────────
                    // Keep tiny regulator so steam flows (engine sound + steam heat)
                    SteamSetRegulatorSmooth(0.1f);
                    SteamSetCutoffSmooth(0.5f, dt);
                }
            }

            return 0f;
        }

        private void SteamSetRegulatorSmooth(float target)
        {
            _steamRegulator = Mathf.Lerp(_steamRegulator, target, 0.15f);
            _steamOverrider?.Throttle?.Set(_steamRegulator);
        }

        // cutoff 0 = full reverse, 0.5 = neutral, 1 = full forward
        private void SteamSetCutoffSmooth(float target, float dt)
        {
            float delta = target - _steamCutoff;
            if (Mathf.Abs(delta) > 0.005f)
                _steamCutoff += delta * 0.15f * dt;
            else
                _steamCutoff = target;

            _steamOverrider?.Reverser?.Set(_steamCutoff);
        }

        private void SteamSetBrake(float value)
        {
            _steamBrakeTarget = value;
            _steamOverrider?.Brake?.Set(value);
        }

        private float UpdateSteamPressureAvg(float currentPressure, float dt)
        {
            _steamPressureTimer += dt;
            if (_steamPressureTimer >= 0.1f)
            {
                _steamPressureSamples.Enqueue(currentPressure);
                _steamPressureTimer = 0f;
                while (_steamPressureSamples.Count > 10)
                    _steamPressureSamples.Dequeue();
            }

            if (_steamPressureSamples.Count == 0) return currentPressure;

            float sum = 0f;
            foreach (float s in _steamPressureSamples) sum += s;
            return sum / _steamPressureSamples.Count;
        }

        private float GetSteamChestPressure()
        {
            try
            {
                if (_steamSimCtrl == null)
                    _steamSimCtrl = trainCar?.GetComponentInChildren<SimController>();
                if (_steamSimCtrl?.simFlow == null) return 8f;

                if (_tryGetPortMI == null)
                    _tryGetPortMI = _steamSimCtrl.simFlow.GetType()
                        .GetMethod("TryGetPort", BindingFlags.Instance | BindingFlags.Public);
                if (_tryGetPortMI == null) return 8f;

                var args = new object[] { "steamEngine.STEAM_CHEST_PRESSURE", null, true };
                if (!(bool)_tryGetPortMI.Invoke(_steamSimCtrl.simFlow, args) || args[1] == null)
                    return 8f;

                if (_portValueProp == null)
                    _portValueProp = args[1].GetType().GetProperty("Value");

                return _portValueProp != null ? (float)_portValueProp.GetValue(args[1]) - 1f : 8f;
            }
            catch { return 8f; }
        }

        // ────────────────────────────────────────────────────────────────────

        public void Stop()
        {
            running = false;
        }

        protected IEnumerator CruiseControlCoroutine()
        {
            float prevSpeed = 0.0f;
            float targetAcceleration = 2.5f;
            float prevTime = Time.time;

            const float TIME_WAIT = 0.1f;
            float timeDelta = TIME_WAIT;

            while (running)
            {
                float speed        = Mathf.Abs(remoteControl.GetForwardSpeed() * 3.6f);
                float acceleration = (speed - prevSpeed) / timeDelta;

                targetAcceleration = MaintainSpeed(targetAcceleration, timeDelta, speed, acceleration);

                float targetThrottle       = remoteControl.GetTargetThrottle();
                float targetIndependentBrake = remoteControl.GetTargetIndependentBrake();
                float targetBrake          = remoteControl.GetTargetBrake();

                prevSpeed = speed;

                yield return new WaitForSeconds(TIME_WAIT);

                timeDelta = Time.time - prevTime;
                prevTime  = Time.time;

                if (Mathf.Abs(targetThrottle - remoteControl.GetTargetThrottle()) > 1.0f * TIME_WAIT)
                    running = false;

                if (Mathf.Abs(targetIndependentBrake - remoteControl.GetTargetIndependentBrake()) > 1.0f * TIME_WAIT
                    || Mathf.Abs(targetBrake - remoteControl.GetTargetBrake()) > 0.1f * TIME_WAIT)
                {
                    if (remoteControl.GetTargetThrottle() > Mathf.Epsilon)
                    {
                        remoteControl.UpdateThrottle(-100.0f);
                        running = false;
                    }
                }

                if (!running)
                    Module.PlayClip(Module.offClip);
            }
        }

        protected float ThrottleCurveFactor(float target, bool increasing)
        {
            if (target < 0.4f)    return 0.01f;
            if (target > 0.6f && increasing) return 0.0005f;
            return increasing ? 0.002f : 0.01f;
        }

        public void Dispose()
        {
            running = false;
        }

        // ── Static cruise control helpers (legacy, not in UI) ────────────────

        public static void ToggleCruiseControl(float? speed = null)
        {
            if (CruiseControl == null || !CruiseControl.Running)
                SetCruiseControl(speed);
            else
            {
                Module.PlayClip(Module.offClip);
                ResetCruiseControl();
            }
        }

        public static void ResetCruiseControl()
        {
            if (CruiseControl != null)
            {
                CruiseControl.Dispose();
                CruiseControl = null;
                OnCruiseControlChange?.Invoke(null, null);
            }
        }

        public static event EventHandler OnCruiseControlChange;

        public static float SetCruiseControl(float? speed = null)
        {
            TrainCar car = PlayerManager.LastLoco;
            if (car == null)
                throw new ArgumentNullException(nameof(car));

            if (!speed.HasValue)
                speed = Mathf.Abs(car.GetForwardSpeed() * 3.6f);

            if (CruiseControl != null && CruiseControl.Running)
            {
                UpdateTargetSpeed(speed.Value - CruiseControl.TargetSpeed);
                return speed.Value;
            }

            ResetCruiseControl();
            Module.PlayClip(Module.onClip);

            ILocomotiveRemoteControl remote = car.GetComponent<ILocomotiveRemoteControl>();
            CruiseControl = new LocoCruiseControl(remote, car);
            CruiseControl.StartCruiseControl(speed.Value);

            OnCruiseControlChange?.Invoke(null, null);
            return speed.Value;
        }

        public static float UpdateTargetSpeed(float speedOffset)
        {
            if (CruiseControl != null)
            {
                CruiseControl.TargetSpeed += speedOffset;
                if (CruiseControl.TargetSpeed < 0.0f)
                    CruiseControl.TargetSpeed = 0.0f;
                OnCruiseControlChange?.Invoke(null, null);
                return CruiseControl.TargetSpeed;
            }
            return 0.0f;
        }

        public static float? GetTargetSpeed()
        {
            if (CruiseControl != null)
                return CruiseControl.TargetSpeed;
            return null;
        }
    }
}
