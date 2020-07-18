using CommandTerminal;
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
    public class SelectTownStationTrackPage : CRMPage
    {

        public SelectTownStationTrackPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        public override void OnEnter(CRMPage previousPage, CRMPageArgs args)
        {
            // call sequentionaly all three pages
            SetSubPage(typeof(SelectTownPage), null, (townPage) =>
            {
                Manager.SetSubPage(typeof(SelectStationPage), (stationPage) =>
                {
                    //here we should be ruterned back to caller
                    Manager.SetPage(typeof(SelectTrackPage), null, stationPage);
                }, null, townPage);
            });
        }

        public override void OnAction()
        {
            //no action is made
        }
    }
}
