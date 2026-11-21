using AvernesTreiner.Core;
using AvernesTreiner.Modules;
using GTA.Math;
using GTA.Native;
using GTA;
using NativeUI;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System;

namespace AvernesTreiner.Modules
{
    public class SettingsModule : IAvernesModule
    {
        public string Name { get { return "Settings"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var settingsMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuSettings", "Settings"));
// Speedometer Unit selector
            var unitList = new List<dynamic> { "KM/H (Metric)", "MPH (Imperial)" };
            int currentUnitIdx = string.Equals(ConfigManager.SpeedUnit, "MPH", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var unitItem = new UIMenuListItem(Localization.Get("Settings", "SpeedUnit", "Speed Unit"), unitList, currentUnitIdx, "Switch between KM/H and MPH");
            unitItem.OnListChanged += (sender, index) =>
            {
                ConfigManager.SpeedUnit = (index == 1) ? "MPH" : "KM/H";
                ConfigManager.Save();
                UI.Notify("~g~Speedometer: " + ConfigManager.SpeedUnit);
            };
            settingsMenu.AddItem(unitItem);

            // Reload addons.json
            var reloadAddonsItem = new UIMenuItem(Localization.Get("Settings", "ReloadAddons", "Reload addons.json"), "Refreshes the vehicle list from scripts/AvernesTreiner/addons.json");
            reloadAddonsItem.Activated += (sender, selected) =>
            {
                AddonManager.Load();
                UI.Notify("~g~addons.json reloaded! " + AddonManager.Addons.Count + " vehicles found.");
            };
            settingsMenu.AddItem(reloadAddonsItem);

            // Version info
            var verItem = new UIMenuItem(Localization.Get("Settings", "VersionLabel", "Version: v1.0.0"), "Official AvernesTreiner version");
            settingsMenu.AddItem(verItem);

            // Creator info
            var authorItem = new UIMenuItem(Localization.Get("Settings", "CreatorLabel", "Created by Avernes"), "Official AvernesTreiner");
            settingsMenu.AddItem(authorItem);
        }

        public void OnTick() { }
    }
}
