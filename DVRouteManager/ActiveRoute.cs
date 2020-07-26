using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRouteManager
{
    public class ActiveRoute
    {
        private Route route;
        private RouteTracker routeTracker;

        public bool IsSet { get => Route != null; }

        public Route Route
        {
            get => route;
            set
            {
                route = value;
                if (route != null)
                {
                    PathMapMarker.DrawPathToMap(route);
                }
                else
                {
                    ClearRoute();
                }

            }
        }

        public void ClearRoute()
        {
            route = null;
            RouteTracker = null;
            PathMapMarker.DestroyAllPoints();
        }

        public RouteTracker RouteTracker
        {
            get => routeTracker;
            set
            {
                if (routeTracker != null)
                {
                    routeTracker.Dispose();
                }

                routeTracker = value;
            }
        }

        public PathMapMarkers PathMapMarker { get; } = new PathMapMarkers();


    }
}
