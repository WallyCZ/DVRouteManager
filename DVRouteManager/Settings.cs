using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace DVRouteManager
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Header("Routing")]
        [Draw(DrawType.ToggleGroup, Label = "Reversing strategy")] public ReversingStrategy ReversingStrategy = ReversingStrategy.ChooseBest;
        [Header("Keys")]
        [Draw(DrawType.KeyBinding)] public KeyBinding CruiseControlToggle = new KeyBinding() { keyCode = KeyCode.C };
        [Draw(DrawType.KeyBinding)] public KeyBinding CruiseControl30 = new KeyBinding() { keyCode = KeyCode.V };
        [Draw(DrawType.KeyBinding)] public KeyBinding CruiseControl60 = new KeyBinding() { keyCode = KeyCode.B };
        [Draw(DrawType.KeyBinding)] public KeyBinding CruiseControlMinus = new KeyBinding() { keyCode = KeyCode.Comma };
        [Draw(DrawType.KeyBinding)] public KeyBinding CruiseControlPlus = new KeyBinding() { keyCode = KeyCode.Period };
        [Draw(DrawType.KeyBinding)] public KeyBinding TrainEndAlarm = new KeyBinding() { keyCode = KeyCode.N };
        public void OnChange()
        {
            //Main.ApplySettings(Preset);
        }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
