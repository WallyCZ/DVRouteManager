using CommandTerminal;
using DV;
using DVRouteManager.Internals;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityAsync;
using UnityEngine.Networking;

namespace DVRouteManager.CommsRadio
{
    public class RouteInfoPage : CRMSelectorPage
    {
        private bool routeTrackerRunning = false;
        public RouteInfoPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        protected override List<MenuItem> CreateMenuItems()
        {
            return new List<MenuItem>()
            {
                new MenuItem("Route tracker", "Show", () => ShowRouteTrackerInfo()),
                new MenuItem("Train end alarm", "Set", () => NotifyTrainEnd()),
                new MenuItem("Clear route", "Clear", () => ClearRoute()),
                GetExitMenu()
            };
        }

        private async void ShowRouteTrackerInfo()
        {
            routeTrackerRunning = true;

            while (routeTrackerRunning)
            {
                if (!Module.ActiveRoute.IsSet)
                {
                    routeTrackerRunning = false;
                    DisplayText(menuSelector.Current.displayText, menuSelector.Current.actionName);
                    break;
                }

                switch (Module.ActiveRoute.RouteTracker.TrackState)
                {
                    case RouteTracker.TrackingState.RightHeading:
                        {
                            string routeInfo = $"Right heading\nRemaining: {(Module.ActiveRoute.RouteTracker.DistanceToFinish / 1000.0):0.#}km";
                            DisplayText(routeInfo);
                        }
                        break;
                    case RouteTracker.TrackingState.WrongHeading:
                        {
                            DisplayText("Wrong heading");
                        }
                        break;
                    case RouteTracker.TrackingState.StopTrainAfterSwitch:
                        {
                            DisplayText("Stop train behind switch");
                        }
                        break;
                    case RouteTracker.TrackingState.ReverseTrain:
                        {
                            DisplayText("Reverse train");
                        }
                        break;
                    case RouteTracker.TrackingState.BeforeStart:
                        {
                            DisplayText("Go to start position");
                        }
                        break;
                    case RouteTracker.TrackingState.OnStart:
                        {
                            DisplayText("On start");
                        }
                        break;
                    case RouteTracker.TrackingState.OnFinish:
                        {
                            DisplayText("Finished");
                        }
                        break;
                    case RouteTracker.TrackingState.OutOfWay:
                        {
                            DisplayText("Out of way");
                        }
                        break;
                    default:
                        {
                            DisplayText("Unknown state");
                        }
                        break;
                }


                await new WaitForSeconds(1.0f);
            }
        }

        public override bool ButtonACustomAction()
        {
            routeTrackerRunning = false;
            return base.ButtonACustomAction();
        }
        public override bool ButtonBCustomAction()
        {
            routeTrackerRunning = false;
            return base.ButtonBCustomAction();
        }

        public override void OnLeave()
        {
            base.OnLeave();
            routeTrackerRunning = false;
        }

        private void NotifyTrainEnd()
        {
            Module.ActiveRoute.RouteTracker.NotifyTrainEnd();
        }

        public override void OnAction()
        {
            if (routeTrackerRunning)
            {
                routeTrackerRunning = false;
                DisplayText(menuSelector.Current.displayText, menuSelector.Current.actionName);
            }
            else
            {
                base.OnAction();
            }
        }

        public void ClearRoute()
        {
            Module.ActiveRoute.ClearRoute();
            RedirectToMessagePage("Route cleared", "MENU");
        }

    }
}
