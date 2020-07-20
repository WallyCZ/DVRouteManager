using CommandTerminal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRouteManager
{
    public class LocoCruiseControl  : IDisposable
    {
        protected ILocomotiveRemoteControl remoteControl;
        public float TargetSpeed { get; protected set; } = 20.0f;
        protected bool running;

        public LocoCruiseControl(ILocomotiveRemoteControl remoteControl)
        {
            this.remoteControl = remoteControl;
        }

        public bool StartCruiseControl(float targetSpeed)
        {
            this.TargetSpeed = targetSpeed;
            running = true;

            Module.StartCoroutine(CruiseControlCoroutine());
            return true;
        }

        private const float Kp_SPEED = 1.5f;
        private const float Ki_SPEED = 0.0005f;
        private const float Kd_SPEED = 7f;
        private const float Kp_SPEED_RETURN_FACTOR = 1.5f;

        float integral = 0;
        float previousError = 0;

        //https://en.wikipedia.org/wiki/PID_controller
        protected float MaintainSpeed(float targetAcceleration, float dt, float speed, float acceleration)
        {
            float error = TargetSpeed - speed;
            integral += error * dt;

            if (error < 0)
            {
                integral = 0;
            }

            float derivative = (error - previousError) / dt;

            float Kp = Kp_SPEED;
            float Ki = Ki_SPEED;
            float Kd = Kd_SPEED;

            if (error < 0)
            {
                Kp = Kp * Kp_SPEED_RETURN_FACTOR;
            }

            float P = Kp * error; // Proportional
            float I = Ki * integral; // Integral
            float D = Kd * derivative; // Derivative

            float controlValue = P + I + D;

            previousError = error;
#if DEBUG
            Terminal.Log($"targetSpeed {TargetSpeed} error {error} accel {acceleration} P {P} I {I} D {D} controlValue {controlValue} throttle {remoteControl.GetTargetThrottle()}");
#endif

            if(controlValue > 20.0f)
            {
                controlValue = 20.0f;
            }

            if(acceleration > targetAcceleration)
            {
                controlValue = 0f;
            }

            if (remoteControl.IsWheelslipping())
            {
                targetAcceleration -= 0.5f * dt;
                controlValue = -50.0f;
            }

            if (error < - 3.0f)
            {
                remoteControl.UpdateIndependentBrake(0.3f * error * -1.0f * dt);
            }
            else if (remoteControl.GetTargetIndependentBrake() > Mathf.Epsilon)
            {
                remoteControl.UpdateIndependentBrake(-30.0f * dt);
            }


            remoteControl.UpdateThrottle(ThrottleCurveFactor( remoteControl.GetTargetThrottle(), controlValue > 0.0f) * controlValue);


            return targetAcceleration;
        }

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
                float speed = Mathf.Abs(remoteControl.GetForwardSpeed() * 3.6f);
                float acceleration = (speed - prevSpeed) / timeDelta;

                targetAcceleration = MaintainSpeed(targetAcceleration, timeDelta, speed, acceleration);

                prevSpeed = speed;

                yield return new WaitForSeconds(TIME_WAIT);

                timeDelta = Time.time - prevTime;
                prevTime = Time.time;
            }
        }

      
        protected float ThrottleCurveFactor(float target, bool increasing)
        {
            if (target < 0.4f)
                return 0.01f;

            if (target > 0.6f & increasing)
                return 0.0005f;

            return increasing ? 0.002f : 0.01f;
        }

        public void Dispose()
        {
            running = false;
        }


        private static LocoCruiseControl CruiseControl;

        public static bool IsSet { get => CruiseControl != null; }

        public static void ToggleCruiseControl(float? speed = null)
        {
            if (CruiseControl == null)
            {
                Module.PlayClip(Module.onClip);
                SetCruiseControl(speed);
            }
            else
            {
                Module.PlayClip(Module.offClip);
                CruiseControl.Stop();
                CruiseControl = null;
            }
        }
        public static void ResetCruiseControl()
        {
            if (CruiseControl != null)
            {
                CruiseControl.Dispose();
                CruiseControl = null;
            }
        }

        public static float SetCruiseControl(float? speed = null)
        {
            ResetCruiseControl();

            TrainCar trainCar = PlayerManager.LastLoco;

            if (trainCar == null)
            {
                throw new ArgumentNullException(nameof(trainCar));
            }

            if (!speed.HasValue)
            {
                speed = Mathf.Abs(trainCar.GetForwardSpeed() * 3.6f);
            }

            ILocomotiveRemoteControl remote = trainCar.GetComponent<ILocomotiveRemoteControl>();

            CruiseControl = new LocoCruiseControl(remote);
            CruiseControl.StartCruiseControl(speed.Value);

            return speed.Value;
        }

        public static float UpdateTargetSpeed(float speedOffset)
        {
            if (CruiseControl != null)
            {
                CruiseControl.TargetSpeed += speedOffset;
                if (CruiseControl.TargetSpeed < 0.0f)
                    CruiseControl.TargetSpeed = 0.0f;

                return CruiseControl.TargetSpeed;
            }

            return 0.0f;
        }

        public static float? GetTargetSpeed()
        {
            if (CruiseControl != null)
            {
                return CruiseControl.TargetSpeed;
            }

            return null;
        }

    }
}
