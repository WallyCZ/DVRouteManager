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
    public class LocoAIPage : CRMSelectorPage
    {
        public LocoAIPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        protected override List<MenuItem> CreateMenuItems()
        {
            return new List<MenuItem>()
            {
                new MenuItem("Shunt to specific track", "Select", () => CreateTrackRoute()),
                new MenuItem("Stop Loco AI", "Stop", () => Stop()),
                GetExitMenu()
            };
        }

        private void CreateTrackRoute()
        {
            SetSubPage(typeof(SelectTownStationTrackPage), null, async (lastPage) =>
            {
                SelectTrackPage trackPage = lastPage as SelectTrackPage;

                Terminal.Log($"Selected track {trackPage.SelectedTrack}");
                CommandArg[] args = new CommandArg[]
                {
                    new CommandArg() { String = "auto" },
                    new CommandArg() { String = trackPage.SelectedTrack }
                };

                try
                {
                    await RouteCommand.DoCommand(args);

                    DisplayText($"Going to {trackPage.SelectedTrack}", "");

                    await new WaitForSeconds(1.0f);

                    DisplayText(menuSelector.Current.displayText, menuSelector.Current.actionName);
                }
                catch (CommandException exc)
                {
                    RedirectToMessagePage(exc.Message, "MENU");
                }
                catch (Exception exc)
                {
                    Terminal.Log(exc.Message + ": " + exc.StackTrace);
                    RedirectToMessagePage("Error LocoAI, see console", "MENU");
                }
            });
        }

        private async void Stop()
        {
            CommandArg[] args = new CommandArg[]
            {
                    new CommandArg() { String = "auto" },
                    new CommandArg() { String = "stop" }
            };

            try
            {
                await RouteCommand.DoCommand(args);

                CallMessageSubPage("Loco AI stopped", "", 1.0f);
            }
            catch (CommandException exc)
            {
                RedirectToMessagePage(exc.Message, "MENU");
            }
            catch (Exception exc)
            {
                Terminal.Log(exc.Message + ": " + exc.StackTrace);
                RedirectToMessagePage("Error LocoAI, see console", "MENU");
            }

        }
    }
}
