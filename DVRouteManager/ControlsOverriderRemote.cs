using DV.Simulation.Cars;
using DV.Simulation.Controllers;
using System;
using UnityEngine;

namespace DVRouteManager
{
    /// <summary>
    /// Implements ILocomotiveRemoteControl directly via BaseControlsOverrider / SimController.
    /// Used as a fallback for locomotives that don't have RemoteControllerModule (e.g. DM3).
    /// Mirrors the same logic as RemoteControllerModule for the methods LocoAI uses.
    /// </summary>
    internal class ControlsOverriderRemote : ILocomotiveRemoteControl
    {
        private readonly TrainCar car;
        private readonly SimController sim;
        private readonly BaseControlsOverrider co;

        private readonly float throttleStep;
        private readonly float brakeStep;
        private readonly float indBrakeStep;

        private MultipleUnitStateObserver _muObserver;

        public ControlsOverriderRemote(TrainCar car, SimController sim)
        {
            this.car = car;
            this.sim  = sim;
            this.co   = sim.controlsOverrider;

            throttleStep = CalcStep(co.Throttle);
            brakeStep    = CalcStep(co.Brake);
            indBrakeStep = CalcStep(co.IndependentBrake);
        }

        private static float CalcStep(OverridableBaseControl ctrl)
        {
            if (ctrl == null || !ctrl.IsNotched) return 0.1f;
            return 1f / ctrl.NotchCount;
        }

        // ── ILocomotiveRemoteControl ─────────────────────────────────────

        public float GetForwardSpeed() => car.GetForwardSpeed();

        public string GetReverserSymbol()
        {
            float v = co.Reverser?.Value ?? 0.5f;
            if (v == 1f) return "F";
            if (v == 0f) return "R";
            return "N";
        }

        public float GetReverserValue() => co.Reverser?.Value ?? 0.5f;

        public float GetTargetThrottle()       => co.Throttle?.Value          ?? 0f;
        public float GetTargetBrake()          => co.Brake?.Value             ?? 0f;
        public float GetTargetIndependentBrake() => co.IndependentBrake?.Value ?? 0f;
        public float GetBrakeIndicatorValue()  => GetTargetBrake();

        public bool IsWheelslipping(bool includeMUConnections = false)
        {
            if (includeMUConnections)
            {
                var obs = GetMUObserver();
                if (obs != null) return obs.AnyInChainWheelslipping;
            }
            return sim.wheelslipController != null && sim.wheelslipController.wheelslip > 0f;
        }

        public MultipleUnitStateObserver.TemperatureState GetEngineTemperatureState(bool includeMUConnections)
        {
            var obs = GetMUObserver();
            if (obs == null) return MultipleUnitStateObserver.TemperatureState.Nominal;
            return includeMUConnections ? obs.MUChainTemperatureState : obs.CarTemperatureState;
        }

        public void UpdateThrottle(float factor)
            => co.Throttle?.Set(co.Throttle.Value + factor * throttleStep);

        public void UpdateBrake(float factor)
            => co.Brake?.Set(co.Brake.Value + factor * brakeStep);

        public void UpdateIndependentBrake(float factor)
            => co.IndependentBrake?.Set(co.IndependentBrake.Value + factor * indBrakeStep);

        public void UpdateReverser(ToggleDirection toggle)
            => co.Reverser?.Set(GetReverserValue() + (toggle == ToggleDirection.UP ? 0.5f : -0.5f));

        public void UpdateSand(ToggleDirection toggle)
            => co.Sander?.Set(toggle == ToggleDirection.UP ? 1f : 0f);

        public void UpdateHorn(float value)
            => co.Horn?.Set(Mathf.Abs(value));

        // ── Methods not used by LocoAI — no-op / safe defaults ──────────

        public bool IsPaired              => false;
        public bool IsReadyToPair         => false;
        public bool IsActivelyControlled  => false;

        public event Action<bool> PairingChanged { add { } remove { } }

        public void PairRemoteController(LocomotiveRemoteController remote)   { }
        public void UnpairRemoteController(LocomotiveRemoteController remote) { }

        public bool IsSandOn()    => (co.Sander?.Value ?? 0f) > 0f;
        public bool IsDerailed()  => car.derailed;
        public Vector3 GetPosition() => car.transform.position;

        public int GetNumberOfCarsInFront() => 0;
        public int GetNumberOfCarsInRear()  => 0;

        public bool IsCouplerInRange(float range) => false;

        public void RemoteControllerCouple() { }
        public void Uncouple(int selectedCoupler) { }

        public string GetLocoGuid() => car?.CarGUID ?? "";

        // ────────────────────────────────────────────────────────────────

        private MultipleUnitStateObserver GetMUObserver()
        {
            if (_muObserver == null)
                _muObserver = car.GetComponent<MultipleUnitStateObserver>();
            return _muObserver;
        }
    }
}
