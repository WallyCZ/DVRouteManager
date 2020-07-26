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
    public class CruiseControlPage : CRMSelectorPage
    {
        private const float MESSAGE_TIMEOUT = 2.0f;

        private int lastSelectoritemsHash = 0;
        private int lastSelectorIndex = 0;

        public CruiseControlPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        private void LocoCruiseControl_OnCruiseControlChange(object sender, EventArgs e)
        {
            SetupSelector();
            RestoreSelectorIndex();
            PrintCurrentSelector();
        }

        protected override List<MenuItem> CreateMenuItems()
        {
            if(menuSelector != null)
            {
                lastSelectorIndex = menuSelector.Index;
            }

            var menus = new List<MenuItem>();

            string currentSpeedSet = LocoCruiseControl.GetTargetSpeed().HasValue ? $"\n\n\nSet to {LocoCruiseControl.GetTargetSpeed():0} km/h" : "\n\n\nSpeed not set";

            int hash = 1;
            if ( ! LocoCruiseControl.IsSet)
            {
                menus.Add(new MenuItem("Set current speed", "Set", () => SetCruiseControl()));
                hash += 2;
            }

            menus.Add(new MenuItem("30 km/h" + currentSpeedSet, "Set", () => SetCruiseControl(30.0f)));
            menus.Add(new MenuItem("60 km/h" + currentSpeedSet, "Set", () => SetCruiseControl(60.0f)));

            if (LocoCruiseControl.IsSet)
            {
                menus.Add(new MenuItem("+ 5 km/h" + currentSpeedSet, "Add", () => UpdateTargetSpeed(5.0f)));
                menus.Add(new MenuItem("- 5 km/h" + currentSpeedSet, "Sub", () => UpdateTargetSpeed(-5.0f)));
                menus.Add(new MenuItem("Reset", "Cancel", () => ResetCruiseControl()));
                hash += 4;
            }
            menus.Add(GetExitMenu());

            //if nothing changed try to keep old selector index, otherwise set it to 0
            if(hash != lastSelectoritemsHash)
            {
                lastSelectorIndex = 0;
                lastSelectoritemsHash = hash;
            }

            return menus;
        }

        private void ResetCruiseControl()
        {
            LocoCruiseControl.ResetCruiseControl();
            CallMessageSubPage($"Cruise control disabled", "", MESSAGE_TIMEOUT);
        }

        private void SetCruiseControl(float? speed = null)
        {
            float speedSet = LocoCruiseControl.SetCruiseControl(speed);
            //CallMessageSubPage($"Speed set to {speedSet:0.#} km/h", "", MESSAGE_TIMEOUT);
        }

        private void UpdateTargetSpeed(float speedDiff)
        {
            float speed = LocoCruiseControl.UpdateTargetSpeed(speedDiff);
            //CallMessageSubPage($"Speed set to {speed:0.#} km/h", "", MESSAGE_TIMEOUT);
        }

        public override void OnEnter(CRMPage previousPage, CRMPageArgs args)
        {
            Terminal.Log("CruiseControlPage.OnEnter");
            SetupSelector();

            RestoreSelectorIndex();

            LocoCruiseControl.OnCruiseControlChange += LocoCruiseControl_OnCruiseControlChange;

            base.OnEnter(previousPage, args);
        }

        private void RestoreSelectorIndex()
        {
            //try to return previous selector index
            for (int i = 0; i < lastSelectorIndex; i++)
            {
                menuSelector.MoveNextRewind();
            }
        }

        public override void OnLeave()
        {
            Terminal.Log("CruiseControlPage.OnLeave");
            LocoCruiseControl.OnCruiseControlChange -= LocoCruiseControl_OnCruiseControlChange;
            base.OnLeave();
        }
    }

    
}
