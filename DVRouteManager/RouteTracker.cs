using CommandTerminal;
using DV;
using DV.Logic.Job;
using DVRouteManager.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DVRouteManager
{
    public class RouteTracker : IDisposable
    {
        public enum TrackingState
        {
            BeforeStart,
            OnStart,
            RightHeading,
            WrongHeading,
            OutOfWay,
            StopTrainAfterSwitch,
            ReverseTrain,
            OnFinish

        }

        private bool Running = false;
        private bool audio;
        public Route Route { get; private set;}

        public RouteTask CurrentTask { get; protected set; }
        public RouteTaskChain TaskChain { get; protected set; }

        private double notifyTrainEndPosition = double.MaxValue;

        public Trainset Trainset { get; protected set; }

        public float RecommendedSpeed { get; protected set; }

        public float ElapsedTime { get; protected set; }

        public RouteTracker(RouteTaskChain chain, bool audio)
        {
            TaskChain = chain;
            TakeTask();
            this.audio = audio;

            /*if(audio)
            {
                InputSystem
            }*/
        }

        public double DistanceTraveled { get; set; } = 0.0;

        public double DistanceToFinish
        {
            get
            {
                return Route.Length - DistanceTraveled;
            }
        }

        public void SetRoute(Route route, Trainset trainset)
        {
            Trainset = trainset;

            if (route == null || route.Path.Count == 0)
            {
                throw new ArgumentNullException(nameof(route));
            }

            Route = route;
            ElapsedTime = 0.0f;

            TrackState = TrackingState.BeforeStart;

            if (!Running)
            {
                Module.StartCoroutine(PositionUpdate());
            }
        }

        private TrackingState _trackState;
        public TrackingState TrackState
        {
            get
            {
                return _trackState;
            }

            private set
            {
                if (_trackState == value)
                    return;
#if DEBUG
                Terminal.Log($"{_trackState} -> {value}");
#endif
                _trackState = value;

                if (audio)
                {
                    switch (_trackState)
                    {
                        case TrackingState.ReverseTrain:
                            Module.PlayClip(Module.trainEnd);
                            break;
                        case TrackingState.StopTrainAfterSwitch:
                            Module.PlayClip(Module.stopTrainClip);
                            break;
                        case TrackingState.WrongHeading:
                        case TrackingState.OutOfWay:
                            Module.PlayClip(Module.wrongWayClip);
                            break;
                    }
                }
            }
        }

        protected void TakeTask()
        {
            if (TaskChain.tasks.Count == 0)
            {
#if DEBUG
                Terminal.Log($"Chain has no task, try to switch other chain");
#endif
                TaskChain = TaskChain.nextTasks;

                if (TaskChain == null)
                {
                    CurrentTask = null;
                    Terminal.Log($"No next chain");
                    return;
                }
                else
                {
                    TakeTask();
                    return;
                }

            }

            if (TaskChain.tasks.Count == 1)
            {
#if DEBUG
                Terminal.Log($"Taking single avilable task");
#endif
                CurrentTask = TaskChain.tasks[0];
            }
            else
            {
#if DEBUG
                Terminal.Log($"Multiple parallel tasks, taking first");
#endif
                CurrentTask = TaskChain.tasks[0];

            }

        }

        protected class CarTrackPosition
        {
            public RailTrack track = null;
            public RailTrack trackPrev = null;
            public RailTrack trackNext = null;
            public TrainCar dvCar = null;
            public double span = 0.0;
            public bool changedTrack = false;
            public bool moving { get; private set; }

            public void UpdatePosition(Bogie bogie)
            {
                RailTrack trackCurrent = bogie.track;
                double spanCurrent = bogie.traveller.Span;

                if (track == null)
                {
                    track = trackCurrent;
                    span = spanCurrent;
                }
                else if (trackCurrent != track)
                {
                    trackNext = trackCurrent.GetNextFromPrev(track);
                    trackPrev = track;
                    track = trackCurrent;
                    span = spanCurrent;
                    changedTrack = true;
                }
                else
                {
                    changedTrack = false;

                    moving = dvCar.GetVelocity().sqrMagnitude > 0.02f;

                    if (moving)
                    {
                        double diff = spanCurrent - span;
                        if (diff < -Mathf.Epsilon)
                        {
                            trackNext = trackCurrent.GetInTrack();
                        }
                        else if (diff > Mathf.Epsilon)
                        {
                            trackNext = trackCurrent.GetOutTrack();
                        }

                        span = spanCurrent;
                    }
                }

            }

            public bool TrackDirection
            {
                get
                {
                    if (trackPrev == null)
                        return trackNext != track.GetInTrack();

                    return trackPrev == track.GetInTrack();
                }
            }

            public double PosOnTrack
            {
                get
                {
                    return TrackDirection ? span : track.logicTrack.length - span;
                }
            }

            public double Span { get => span; }

        }

        protected IEnumerator PositionUpdate()
        {
            Running = true;

            CarTrackPosition firstCarPosition = new CarTrackPosition();
            CarTrackPosition lastCarPosition = new CarTrackPosition();

            float lastTime = Time.time;

            while (Running)
            {
                try
                { 
                    if (!AppUtil.Instance.IsTimePaused)
                    {
                        ElapsedTime += Time.time - lastTime;
                    }

                    lastTime = Time.time;

                    if (PlayerManager.LastLoco != null && ! AppUtil.Instance.IsTimePaused)
                    {
                        TrainCar firstCar = Trainset.firstCar;
                        TrainCar lastCar = Trainset.lastCar;

                        (Bogie firstBoogie, Bogie lastBogie) = Utils.GetBogiesWithMaxDistance(firstCar, lastCar);

                        if (firstBoogie != null)
                        {
                            firstCarPosition.dvCar = firstCar;
                            firstCarPosition.UpdatePosition(firstBoogie);
                        }

                        if (lastBogie != null)
                        {
                            lastCarPosition.dvCar = lastCar;
                            lastCarPosition.UpdatePosition(lastBogie);
                        }

#if DEBUG2
                        Terminal.Log($"first track {firstCarPosition.track?.logicTrack.ID.FullID} next {firstCarPosition.trackNext?.logicTrack.ID.FullID} span {firstCarPosition.span}");
                        Terminal.Log($"last track {lastCarPosition.track?.logicTrack.ID.FullID} next {lastCarPosition.trackNext?.logicTrack.ID.FullID} span {lastCarPosition.span}");
#endif

                        UpdateCurrentTrack(firstCarPosition, lastCarPosition);
                    }
                }
                catch (Exception exc)
                {
                    Terminal.Log("PositionUpdate " + exc.Message + " " + exc.StackTrace);
                }

#if DEBUG
                yield return new WaitForSeconds(0.5f);
#else
                yield return new WaitForSeconds(0.1f);
#endif
            }

            Running = false;

#if DEBUG
            Terminal.Log("RouteTracker.PositionUpdate exit");
#endif
        }

        float lastAngle;
        double lastPos;

        protected void UpdateCurrentTrack(CarTrackPosition firstCar, CarTrackPosition lastCar)
        {
            var firstPathData = Route.GetNextTrack(firstCar.track, firstCar.trackPrev);
            var lastPathData = Route.GetNextTrack(lastCar.track, lastCar.trackPrev);

            if(firstPathData == null)
                firstPathData = Route.GetPrevTrack(firstCar.track, firstCar.trackNext);

            if (lastPathData == null)
                lastPathData = Route.GetPrevTrack(lastCar.track, lastCar.trackNext);

            Route.WalkPathData pathData = firstPathData;
            CarTrackPosition car = firstCar;

            double posFromStart = firstPathData != null ? firstPathData.distanceFromStart + firstCar.PosOnTrack : 0.0;

            if (lastPathData != null)
            {
                if (pathData == null || posFromStart < (lastPathData.distanceFromStart + lastCar.PosOnTrack))
                {
                    pathData = lastPathData;
                    car = lastCar;
                    posFromStart = lastPathData.distanceFromStart + lastCar.PosOnTrack;
                }
            }

            bool isOnTrack = pathData != null && (pathData.nextTrack == null || pathData.nextTrack == car.trackNext);

#if DEBUG
            if(!isOnTrack)
            {
                Terminal.Log($"firstPathData: {firstPathData?.prevTrack?.logicTrack.ID.FullID}->{firstPathData?.currentTrack.logicTrack.ID.FullID}->{firstPathData?.nextTrack?.logicTrack.ID.FullID}");
                Terminal.Log($"lastPathData: {lastPathData?.prevTrack?.logicTrack.ID.FullID}->{lastPathData?.currentTrack.logicTrack.ID.FullID}->{lastPathData?.nextTrack?.logicTrack.ID.FullID}");
                Terminal.Log($"firstCar {firstCar.dvCar.logicCar.ID}: {firstCar.trackPrev?.logicTrack.ID.FullID}->{firstCar.track?.logicTrack.ID.FullID}->{firstCar.trackNext?.logicTrack.ID.FullID}");
                Terminal.Log($"lastCar {lastCar.dvCar.logicCar.ID}: {lastCar.trackPrev?.logicTrack.ID.FullID}->{lastCar.track?.logicTrack.ID.FullID}->{lastCar.trackNext?.logicTrack.ID.FullID}");
            }
#endif
            if(isOnTrack)
            {
                DistanceTraveled = posFromStart;

                if( (posFromStart - Trainset.Length()) > notifyTrainEndPosition)
                {
                    notifyTrainEndPosition = double.MaxValue;
                    Module.PlayClip(Module.trainEnd);
                }

            }

#if DEBUG
            float carSpeed = Mathf.Abs(car.dvCar.GetForwardSpeed());
            float metersAhead = carSpeed * carSpeed + 50.0f;
            var (aheadTrack, aheadSpan, direction) = car.track.GetAheadTrack(car.span, car.TrackDirection, metersAhead);
            float span = (float)Mathd.Clamp01(aheadSpan / aheadTrack.logicTrack.length);
            Vector3 tan = aheadTrack.curve.GetTangentAt(span );
            if (direction)
                tan = -tan;
            Vector2 tand = new Vector2(tan.x, tan.z);
            float angle = Mathf.Atan2(tand.y, tand.x);




            //Terminal.Log($"isOnTrack: {isOnTrack} firstCar: {car == firstCar} trackDistance: {pathData?.distanceFromStart} posFromstart: {DistanceTraveled} posToFinish: {DistanceToFinish} carTrack: {car?.track?.logicTrack.ID.FullID} carNext: {car?.trackNext?.logicTrack.ID.FullID} carMoving: {car?.moving} carVelocity: {car?.dvCar.GetVelocity().sqrMagnitude} carChangedTrack: {car?.changedTrack}  tan: {tan}");
            float angleDiff = Utils.GetAngleDifference(lastAngle, angle);
            RecommendedSpeed = RailTrackExtension.AngleDiffToSpeed(angleDiff, (float) (posFromStart - lastPos));
            angleDiff = angleDiff * 180f / Mathf.PI / Mathf.Abs((float)(posFromStart - lastPos));

            Terminal.Log($"isOnTrack: {isOnTrack} car: {car.dvCar.ID} posFromstart: {DistanceTraveled} posToFinish: {DistanceToFinish} carTrack: {car?.track?.logicTrack.ID.FullID} carNext: {car?.trackNext?.logicTrack.ID.FullID} angle: {Mathf.Atan2(tand.y, tand.x)} ahead {aheadTrack.logicTrack.ID.FullID} aheadSpan {aheadSpan} aheadDir {direction} carSpan {car.span} carDir {car.TrackDirection} angleDiff {angleDiff} {RecommendedSpeed}");
            lastAngle = angle;
            lastPos = posFromStart;
#endif



            if (TrackState == TrackingState.BeforeStart)
            {
                if ( ! car.moving && (firstCar.track == Route.FirstTrack || lastCar.track == Route.FirstTrack))
                {
                    TrackState = TrackingState.OnStart;
                    Terminal.Log("Task tracking started");
                }
                else if (car.moving && isOnTrack)
                {
                    TrackState = TrackingState.RightHeading;
                }
                
            }
            else if (TrackState == TrackingState.OnStart)
            {
                if (car.moving)
                {
                    if (isOnTrack)
                    {
                        TrackState = TrackingState.RightHeading;
                    }
                    else if (car.track == Route.FirstTrack)
                    {
                        if (car.trackNext == Route.SecondTrack)
                        {
                            TrackState = TrackingState.RightHeading;
                        }
                        else
                        {
                            TrackState = TrackingState.WrongHeading;
                        }
                    }
                    else
                    {
                        TrackState = TrackingState.OutOfWay;
                    }
                }
            }
            else if (TrackState == TrackingState.RightHeading)
            {
                if (firstCar.track.logicTrack == CurrentTask.DestinationTrack || lastCar.track.logicTrack == CurrentTask.DestinationTrack)
                {
                    if ((firstCar.track.logicTrack == CurrentTask.DestinationTrack && lastCar.track.logicTrack == CurrentTask.DestinationTrack)
                        || CurrentTask.DestinationTrack.IsOccupiedAtLeast(0.8f)
                        || ! CurrentTask.DestinationTrack.IsFree(Trainset))
                    {
                        OnTaskFinished();
                    }
                }
                else if (car.changedTrack && pathData != null)
                {
                    Junction junction = null;

                    if(/*DistanceToFinish < 1000.0f
                        &&*/ ((Route.Reverses.Count % 2 == 0 && car.dvCar.IsLoco) || (Route.Reverses.Count % 2 == 1 && ! car.dvCar.IsLoco))
                        && !Route.Destination.IsFree()
                    )
                    {
                        Terminal.Log($"Should approach reversed!!!");
                    }

                    Terminal.Log($"junctionId {pathData.junctionId}");
                    if (Route.Reverses.TryGetValue(pathData.junctionId, out junction))
                    {
                        TrackState = TrackingState.StopTrainAfterSwitch;
                        Module.StartCoroutine(SwitchJunctionAfterIsFree(junction));
                        Route.Reverses.Remove(pathData.junctionId);
                    }
                }
                else if (!isOnTrack)
                {
                    TrackState = Route.Path.Contains(car.track) ? TrackingState.WrongHeading : TrackingState.OutOfWay;
                }
            }
            else if (TrackState == TrackingState.OutOfWay || TrackState == TrackingState.WrongHeading)
            {
                if (isOnTrack)
                {
                    TrackState = TrackingState.RightHeading;
                    Terminal.Log("Back on track");
                }
            }
            else if (TrackState == TrackingState.ReverseTrain)
            {
                if (isOnTrack && car.moving)
                {
                    TrackState = TrackingState.RightHeading;
                }
            }
        }

        
        /*private Trainset GetJobTrainset()
        {
            if(Job != null)
            {
                //TODO check if all cars from job are in one trainset
                return CurrentTask.GetTaskData().cars.GroupBy(c => c.)

            }

            PlayerManager.LastLoco?.trainset
        }*/


        private IEnumerator SwitchJunctionAfterIsFree(Junction junction)
        {

            while (true)
            {
#if DEBUG2
                Terminal.Log($"In: {junction.inBranch.track.onTrackBogies.Count} {junction.inBranch.track.onTrackBogies.Count}");
                junction.outBranches.ForEach(b => Terminal.Log($"Out: {b.track.logicTrack?.GetCarsPartiallyOnTrack().Count}"));
#endif
                if (junction.IsFree())
                    break;

                yield return new WaitForSeconds(0.2f);
            }

            junction.Switch(Junction.SwitchMode.REGULAR);

            TrackState = TrackingState.ReverseTrain;

            /*while(PlayerManager.LastLoco == null || PlayerManager.LastLoco.trainset   )
            {
                yield return new WaitForSeconds(0.2f);
            }*/

            //wait for reversing
        }

        private IEnumerator WaitForTaskCompletition(Task task)
        {
            while ( ! task.IsTaskCompleted())
                yield return new WaitForSeconds(0.5f);

            Terminal.Log("Task finished");

            TaskChain.tasks.Remove(CurrentTask);

            TakeTask();

            if(CurrentTask == null)
            {
                Module.ActiveRoute.ClearRoute(); //TODO - remove Module dependency
            }
            else
            {
                var args = new CommandArg[]
                    {
                        new CommandArg() { String = "tracker" }
                    };
                var commandtask = RouteCommand.DoCommand(args);
                commandtask.Start();
            }

        }

        protected void OnTaskFinished()
        {
            TrackState = TrackingState.OnFinish;
            Terminal.Log("OnTaskFinished");

            if(CurrentTask.DVTask != null)
            {
                Module.StartCoroutine(WaitForTaskCompletition(CurrentTask.DVTask));
                return;
            }

            TaskChain.tasks.Remove(CurrentTask);

            TakeTask();

            if (CurrentTask == null)
            {
                Dispose();
            }
        }

        public void NotifyTrainEnd()
        {
            notifyTrainEndPosition = DistanceTraveled;

            if(audio)
            {
                Module.PlayClip(Module.setClip);
            }
            Terminal.Log($"notifyEnd: {DistanceTraveled}");
        }

        public void Dispose()
        {
#if DEBUG
            Terminal.Log("RouteTracker.Dispose");
#endif
            Running = false;
        }
    }
}
