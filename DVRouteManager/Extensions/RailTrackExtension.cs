using CommandTerminal;
using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace DVRouteManager
{
    public static class RailTrackExtension
    {
        /// <summary>
        /// If we can go through junctoin without reveresing
        /// </summary>
        /// <param name="current"></param>
        /// <param name="junction"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        private static bool CanGoThroughJunctionDirectly(this RailTrack current, Junction junction, RailTrack from, RailTrack to)
        {
            bool fromIsOutBranch = junction != null && junction.outBranches.Any(b => b.track == from);

            if (fromIsOutBranch)
            {
                //Terminal.Log($"{from?.logicTrack.ID.FullID} -> {to?.logicTrack.ID.FullID} fromIsOutBranch");
                return false;
            }

            bool currentIsOutBranch = junction != null && junction.outBranches.Any(b => b.track == current);

            if (currentIsOutBranch)
            {
                //Terminal.Log($"{from?.logicTrack.ID.FullID} -> {to?.logicTrack.ID.FullID} currentIsOutBranch {junction.inBranch.track.logicTrack.ID.FullID}");
                return junction.inBranch.track == to;
            }

            return true;
        }
        public static bool CanGoToDirectly(this RailTrack current, RailTrack from, RailTrack to)
        {
            Junction reversingJunction;
            return CanGoToDirectly(current, from, to, out reversingJunction);
        }

        /// <summary>
        /// Computes if track without revresing is long enough for given length
        /// </summary>
        /// <param name="current"></param>
        /// <param name="from"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static bool IsDirectLengthEnough(this RailTrack current, RailTrack from, double length)
        {
            if (current.logicTrack.length > length)
                return true;

            bool isFromInJuction = current.inIsConnected && current.GetAllInBranches().Any(b => b.track == from);
            bool isFromOutJuction = current.outIsConnected && current.GetAllOutBranches().Any(b => b.track == from);

            if (current.inIsConnected && !isFromInJuction)
            {
                foreach (var branch in current.GetAllInBranches())
                {
                    if (CanGoToDirectly(current, from, branch.track) && IsDirectLengthEnough(branch.track, current, length - current.logicTrack.length))
                        return true;
                }
            }

            if (current.outIsConnected && !isFromOutJuction)
            {
                foreach (var branch in current.GetAllOutBranches())
                {
                    if (CanGoToDirectly(current, from, branch.track) && IsDirectLengthEnough(branch.track, current, length - current.logicTrack.length))
                        return true;
                }
            }

            return false;
        }

        public static bool CanGoToDirectly(this RailTrack current, RailTrack from, RailTrack to, out Junction reversingJunction)
        {
            reversingJunction = null;

            bool isInJuction = current.inIsConnected && current.GetAllInBranches().Any(b => b.track == to);
            bool isOutJuction = current.outIsConnected && current.GetAllOutBranches().Any(b => b.track == to);

            if (current.inIsConnected)
            {

                //Terminal.Log($"IN: {from?.logicTrack.ID.FullID} -> {current.logicTrack.ID.FullID} -> {to?.logicTrack.ID.FullID}");
                if (isInJuction && CanGoThroughJunctionDirectly(current, current.inJunction, from, to))
                    return true;
            }

            if (current.outIsConnected)
            {
                //Terminal.Log($"OUT: {from?.logicTrack.ID.FullID} -> {current.logicTrack.ID.FullID} -> {to?.logicTrack.ID.FullID}");
                if (isOutJuction && CanGoThroughJunctionDirectly(current, current.outJunction, from, to))
                    return true;

            }

            if (isInJuction)
            {
                reversingJunction = current.inJunction;
            }

            if (isOutJuction)
            {
                reversingJunction = current.outJunction;
            }

            return false;
        }


        public static bool IsTrackInBranch(this RailTrack current, RailTrack next)
        {
            if (!current.inIsConnected)
                return false;

            return current.GetAllInBranches().Any(b => b.track == next);
        }

        public static bool IsTrackOutBranch(this RailTrack current, RailTrack next)
        {
            if (!current.outIsConnected)
                return false;

            return current.GetAllOutBranches().Any(b => b.track == next);
        }

        public static bool IsFree(this Track current, ISet<string> carsToIgnore)
        {
            if (current.IsFree())
                return true;

            return current.GetCarsFullyOnTrack().All(c => carsToIgnore.Contains(c.ID));
        }

        public static bool IsFree(this Track current, Trainset trainset)
        {
            return IsFree(current, new HashSet<string>(trainset.cars.Select(c => c.logicCar.ID)));
        }

        public static RailTrack GetNextTrack(this RailTrack current, bool direction)
        {
            return direction ? GetOutTrack(current) : GetInTrack(current);
        }

        public static bool GetDirectionFromPrev(this RailTrack current, RailTrack prev)
        {
            if (IsTrackOutBranch(current, prev))
            {
                return false;
            }

            return true;
        }

        public static RailTrack GetOutTrack(this RailTrack current)
        {
            if (current.outJunction != null)
            {
                if (current.outJunction.inBranch.track == current)
                {
                    return current.outJunction.outBranches[current.outJunction.selectedBranch].track;
                }
                else
                {
                    return current.outJunction.inBranch.track;
                }
            }
            else
            {
                return current.outBranch.track;
            }
        }
        public static RailTrack GetInTrack(this RailTrack current)
        {
            if (current.inJunction != null)
            {
                if (current.inJunction.inBranch.track == current)
                {
                    return current.inJunction.outBranches[current.inJunction.selectedBranch].track;
                }
                else
                {
                    return current.inJunction.inBranch.track;
                }
            }
            else
            {
                return current.inBranch.track;
            }
        }

        public static RailTrack GetNextFromPrev(this RailTrack current, RailTrack prev)
        {
            if (IsTrackInBranch(current, prev))
            {
                return GetOutTrack(current);
            }

            if (IsTrackOutBranch(current, prev))
            {
                return GetInTrack(current);
            }

            return null;
        }

        public static bool IsSectorFreeFromJunction(this RailTrack current, double sectorLength, Junction junction)
        {
            if (current.inJunction == junction)
                return IsSectorFree(current, sectorLength, false);
            else if (current.outJunction == junction)
                return IsSectorFree(current, sectorLength, true);

#if DEBUG
            Terminal.Log($"IsSectorFreeFromJunction: not connected to given junction... track {current.logicTrack.ID.FullID} junction {junction?.GetInstanceID()} inJunction {current.inJunction?.GetInstanceID()} outJunction {current.outJunction?.GetInstanceID()}");
#endif
            return true;
        }
        public static bool IsSectorFree(this RailTrack current, double sectorLength, bool fromOutConnection)
        {

            if (fromOutConnection)
            {
#if DEBUG2
                current.onTrackBogies.ToList().ForEach(b => Terminal.Log($"SpanOut: {b.traveller.Span}"));
#endif
                return current.onTrackBogies.All(b => b.traveller.Span < (current.logicTrack.length - sectorLength));
            }
            else
            {
#if DEBUG2
                current.onTrackBogies.ToList().ForEach(b => Terminal.Log($"SpanIn: {b.traveller.Span}"));
#endif
                return current.onTrackBogies.All(b => b.traveller.Span > sectorLength);
            }
        }


        /// <summary>
        /// Checks if track is occupied at least as ratio
        /// </summary>
        /// <param name="current"></param>
        /// <param name="ration"></param> 0.0 - 1.0
        /// <returns></returns>
        public static bool IsOccupiedAtLeast(this Track current, float ratio)
        {
            return (current.OccupiedLength / current.length) > ratio;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="current"></param>
        /// <returns>m/s</returns>
        public static float GetAverageSpeed(this RailTrack current)
        {
#if DEBUG
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var watchGetTangent = new System.Diagnostics.Stopwatch();
#endif

            const float step = 25f;
            float trackPos = 0.0f;

            int num = -1;

            float speedSum = 0;

            float realSpeed = 0;

            float trackLength = (float)current.logicTrack.length;

            float lastAngle = 0;

            float curveTotalDistance = 0.0f;

            BezierPoint[] points = current.curve.GetAnchorPoints();

            BezierPoint p1 = points[0];
            BezierPoint p2 = points[1];
            float curveBlockLength = BezierCurve.ApproximateLength(p1, p2, 10);
            int curvePointIndex = 1;

            if (current.curve.close)
            {
                Terminal.Log($"Closed curve not supported yet!");
            }

            while (trackPos < trackLength)
            {
                float localDistance = Mathf.InverseLerp(0.0f, trackLength, trackPos);

                while (trackPos > (curveTotalDistance + curveBlockLength))
                {
                    curveTotalDistance += curveBlockLength;
                    p1 = points[curvePointIndex];
                    p2 = points[curvePointIndex + 1];
                    curvePointIndex++;
                    curveBlockLength = BezierCurve.ApproximateLength(p1, p2, 10);
                }

#if DEBUG
                watchGetTangent.Start();
#endif
                float localCurveDistance = Mathf.InverseLerp(curveTotalDistance, curveTotalDistance + curveBlockLength, trackPos);

                //Vector3 tangent2 = current.curve.GetTangentAt(localDistance); // Slow as f*ck
                Vector3 tangent = current.curve.GetTangent(p1, p2, localCurveDistance);

                //Terminal.Log($"tan1 {tangent2} tan2 {tangent} local {localCurveDistance} total {curveTotalDistance} block {curveBlockLength} pos {trackPos}");

#if DEBUG
                watchGetTangent.Stop();
#endif
                float angle = Mathf.Atan2(tangent.z, tangent.x);

                if (num >= 0)
                {
                    float maxSpeed = AngleDiffToSpeed(Utils.GetAngleDifference(lastAngle, angle), step);

                    if (num == 0)
                    {
                        realSpeed = maxSpeed;
                    }
                    else
                    {
                        if (maxSpeed > realSpeed)
                        {
                            if (realSpeed < 1.0f)
                                realSpeed = 1.0f;

                            realSpeed += step / realSpeed;
                        }
                        else
                        {
                            realSpeed = maxSpeed;
                        }
                    }

                    speedSum += realSpeed;
                }

                num++;

                trackPos += step;
                lastAngle = angle;
            }

            if (num <= 0)
            {
                //if track segment is too short, take two end points
                Vector3 tangent1 = current.curve.GetTangentAt(0.0f);
                Vector3 tangent2 = current.curve.GetTangentAt(1.0f);

                float angle1 = Mathf.Atan2(tangent1.z, tangent1.x);
                float angle2 = Mathf.Atan2(tangent2.z, tangent2.x);

#if DEBUG2
                watch.Stop();
                //Terminal.Log($"num {num} {watch.ElapsedMilliseconds}ms");
#endif
                return AngleDiffToSpeed(Utils.GetAngleDifference(angle1, angle2), trackLength);
            }
#if DEBUG2
            watch.Stop();
            if (num > 100)
            {
                Terminal.Log($"num {num} all {watch.ElapsedMilliseconds}ms tangent {watchGetTangent.ElapsedMilliseconds}");
            }
#endif
            return (speedSum / num) * 3.6f; // km/h -> m/s
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="angleDiff"></param>
        /// <param name="step"></param>
        /// <returns>km/h</returns>
        public static float AngleDiffToSpeed(float angleDiff, float step)
        {
            angleDiff = angleDiff * 180f / Mathf.PI / step;


            if (angleDiff < 0.01f)
            {
                return 120.0f;
            }
            if (angleDiff < 0.08f)
            {
                return 100.0f;
            }
            if (angleDiff < 0.14f)
            {
                return 90.0f;
            }
            if (angleDiff < 0.19f)
            {
                return 80.0f;
            }
            if (angleDiff < 0.28f)
            {
                return 70.0f;
            }
            if (angleDiff < 0.40f)
            {
                return 60.0f;
            }
            if (angleDiff < 0.48f)
            {
                return 50.0f;
            }
            if (angleDiff < 0.63f)
            {
                return 40.0f;
            }
            if (angleDiff < 0.80f)
            {
                return 30.0f;
            }
            if (angleDiff < 1.40f)
            {
                return 20.0f;
            }

            return 10.0f;
        }

        public static (RailTrack, double, bool) GetAheadTrack(this RailTrack current, double currentCarSpan, bool direction, double aheadDistance)
        {
            aheadDistance -= direction ? current.logicTrack.length - currentCarSpan : currentCarSpan;

            while(aheadDistance >= 0.0f)
            {
                RailTrack nextTrack = current.GetNextTrack(direction);

                if(nextTrack == null)
                {
                    break;
                }

                direction = nextTrack.GetDirectionFromPrev(current);

                current = nextTrack;

                aheadDistance -= current.logicTrack.length;
            }

            double span = direction ? current.logicTrack.length + aheadDistance : -aheadDistance;
            if(span > current.logicTrack.length)
            {
                span = current.logicTrack.length;
            }

            return (current, span, direction);
        }
    }

}
