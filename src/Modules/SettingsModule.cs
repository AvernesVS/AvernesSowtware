using AvernesTreiner.Core;
using GTA;
using NativeUI;
using System;
using System.Collections.Generic;

namespace AvernesTreiner.Modules
{
    public class SettingsModule : IAvernesModule
    {
        private readonly Action<bool> _requestMenuRebuild;

        public SettingsModule(Action<bool> requestMenuRebuild)
        {
            _requestMenuRebuild = requestMenuRebuild;
        }

        public string Name { get { return "Settings"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var settingsMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuSettings", "Settings"));

            var languageList = new List<dynamic> { Localization.Get("Settings", "English", "English"), Localization.Get("Settings", "Russian", "Русский") };
            int currentLanguageIndex = string.Equals(ConfigManager.Language, "ru-RU", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var languageItem = new UIMenuListItem(
                Localization.Get("Settings", "Language", "Language"),
                languageList,
                currentLanguageIndex,
                Localization.Get("Settings", "LanguageDesc", "Choose menu language"));
            languageItem.OnListChanged += (sender, index) =>
            {
                ConfigManager.Language = index == 1 ? "ru-RU" : "en-US";
                Localization.LoadLanguage(ConfigManager.Language);
                bool saved = ConfigManager.Save();
                if (!saved)
                {
                    UI.Notify("~r~Could not save language: " + ConfigManager.LastError);
                    return;
                }
                UI.Notify(Localization.Get("Settings", "LanguageChanged", "Language changed."));
                if (_requestMenuRebuild != null) _requestMenuRebuild(false);
            };
            settingsMenu.AddItem(languageItem);

            var unitList = new List<dynamic> { Localization.Get("Settings", "SpeedMetric", "KM/H (Metric)"), Localization.Get("Settings", "SpeedImperial", "MPH (Imperial)") };
            int currentUnitIdx = string.Equals(ConfigManager.SpeedUnit, "MPH", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var unitItem = new UIMenuListItem(
                Localization.Get("Settings", "SpeedUnit", "Speed Unit"),
                unitList,
                currentUnitIdx,
                "Switch between KM/H and MPH");
            unitItem.OnListChanged += (sender, index) =>
            {
                ConfigManager.SpeedUnit = (index == 1) ? "MPH" : "KM/H";
                UI.Notify("~b~Speedometer unit changed to " + ConfigManager.SpeedUnit +
                    ". Press Save Settings to keep this change.");
            };
            settingsMenu.AddItem(unitItem);

            var saveItem = new UIMenuItem(
                Localization.Get("Settings", "SaveSettings", "Save Settings"),
                Localization.Get("Settings", "SaveSettingsDesc", "Writes the current settings to Config.ini"));
            saveItem.Activated += (sender, selected) =>
            {
                bool saved = ConfigManager.Save();
                UI.Notify(saved
                    ? "~g~Settings saved (" + ConfigManager.SaveLocationDescription + ")."
                    : "~r~Could not save Config.ini: " + ConfigManager.LastError);
            };
            settingsMenu.AddItem(saveItem);

            var loadItem = new UIMenuItem(
                Localization.Get("Settings", "LoadSettings", "Load Settings"),
                Localization.Get("Settings", "LoadSettingsDesc", "Reloads saved settings from Config.ini"));
            loadItem.Activated += (sender, selected) =>
            {
                if (ConfigManager.Load())
                {
                    Localization.LoadLanguage(ConfigManager.Language);
                    unitItem.Index = string.Equals(ConfigManager.SpeedUnit, "MPH", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    languageItem.Index = string.Equals(ConfigManager.Language, "ru-RU", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    UI.Notify(Localization.Get("Settings", "SettingsLoaded", "Settings loaded."));
                    if (_requestMenuRebuild != null) _requestMenuRebuild(true);
                }
                else
                {
                    UI.Notify("~r~Could not load Config.ini: " + ConfigManager.LastError);
                }
            };
            settingsMenu.AddItem(loadItem);

            var reloadAddonsItem = new UIMenuItem(
                Localization.Get("Settings", "ReloadAddons", "Reload addons.json"),
                Localization.Get("Settings", "ReloadAddonsDesc", "Refreshes the vehicle list from Documents/AvernesTreiner/addons.json"));
            reloadAddonsItem.Activated += (sender, selected) =>
            {
                AddonManager.Load();
                UI.Notify("~g~addons.json reloaded! " + AddonManager.Addons.Count + " vehicles found.");
            };
            settingsMenu.AddItem(reloadAddonsItem);

            var verItem = new UIMenuItem(
                Localization.Get("Settings", "VersionLabel", "Version: v0.8.3"),
                Localization.Get("Settings", "VersionDesc", "Official AvernesTreiner version"));
            settingsMenu.AddItem(verItem);

            var authorItem = new UIMenuItem(
                Localization.Get("Settings", "CreatorLabel", "Created by Avernes"),
                Localization.Get("Settings", "CreatorDesc", "Official AvernesTreiner"));
            settingsMenu.AddItem(authorItem);
        }

        public void ReloadSettings() { }

        public void OnTick() { }
    }
}