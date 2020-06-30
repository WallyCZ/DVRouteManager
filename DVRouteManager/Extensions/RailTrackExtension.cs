using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager
{
    public static class RailTrackExtension
    {

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
        public static bool CanGoToDirectly(this RailTrack current, RailTrack from, RailTrack to, out Junction reversingJunction)
        {
            reversingJunction = null;

            bool isInJuction = current.inIsConnected && current.GetAllInBranches().Any(b => b.track == to);
            bool isOutJuction = current.outIsConnected && current.GetAllOutBranches().Any(b => b.track == to);

            if (current.outIsConnected)
            {
                //Terminal.Log($"OUT: {from?.logicTrack.ID.FullID} -> {current.logicTrack.ID.FullID} -> {to?.logicTrack.ID.FullID}");
                if (isOutJuction && CanGoThroughJunctionDirectly(current, current.outJunction, from, to))
                    return true;

            }

            if (current.inIsConnected)
            {

                //Terminal.Log($"IN: {from?.logicTrack.ID.FullID} -> {current.logicTrack.ID.FullID} -> {to?.logicTrack.ID.FullID}");
                if (isInJuction && CanGoThroughJunctionDirectly(current, current.inJunction, from, to))
                    return true;
            }

            if(isOutJuction)
            {
                reversingJunction = current.outJunction;
            }

            if (isInJuction)
            {
                reversingJunction = current.inJunction;
            }

            return false;
        }

       
        public static bool IsTrackInBranch(this RailTrack current, RailTrack next)
        {
            if (!current.inIsConnected)
                return false;

            return current.GetAllInBranches().Any(b => b.track == next);
        }

        public static bool IsFree(this RailTrack current, HashSet<string> carsToIgnore)
        {
            if (current.logicTrack.IsFree())
                return true;

            return current.logicTrack.GetCarsFullyOnTrack().All(c => carsToIgnore.Contains(c.ID));
        }
    }

}
