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
    public class SelectTownPage : CRMSelectorSubPage
    {
        private string[] townCodesArray;

        public SelectTownPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        protected override List<MenuItem> CreateMenuItems()
        {
            townCodesArray = RailTrackRegistry.AllTracks.Select(p => p.logicTrack.ID.FullID)
                .Where(s => !s.StartsWith(SelectTrackPage.GENERAL_TRACK_PREFIX))
                .Select(s => s.GetUntilOrEmpty(SelectTrackPage.TRACK_PARTS_SEPARATOR))
                .Distinct()
                .OrderBy(s => s)
                .ToArray();

            if (townCodesArray.Length == 0)
            {
                Terminal.Log("No town found!");
            }


            List<string> townNames = new List<string>();

            foreach (var townCode in townCodesArray)
            {
                switch (townCode)
                {
                    case "HB":
                        townNames.Add("Harbor and town"); break;
                    case "GF":
                        townNames.Add("Goods factory and town"); break;
                    case "FF":
                        townNames.Add("Foods factory and town"); break;
                    case "OWN":
                        townNames.Add("Oil well north"); break;
                    case "OWC":
                        townNames.Add("Oil well central"); break;
                    case "CM":
                        townNames.Add("Coal mine"); break;
                    case "SM":
                        townNames.Add("Steel mill"); break;
                    case "CSW":
                        townNames.Add("City SW"); break;
                    case "IME":
                        townNames.Add("Iron ore mine east"); break;
                    case "IMW":
                        townNames.Add("Iron ore mine west"); break;
                    case "FRC":
                        townNames.Add("Forest central"); break;
                    case "FRS":
                        townNames.Add("Forest south"); break;
                    case "FM":
                        townNames.Add("Farm"); break;
                    case "MF":
                        townNames.Add("Machine factory and town"); break;
                    case "MB":
                        townNames.Add("Military base"); break;
                    case "SW":
                        townNames.Add("Sawmill"); break;
                    case "HMB":
                        townNames.Add("Harbor military base"); break;
                    case "MFMB":
                        townNames.Add("Machine factory military base"); break;
                    default:
                        townNames.Add(townCode); break;
                }
            }

            return townNames.Select(s => new MenuItem(s, null)).ToList();
        }

        public override void OnAction()
        {
            Return();
        }

        public string SelectedTownCode { get => townCodesArray[menuSelector.Index]; }
    }
}
