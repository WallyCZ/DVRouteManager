using CommandTerminal;
using DV.Logic.Job;
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
    public class LocoAI : LocoCruiseControl
    {
        private const float TARGET_SPEED_DEFAULT = 20.0f;
        private const float COUPLER_APPROACH_SPEED = 5.0f;
        private RouteTracker RouteTracker;

        public bool IsRunning => running;
        private bool _freightHaulActive = false;
        public bool IsFreightHaulActive => _freightHaulActive;

        public LocoAI(ILocomotiveRemoteControl remoteControl, TrainCar car) :
            base(remoteControl, car)
        {
        }

        // Disables DriverAssist and SteamCruiseControl via reflection so they don't fight us.
        // Both mods are optional — failures are silently swallowed.
        private static void DisableCompetingMods()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // DriverAssist: EntityManager.Instance.Loco.Components.CruiseControl = null
            try
            {
                var asm = assemblies.FirstOrDefault(a => a.GetName().Name == "DriverAssist");
                if (asm != null)
                {
                    Type entityManagerType = asm.GetType("EntityManager");
                    FieldInfo instanceField = entityManagerType?.GetField("Instance");
                    object entityManager = instanceField?.GetValue(null);
                    FieldInfo locoField = entityManager?.GetType().GetField("Loco");
                    object loco = locoField?.GetValue(entityManager);
                    if (loco != null)
                    {
                        FieldInfo componentsField = loco.GetType().GetField("Components");
                        object components = componentsField?.GetValue(loco);
                        if (components != null)
                        {
                            PropertyInfo cruiseControlProp = components.GetType().GetProperty("CruiseControl");
                            cruiseControlProp?.SetValue(components, null);
                            Terminal.Log("DriverAssist cruise control disabled");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Module.mod.Logger.Log("DisableCompetingMods DriverAssist: " + e.Message);
            }

            // SteamCruiseControl: Main._cruiseControlManager.IsEnabled = false
            try
            {
                var asm = assemblies.FirstOrDefault(a => a.GetName().Name == "SteamCruiseControl");
                if (asm != null)
                {
                    Type mainType = asm.GetType("SteamCruiseControl.Main");
                    FieldInfo managerField = mainType?.GetField("_cruiseControlManager", BindingFlags.Static | BindingFlags.NonPublic);
                    object manager = managerField?.GetValue(null);
                    if (manager != null)
                    {
                        PropertyInfo isEnabledProp = manager.GetType().GetProperty("IsEnabled");
                        isEnabledProp?.SetValue(manager, false);
                        Terminal.Log("SteamCruiseControl disabled");
                    }
                }
            }
            catch (Exception e)
            {
                Module.mod.Logger.Log("DisableCompetingMods SteamCruiseControl: " + e.Message);
            }
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

            DisableCompetingMods();
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

            RouteTracker.TrackingState lastState = RouteTracker.TrackState;

            while (running)
            {
                float speed = Mathf.Abs(remoteControl.GetForwardSpeed() * 3.6f);
                float acceleration = (speed - prevSpeed) / timeDelta;

                bool stateChanged = false;

                if(lastState != RouteTracker.TrackState)
                {
                    lastState = RouteTracker.TrackState;
                    stateChanged = true;
                }

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
                    if (RouteTracker.DistanceToFinish < 50.0f && !RouteTracker.Route.LastTrack.LogicTrack().IsFree(RouteTracker.Trainset)) //finds all couplers not only on right rail
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
                    if (!shouldreverse)
                    {
                        shouldreverse = true;
                    }
                }
                else if (RouteTracker.TrackState == RouteTracker.TrackingState.WrongHeading
                    || RouteTracker.TrackState == RouteTracker.TrackingState.ReverseTrain)
                {
                    if (stateChanged)
                    {
                        //https://www.wolframalpha.com/input/?i=InterpolatingPolynomial%5B%7B%7B5%2C+4%7D%2C+%7B100%2C+15%7D%7D%2C+x%5D
                        int brakeLevel = 11 * ((int)speed - 5) / 95 + 4;
                        float brakeTime = speed / 5.0f;
                        Module.StartCoroutine(BrakePulse(brakeLevel, brakeTime));
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

                targetAcceleration = MaintainSpeed(targetAcceleration, timeDelta, speed, acceleration);

                prevSpeed = speed;

                yield return new WaitForSeconds(TIME_WAIT);

                timeDelta = Time.time - prevTime;
                prevTime = Time.time;

                if (RouteTracker.TrackState == RouteTracker.TrackingState.OutOfWay)
                    break;

                if (RouteTracker.TrackState == RouteTracker.TrackingState.OnFinish)
                {
                    if (RouteTracker.Route.LastTrack.LogicTrack().IsFree(RouteTracker.Trainset))
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
            remoteControl.UpdateReverser(direction ? ToggleDirection.DOWN : ToggleDirection.UP);
            yield return null;
            remoteControl.UpdateReverser(direction ? ToggleDirection.DOWN : ToggleDirection.UP);
            yield return null;
        }

        IEnumerator BrakePulse(int level, float waitTime)
        {
            for (int i = 0; i < level; i++)
            {
                remoteControl.UpdateBrake(1.0f);
                yield return new WaitForSeconds(0.1f);
            }

            yield return new WaitForSeconds(waitTime);

            for (int i = 0; i < level + 1; i++)
            {
                remoteControl.UpdateBrake(-1.0f);
                yield return new WaitForSeconds(0.1f);
            }
        }

        // ─── Freight haul ────────────────────────────────────────────────────

        /// <summary>Stops both the AI driving and any active freight haul.</summary>
        public void StopAll()
        {
            _freightHaulActive = false;
            Stop();
        }

        /// <summary>
        /// Starts a full freight haul: loco → cars → couple → release HB → destination → uncouple → apply HB.
        /// </summary>
        public void StartFreightHaul(RouteTask task, TrainCar loco)
        {
            _freightHaulActive = false; // abort any existing haul
            Stop();
            Module.StartCoroutine(FreightHaulCoroutine(task, loco));
        }

        private IEnumerator FreightHaulCoroutine(RouteTask task, TrainCar loco)
        {
            _freightHaulActive = true;

            // ── Phase 1: drive loco to freight cars ──────────────────────────
            Terminal.Log("Freight haul: phase 1 – routing to cars");

            Trainset freightTrainset = task.TrainSets.FirstOrDefault();
            if (freightTrainset == null)
            {
                Terminal.Log("Freight haul: no trainset in task");
                _freightHaulActive = false;
                yield break;
            }

            Track carTrack = freightTrainset.firstCar.Bogies[0].track.LogicTrack();
            Track locoTrack = loco.trainset.firstCar.Bogies[0].track.LogicTrack();

            var toCarsTask = Route.FindRoute(locoTrack, carTrack, ReversingStrategy.ChooseBest, loco.trainset);
            while (!toCarsTask.IsCompleted) yield return null;

            if (!_freightHaulActive) yield break;

            if (toCarsTask.IsFaulted || toCarsTask.Result == null)
            {
                Terminal.Log("Freight haul: cannot find route to cars – " + (toCarsTask.Exception?.InnerException?.Message ?? "null"));
                _freightHaulActive = false;
                yield break;
            }

            var chain1 = RouteTaskChain.FromDestination(carTrack, loco.trainset);
            var tracker1 = new RouteTracker(chain1, true);
            tracker1.SetRoute(toCarsTask.Result, loco.trainset);
            Module.ActiveRoute.Route = toCarsTask.Result;
            Module.ActiveRoute.RouteTracker = tracker1;

            StartAI(tracker1);
            while (running && _freightHaulActive) yield return null;

            if (!_freightHaulActive) { Stop(); yield break; }

            // ── Phase 2: couple and release handbrakes ───────────────────────
            Terminal.Log("Freight haul: phase 2 – coupling");
            yield return TryCoupleAndReleaseHandbrakes(loco);
            yield return new WaitForSeconds(1.5f);

            if (!_freightHaulActive) yield break;

            // ── Phase 3: drive to destination ────────────────────────────────
            Terminal.Log($"Freight haul: phase 3 – routing to {task.DestinationTrack.ID.FullID}");

            Track nowTrack = loco.trainset.firstCar.Bogies[0].track.LogicTrack();
            var toDestTask = Route.FindRoute(nowTrack, task.DestinationTrack, ReversingStrategy.ChooseBest, loco.trainset);
            while (!toDestTask.IsCompleted) yield return null;

            if (!_freightHaulActive) yield break;

            if (toDestTask.IsFaulted || toDestTask.Result == null)
            {
                Terminal.Log("Freight haul: cannot find route to destination – " + (toDestTask.Exception?.InnerException?.Message ?? "null"));
                _freightHaulActive = false;
                yield break;
            }

            var chain2 = RouteTaskChain.FromDestination(task.DestinationTrack, loco.trainset);
            var tracker2 = new RouteTracker(chain2, true);
            tracker2.SetRoute(toDestTask.Result, loco.trainset);
            Module.ActiveRoute.Route = toDestTask.Result;
            Module.ActiveRoute.RouteTracker = tracker2;

            StartAI(tracker2);
            while (running && _freightHaulActive) yield return null;

            if (!_freightHaulActive) { Stop(); yield break; }

            // ── Phase 4: uncouple and apply handbrakes ───────────────────────
            Terminal.Log("Freight haul: phase 4 – uncoupling");
            yield return UncoupleAndApplyHandbrakes(loco);

            _freightHaulActive = false;
            Terminal.Log("Freight haul: complete!");
            Module.PlayClip(Module.trainEnd);
        }

        private IEnumerator TryCoupleAndReleaseHandbrakes(TrainCar loco)
        {
            // Couple any couplers at the ends of the current trainset that are in range
            Coupler frontEnd = CouplerLogic.GetLastCoupler(loco.trainset.firstCar.frontCoupler);
            Coupler rearEnd = CouplerLogic.GetLastCoupler(loco.trainset.lastCar.rearCoupler);
            frontEnd.GetFirstCouplerInRange(2.5f)?.TryCouple();
            rearEnd.GetFirstCouplerInRange(2.5f)?.TryCouple();
            yield return new WaitForSeconds(0.5f);

            // Release handbrakes on all non-loco cars now in the trainset
            foreach (TrainCar car in loco.trainset.cars)
            {
                if (!car.IsLoco && car.brakeSystem.hasHandbrake)
                {
                    car.brakeSystem.SetHandbrakePosition(0f);
                    Terminal.Log($"Released handbrake on {car.logicCar.ID}");
                }
            }
        }

        private IEnumerator UncoupleAndApplyHandbrakes(TrainCar loco)
        {
            // Record freight cars before uncoupling so we can apply their handbrakes after
            List<TrainCar> freightCars = loco.trainset.cars.Where(c => !c.IsLoco).ToList();

            if (loco.trainset.firstCar == loco)
                loco.rearCoupler.Uncouple();
            else if (loco.trainset.lastCar == loco)
                loco.frontCoupler.Uncouple();
            else
            {
                loco.frontCoupler.Uncouple();
                yield return null;
                loco.rearCoupler.Uncouple();
            }

            yield return new WaitForSeconds(0.5f);

            foreach (TrainCar car in freightCars)
            {
                if (car.brakeSystem.hasHandbrake)
                {
                    car.brakeSystem.SetHandbrakePosition(1f);
                    Terminal.Log($"Applied handbrake on {car.logicCar.ID}");
                }
            }
        }

    }
}
