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
    public class SelectJobPage : CRMSelectorSubPage
    {
        public SelectJobPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        protected override List<MenuItem> CreateMenuItems()
        {
            List<JobBooklet> allJobBooklets = new List<JobBooklet>(JobBooklet.allExistingJobBooklets);

            var list = (allJobBooklets.Select(b =>
            {
                return new MenuItem(b.job.ID, "Select", null);
            }).ToList());

            list.Add( new MenuItem("Cancel", "Back", () => Return()));

            return list;
        }

        public override void OnEnter(CRMPage previousPage, CRMPageArgs args)
        {
            SetupSelector();

            base.OnEnter(previousPage, args);
        }

        public override void OnAction()
        {
            Return();
        }

        public string SelectedJobName { get => menuSelector.Current.displayText; }
    }
}
