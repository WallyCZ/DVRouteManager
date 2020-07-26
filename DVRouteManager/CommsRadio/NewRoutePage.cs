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
using UnityEngine.Networking;

namespace DVRouteManager.CommsRadio
{
    public class NewRoutePage : CRMSelectorPage
    {
        bool fromLoco = true;
        public NewRoutePage(ICRMPageManager manager) :
            base(manager)
        {
        }

        protected override List<MenuItem> CreateMenuItems()
        {
            return new List<MenuItem>()
            {
                new MenuItem("From last used locomotion\nto job destination", "Build route", () => CreateJobRoute(true)),
                new MenuItem("From last used locomotion\nto specific track", "Select", () => CreateTrackRoute()),
                new MenuItem("From job cars\nto job destination", "Build route", () => CreateJobRoute(false)),
                GetExitMenu()
            };
        }

        private void CreateTrackRoute()
        {
            SetSubPage(typeof(SelectTownStationTrackPage), null, (lastPage) =>
            {
                SelectTrackPage trackPage = lastPage as SelectTrackPage;

                Terminal.Log($"Selected track {trackPage.SelectedTrack}");
                CommandArg[] args = new CommandArg[]
                {
                            new CommandArg() { String = "from" },
                            new CommandArg() { String = "loco" },
                            new CommandArg() { String = "to" },
                            new CommandArg() { String = trackPage.SelectedTrack }
                };
                BuildRoute(args, this);
            });
        }

        private void CreateJobRoute(bool fromLoco)
        {
            this.fromLoco = fromLoco;

            List<JobBooklet> allJobBooklets = new List<JobBooklet>(JobBooklet.allExistingJobBooklets);

            if (allJobBooklets.Count == 0)
            {
                RedirectToMessagePage("No job", "Confirm");
                return;
            }

            if (allJobBooklets.Count > 1)
            {
                SetSubPage(typeof(SelectJobPage), null, (lastPage) =>
                {
                    SelectJobPage jobPage = lastPage as SelectJobPage;
                    if (jobPage != null)
                    {
                        UseJob(jobPage.SelectedJobName);
                    }
                });
            }
            else
            {
                UseJob(allJobBooklets[0].job.ID);
            }
        }

        public void UseJob(string jobName)
        {
            CommandArg[] args;
            if (fromLoco)
            {
                args = new CommandArg[]
                    {
                    new CommandArg() { String = "loco" },
                    new CommandArg() { String = jobName }
                    };
            }
            else
            {
                args = new CommandArg[]
                    {
                    new CommandArg() { String = "job" },
                    new CommandArg() { String = jobName }
                    };
            }

            BuildRoute(args, this);
        }

        public static async void BuildRoute(CommandArg[] args, CRMPage page)
        {
            try
            {
                page.DisplayText("Computing route", "");

                await RouteCommand.DoCommand(args);

                if (Module.ActiveRoute.IsSet)
                {
                    StringBuilder via = Module.ActiveRoute.Route.Path.Select(p => p.logicTrack.ID.FullDisplayID)
                        .Where(s => !s.StartsWith(SelectTrackPage.GENERAL_TRACK_PREFIX))
                        .Select(s => s.GetUntilOrEmpty(SelectTrackPage.TRACK_PARTS_SEPARATOR))
                        .Distinct()
                        .Aggregate(new StringBuilder(), (current, next) => current.Append(current.Length == 0 ? "" : ", ").Append(next));

                    string routeInfo = $"Route {(Module.ActiveRoute.Route.Length / 1000.0):0.#}km\nHeading: {Module.ActiveRoute.Route.StartHeading}\nvia: {via}";

                    if (Module.ActiveRoute.Route.Reverses.Count > 0)
                        routeInfo += $"\nReverses: {Module.ActiveRoute.Route.Reverses.Count}";

                    page.RedirectToMessagePage(routeInfo, "MENU");
                }
                else
                {
                    page.RedirectToMessagePage("Route not found", "MENU");
                }
            }
            catch (CommandException exc)
            {
                page.RedirectToMessagePage(exc.Message, "MENU");
            }
            catch (Exception exc)
            {
                Terminal.Log(exc.Message + ": " + exc.StackTrace);
                page.RedirectToMessagePage("Error in building path, see console", "MENU");
            }
        }
    }
}
