using CommandTerminal;
using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public static bool IsSectorFree(this RailTrack current, double sectorLength, bool fromOutConnection )
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


    }

}
