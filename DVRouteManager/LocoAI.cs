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
    public class LocoAI
    {
        private const float TARGET_SPEED = 20.0f;
        private const float COUPLER_APPROACH_SPEED = 2.0f;
        private RouteTracker RouteTracker;
        private ILocomotiveRemoteControl RemoteControl;

        public LocoAI(RouteTracker routeTracker, ILocomotiveRemoteControl remoteControl)
        {
            RouteTracker = routeTracker;
            RemoteControl = remoteControl;
        }

        public bool Start()
        {
            if (RouteTracker.TrackState != RouteTracker.TrackingState.BeforeStart && RouteTracker.TrackState != RouteTracker.TrackingState.OnStart)
                return false;

            Module.StartCoroutine(MainCoroutine());
            return true;
        }

        private IEnumerator MainCoroutine()
        {

            const float TIME_WAIT= 0.3f;

            Terminal.Log("Autonomous driver start");
            yield return null;

            float targetSpeed = 0.0f;
            bool shouldreverse = false;

            RemoteControl.UpdateReverser(ToggleDirection.UP);
            yield return null;
            RemoteControl.UpdateReverser(ToggleDirection.UP);
            yield return null;

            RouteTracker.Route.AdjustSwitches();

            float prevSpeed = 0.0f;
            float targetAcceleration = 2.5f;
            float prevTime = Time.time;

            float timeDelta = TIME_WAIT;

            bool couplerApproach = false;

            while (true)
            {
                float speed = Mathf.Abs(RemoteControl.GetForwardSpeed() * 3.6f);
                float acceleration = (speed - prevSpeed) / timeDelta;

                if(couplerApproach)
                {
                    if (IsCouplerInRange(1.00f))
                    {
                        Terminal.Log($"coupler in ranch");
                        break; //stop train
                    }
                    else if (IsCouplerInRange(3.0f))
                    {
                        targetSpeed = 1.0f;
                    }
                    else
                    {
                        targetSpeed = COUPLER_APPROACH_SPEED;
                    }
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.RightHeading)
                {
                    if (RouteTracker.DistanceToFinish < 50.0f && ! RouteTracker.Route.LastTrack.logicTrack.IsFree(RouteTracker.Loco.trainset)) //finds all couplers not only on right rail
                    {
                        targetSpeed = COUPLER_APPROACH_SPEED;
                    }
                    else
                    {
                        targetSpeed = TARGET_SPEED;
                    }
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.OnStart)
                {
                    targetSpeed = TARGET_SPEED;
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.StopTrainAfterSwitch)
                {
                    targetSpeed = 10.0f;
                    shouldreverse = true;
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.WrongHeading)
                {
                    if (speed > 1.0f && !shouldreverse)
                    {
                        targetSpeed = 0.0f;
                        shouldreverse = true;
                    }

                    if (speed < 1.0f && shouldreverse)
                    {
                        yield return Module.StartCoroutine(Reverse());
                        shouldreverse = false;
                        targetSpeed = TARGET_SPEED;
                    }
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.ReverseTrain)
                {
                    if(speed < 1.0f && shouldreverse)
                    {
                        yield return Module.StartCoroutine(Reverse());
                        shouldreverse = false;
                        targetSpeed = TARGET_SPEED;
                    }

                    if (shouldreverse)
                    {
                        targetSpeed = 0.0f;
                    }
                }

                float speedDiff = targetSpeed - speed;

                float diff = targetAcceleration - acceleration;

                Terminal.Log($"targetSpeed {targetSpeed} speediff {speedDiff} accel {acceleration} diff {diff} throttle {RemoteControl.GetTargetThrottle()}");

                if (speedDiff > 5.0f)
                {
                    /*if (RemoteControl.GetTargetBrake() > 0.001f)
                    {
                        RemoteControl.UpdateBrake(-30.0f * TIME_BLOCK);
                    }*/

                    if (RemoteControl.GetTargetIndependentBrake() > Mathf.Epsilon)
                    {
                        RemoteControl.UpdateIndependentBrake(-30.0f * timeDelta);
                    }

                    if (RemoteControl.IsWheelslipping())
                    {
                        targetAcceleration -= 0.5f * timeDelta;
                        RemoteControl.UpdateThrottle( - RemoteControl.GetTargetThrottle() * 5.0f  * timeDelta);
                    }
                    else
                    {
                        RemoteControl.UpdateThrottle(ThrottleCurve(RemoteControl.GetTargetThrottle(), diff > 0.0f) * diff * timeDelta);
                    }
                }
                else if (speedDiff < -3.0f)
                {
                    RemoteControl.UpdateThrottle(-0.06f * speedDiff * -1.0f * timeDelta);
                    RemoteControl.UpdateIndependentBrake(0.3f * speedDiff * -1.0f * timeDelta);
                }
                else
                {
                    if (RemoteControl.IsWheelslipping())
                    {
                        RemoteControl.UpdateThrottle(-RemoteControl.GetTargetThrottle() * 5.0f * timeDelta);
                    }
                    else
                    {
                        float accelCoef = -(Mathf.Sign(acceleration) * acceleration * acceleration * 0.5f);
                        float speedCoef = Mathf.Sign(speedDiff) * speedDiff * speedDiff * 0.02f;
                        float throttleDiff = 0.0f;

                        if (Mathf.Abs(accelCoef) > 0.01f)
                        {
                            throttleDiff += accelCoef;
                        }

                        if (Mathf.Abs(speedCoef) > 0.01f)
                        {
                            throttleDiff += speedCoef;
                        }

                        throttleDiff = Mathf.Clamp(throttleDiff, -10.0f, 5.0f);

                        Terminal.Log($"throttleDiff {throttleDiff} accelCoef {accelCoef} speedCoef {speedCoef}");
                        RemoteControl.UpdateThrottle(throttleDiff * timeDelta);
                        RemoteControl.UpdateIndependentBrake(-2.0f * timeDelta);
                    }
                }

                prevSpeed = speed;

                yield return new WaitForSeconds(TIME_WAIT);

                timeDelta = Time.time - prevTime;
                prevTime = Time.time;

                if (RouteTracker.TrackState == RouteTracker.TrackingState.OutOfWay)
                    break;

                if(RouteTracker.TrackState == RouteTracker.TrackingState.OnFinish)
                {
                    if(RouteTracker.Route.LastTrack.logicTrack.IsFree(RouteTracker.Loco.trainset))
                    {
                        break;
                    }
                    else
                    {
                        //on last track is some other car so try to go close to it's coupler
                        couplerApproach = true;
                        Terminal.Log($"coupler approach");
                    }
                }
            }

            for (int i = 0; i < 10; i++)
            {
                RemoteControl.UpdateIndependentBrake(10.0f);
                RemoteControl.UpdateBrake(1.0f);
                RemoteControl.UpdateThrottle(-10.0f);
                yield return new WaitForSeconds(0.3f);
            }

            RouteTracker.Dispose();
        }

        bool IsCouplerInRange(float range)
        {
            Coupler lastCoupler = CouplerLogic.GetLastCoupler(this.RouteTracker.Loco.frontCoupler);
            Coupler lastCoupler2 = CouplerLogic.GetLastCoupler(this.RouteTracker.Loco.rearCoupler);
            Coupler firstCouplerInRange = lastCoupler.GetFirstCouplerInRange(range);
            Coupler firstCouplerInRange2 = lastCoupler2.GetFirstCouplerInRange(range);
            return firstCouplerInRange != null || firstCouplerInRange2 != null;
        }

        IEnumerator Reverse()
        {
            bool direction = RemoteControl.GetReverserSymbol().ToUpper() == "F";

            while (RemoteControl.GetTargetThrottle() > Mathf.Epsilon)
            {
                RemoteControl.UpdateThrottle(-100.0f);
                yield return null;
            }
            Terminal.Log($"reverse {RemoteControl.GetTargetThrottle()}");
            RemoteControl.UpdateReverser(direction ? ToggleDirection.DOWN : ToggleDirection.UP);
            yield return null;
            Terminal.Log($"reverse {RemoteControl.GetTargetThrottle()}");
            RemoteControl.UpdateReverser(direction ? ToggleDirection.DOWN : ToggleDirection.UP);
            yield return null;
        }

        protected float ThrottleCurve(float target, bool increasing)
        {
            if (increasing)
            {
                if (target < 0.4f)
                    return 0.4f;
                else return 0.06f;
            }
            else
            {
                return 0.5f;
            }
        }
    }
}
