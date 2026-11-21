using AvernesTreiner.Core;
using GTA;
using NativeUI;
using System;
using System.Collections.Generic;

namespace AvernesTreiner.Modules
{
    public class SettingsModule : IAvernesModule
    {
        public string Name { get { return "Settings"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var settingsMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuSettings", "Settings"));

            var unitList = new List<dynamic> { "KM/H (Metric)", "MPH (Imperial)" };
            int currentUnitIdx = string.Equals(ConfigManager.SpeedUnit, "MPH", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var unitItem = new UIMenuListItem(
                Localization.Get("Settings", "SpeedUnit", "Speed Unit"),
                unitList,
                currentUnitIdx,
                "Switch between KM/H and MPH");
            unitItem.OnListChanged += (sender, index) =>
            {
                ConfigManager.SpeedUnit = (index == 1) ? "MPH" : "KM/H";
                if (ConfigManager.Save())
                {
                    UI.Notify("~g~Speedometer: " + ConfigManager.SpeedUnit);
                }
                else
                {
                    UI.Notify("~r~Could not save settings.");
                }
            };
            settingsMenu.AddItem(unitItem);

            var saveItem = new UIMenuItem(
                Localization.Get("Settings", "SaveSettings", "Save Settings"),
                "Writes the current settings to Config.ini");
            saveItem.Activated += (sender, selected) =>
            {
                UI.Notify(ConfigManager.Save()
                    ? "~g~Settings saved to Config.ini."
                    : "~r~Could not save Config.ini.");
            };
            settingsMenu.AddItem(saveItem);

            var loadItem = new UIMenuItem(
                Localization.Get("Settings", "LoadSettings", "Load Settings"),
                "Reloads saved settings from Config.ini");
            loadItem.Activated += (sender, selected) =>
            {
                if (ConfigManager.Load())
                {
                    unitItem.Index = string.Equals(ConfigManager.SpeedUnit, "MPH", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    UI.Notify("~g~Settings loaded from Config.ini.");
                }
                else
                {
                    UI.Notify("~r~Could not load Config.ini.");
                }
            };
            settingsMenu.AddItem(loadItem);

            var reloadAddonsItem = new UIMenuItem(
                Localization.Get("Settings", "ReloadAddons", "Reload addons.json"),
                "Refreshes the vehicle list from scripts/AvernesTreiner/addons.json");
            reloadAddonsItem.Activated += (sender, selected) =>
            {
                AddonManager.Load();
                UI.Notify("~g~addons.json reloaded! " + AddonManager.Addons.Count + " vehicles found.");
            };
            settingsMenu.AddItem(reloadAddonsItem);

            var verItem = new UIMenuItem(
                Localization.Get("Settings", "VersionLabel", "Version: v0.2"),
                "Official AvernesTreiner version");
            settingsMenu.AddItem(verItem);

            var authorItem = new UIMenuItem(
                Localization.Get("Settings", "CreatorLabel", "Created by Avernes"),
                "Official AvernesTreiner");
            settingsMenu.AddItem(authorItem);
        }

        public void OnTick() { }
    }
}