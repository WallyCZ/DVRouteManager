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
    public class SelectStationPage : CRMSelectorSubPage
    {
        private string selectedTownCode;

        public SelectStationPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        protected override List<MenuItem> CreateMenuItems()
        {
            var stationList = RailTrackRegistry.AllTracks.Select(p => p.logicTrack.ID.FullID)
                .Where(s => s.StartsWith(selectedTownCode + SelectTrackPage.TRACK_PARTS_SEPARATOR))
                .Select(s => s.GetAfterOrEmpty(SelectTrackPage.TRACK_PARTS_SEPARATOR).GetUntilOrEmpty(SelectTrackPage.TRACK_PARTS_SEPARATOR))
                .Distinct()
                .OrderBy(s => s)
                .Select(s => new MenuItem(s, null))
                .ToList();

            if (stationList.Count == 0)
            {
                Terminal.Log("No station found in town " + selectedTownCode);
            }


            return stationList;
        }

        public override void OnAction()
        {
            Return();
        }

        public override void OnEnter(CRMPage previousPage, CRMPageArgs args)
        {
            SelectTownPage townPage = previousPage as SelectTownPage;
            selectedTownCode = townPage.SelectedTownCode;
            SetupSelector();
            base.OnEnter(previousPage, args);
        }

        public string SelectedTownStationCode { get => (selectedTownCode + SelectTrackPage.TRACK_PARTS_SEPARATOR + menuSelector.Current.displayText); }
    }
}
