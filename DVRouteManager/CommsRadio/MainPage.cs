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
    public class MainPage : CRMSelectorPage
    {
        public MainPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        protected override List<MenuItem> CreateMenuItems()
        {
            var menus = new List<MenuItem>();

            if ( ! Module.ActiveRoute.IsSet)
            {
                menus.Add(new MenuItem("New route", "Select", () => SetPage(typeof(NewRoutePage))));
            }
            else
            {
                menus.Add(new MenuItem("Active route", "Select", () => SetPage(typeof(RouteInfoPage))));
            }

            menus.Add(new MenuItem("Cruise Control (experimental)", "Select", () => SetPage(typeof(CruiseControlPage))));
            menus.Add(new MenuItem("Diesel loco AI (experimental)", "Select", () => SetPage(typeof(LocoAIPage))));
            menus.Add(new MenuItem("Settings", "Select", () => SetPage(typeof(SettingsPage))));
            menus.Add(GetExitMenu());

            return menus;
        }

        public override void OnEnter(CRMPage previousPage, CRMPageArgs args)
        {
            SetupSelector();

            base.OnEnter(previousPage, args);
        }
    }
}
