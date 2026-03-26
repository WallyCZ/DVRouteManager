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
