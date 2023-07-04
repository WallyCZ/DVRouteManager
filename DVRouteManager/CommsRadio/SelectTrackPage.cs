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
    public class SelectTrackPage : CRMSelectorSubPage
    {
        public const string GENERAL_TRACK_PREFIX = "#Y-";
        public const string TRACK_PARTS_SEPARATOR = "-";

        private string selectedTownStationCode;

        public SelectTrackPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        protected override List<MenuItem> CreateMenuItems()
        {
            var trackList = RailTrackRegistry.Instance.AllTracks.Select(p => p.logicTrack.ID.FullID)
                .Where(s => s.StartsWith(selectedTownStationCode + TRACK_PARTS_SEPARATOR))
                .Select(s => s.GetAfterOrEmpty(TRACK_PARTS_SEPARATOR).GetAfterOrEmpty(TRACK_PARTS_SEPARATOR))
                .OrderBy(s => s)
                .Select(s => new MenuItem(s, null))
                .ToList();

            if (trackList.Count == 0)
            {
                Terminal.Log("No track found in town " + selectedTownStationCode);
            }


            return trackList;
        }

        public override void OnAction()
        {
            Return();
        }

        public override void OnEnter(CRMPage previousPage, CRMPageArgs args)
        {
            SelectStationPage stationPage = previousPage as SelectStationPage;
            selectedTownStationCode = stationPage.SelectedTownStationCode;
            Terminal.Log(selectedTownStationCode);
            SetupSelector();
            base.OnEnter(previousPage, args);

        }

        public string SelectedTrack { get => (selectedTownStationCode + SelectTrackPage.TRACK_PARTS_SEPARATOR + menuSelector.Current.displayText); }
    }
}
