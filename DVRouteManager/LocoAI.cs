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
    public class LocoAI : LocoCruiseControl
    {
        private const float TARGET_SPEED_DEFAULT = 20.0f;
        private const float COUPLER_APPROACH_SPEED = 5.0f;
        private RouteTracker RouteTracker;

        public LocoAI(ILocomotiveRemoteControl remoteControl) :
            base(remoteControl)
        {
        }

        public bool StartAI(RouteTracker routeTracker)
        {
            if(RouteTracker != null)
            {
                RouteTracker.Dispose();
            }

            RouteTracker = routeTracker;

            if (RouteTracker.TrackState != RouteTracker.TrackingState.BeforeStart && RouteTracker.TrackState != RouteTracker.TrackingState.OnStart)
                return false;

            RouteTracker.Route.AdjustSwitches();

            TargetSpeed = TARGET_SPEED_DEFAULT;

            if (!running)
            {
                running = true;
                Module.StartCoroutine(AICoroutine());
            }

            return true;
        }

        private IEnumerator AICoroutine()
        {
            const float TIME_WAIT = 0.3f;

            Terminal.Log("Autonomous driver start");
            yield return null;

            bool shouldreverse = false;

            remoteControl.UpdateReverser(ToggleDirection.UP);
            yield return null;
            remoteControl.UpdateReverser(ToggleDirection.UP);
            yield return null;

            float prevSpeed = 0.0f;
            float targetAcceleration = 2.5f;
            float prevTime = Time.time;

            float timeDelta = TIME_WAIT;

            bool couplerApproach = false;

            while (running)
            {
                float speed = Mathf.Abs(remoteControl.GetForwardSpeed() * 3.6f);
                float acceleration = (speed - prevSpeed) / timeDelta;

                if (couplerApproach)
                {
                    if (IsCouplerInRange(1.00f))
                    {
                        Terminal.Log($"coupler in ranch");
                        break; //stop train
                    }
                    else if (IsCouplerInRange(7.0f))
                    {
                        TargetSpeed = 1.0f;
                    }
                    else
                    {
                        TargetSpeed = COUPLER_APPROACH_SPEED;
                    }
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.RightHeading)
                {
                    if (RouteTracker.DistanceToFinish < 50.0f && !RouteTracker.Route.LastTrack.logicTrack.IsFree(RouteTracker.Trainset)) //finds all couplers not only on right rail
                    {
                        TargetSpeed = COUPLER_APPROACH_SPEED;
                    }
                    else
                    {
                        TargetSpeed = TARGET_SPEED_DEFAULT;
                    }
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.OnStart)
                {
                    TargetSpeed = TARGET_SPEED_DEFAULT;
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.StopTrainAfterSwitch)
                {
                    TargetSpeed = 10.0f;
                    shouldreverse = true;
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.WrongHeading)
                {
                    if (speed > 3.0f && !shouldreverse)
                    {
                        TargetSpeed = 0.0f;
                        shouldreverse = true;
                    }

                    if (speed < 3.0f && shouldreverse)
                    {
                        yield return Module.StartCoroutine(Reverse());
                        shouldreverse = false;
                        TargetSpeed = TARGET_SPEED_DEFAULT;
                    }
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.ReverseTrain)
                {
                    if (speed < 5.0f && shouldreverse)
                    {
                        yield return Module.StartCoroutine(Reverse());
                        shouldreverse = false;
                        TargetSpeed = TARGET_SPEED_DEFAULT;
                    }

                    if (shouldreverse)
                    {
                        TargetSpeed = 0.0f;
                    }
                }

                targetAcceleration = MaintainSpeed(targetAcceleration, timeDelta, speed, acceleration);

                prevSpeed = speed;

                yield return new WaitForSeconds(TIME_WAIT);

                timeDelta = Time.time - prevTime;
                prevTime = Time.time;

                if (RouteTracker.TrackState == RouteTracker.TrackingState.OutOfWay)
                    break;

                if (RouteTracker.TrackState == RouteTracker.TrackingState.OnFinish)
                {
                    if (RouteTracker.Route.LastTrack.logicTrack.IsFree(RouteTracker.Trainset))
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

            running = false;

            for (int i = 0; i < 10; i++)
            {
                remoteControl.UpdateIndependentBrake(10.0f);
                remoteControl.UpdateBrake(1.0f);
                remoteControl.UpdateThrottle(-10.0f);
                yield return new WaitForSeconds(0.3f);
            }

            RouteTracker.Dispose();
        }

        bool IsCouplerInRange(float range)
        {
            Coupler lastCoupler = CouplerLogic.GetLastCoupler(this.RouteTracker.Trainset.firstCar.frontCoupler);
            Coupler lastCoupler2 = CouplerLogic.GetLastCoupler(this.RouteTracker.Trainset.lastCar.rearCoupler);
            Coupler firstCouplerInRange = lastCoupler.GetFirstCouplerInRange(range);
            Coupler firstCouplerInRange2 = lastCoupler2.GetFirstCouplerInRange(range);
            return firstCouplerInRange != null || firstCouplerInRange2 != null;
        }

        IEnumerator Reverse()
        {
            bool direction = remoteControl.GetReverserSymbol().ToUpper() == "F";

            while (remoteControl.GetTargetThrottle() > Mathf.Epsilon || Mathf.Abs( remoteControl.GetForwardSpeed() ) > 0.1)
            {
                remoteControl.UpdateIndependentBrake(1.0f);
                remoteControl.UpdateThrottle(-100.0f);
                yield return null;
            }
            Terminal.Log($"reverse {remoteControl.GetTargetThrottle()}");
            remoteControl.UpdateReverser(direction ? ToggleDirection.DOWN : ToggleDirection.UP);
            yield return null;
            Terminal.Log($"reverse {remoteControl.GetTargetThrottle()}");
            remoteControl.UpdateReverser(direction ? ToggleDirection.DOWN : ToggleDirection.UP);
            yield return null;
        }

    }
}
