using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager
{
    public class RouteTracker
    {
        public enum TrackingState
        {
            OnStart,
            RightHeading,
            WrongHeading,
            OutOfWay,
            NeedReverse, //Is it same as WrongHeading?
            OnFinish

        }

        private Route Route { get; set;}
        private RailTrack currentTrack;

        public void UpdateCurrentTrack(RailTrack currentTrack, RailTrack nextTrack)
        {
            if (this.currentTrack == currentTrack)
                return;


        }
    }
}
