using AvernesTreiner.Core;
using GTA;
using NativeUI;
using System;
using System.Collections.Generic;
using System.IO;

namespace AvernesTreiner.Modules
{
    public class SettingsModule : IAvernesModule
    {
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
                UI.Notify("~b~Language saved selection. Press Save Settings, then reload the trainer script.");
            };
            settingsMenu.AddItem(languageItem);

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
                UI.Notify("~b~Speedometer unit changed to " + ConfigManager.SpeedUnit +
                    ". Press Save Settings to keep this change.");
            };
            settingsMenu.AddItem(unitItem);

            var saveItem = new UIMenuItem(
                Localization.Get("Settings", "SaveSettings", "Save Settings"),
                "Writes the current settings to Config.ini");
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
                "Reloads saved settings from Config.ini");
            loadItem.Activated += (sender, selected) =>
            {
                if (ConfigManager.Load())
                {
                    unitItem.Index = string.Equals(ConfigManager.SpeedUnit, "MPH", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    languageItem.Index = string.Equals(ConfigManager.Language, "ru-RU", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    UI.Notify("~g~Settings loaded. Restart the trainer script to apply saved toggle states.");
                }
                else
                {
                    UI.Notify("~r~Could not load Config.ini: " + ConfigManager.LastError);
                }
            };
            settingsMenu.AddItem(loadItem);

            var backupItem = new UIMenuItem("Back Up Saved User Data", "Copies saved settings, vehicles and add-on catalog to a dated backup folder.");
            backupItem.Activated += (sender, selected) => CreateUserDataBackup();
            settingsMenu.AddItem(backupItem);

            var locationItem = new UIMenuItem("Show User Data Location", "Displays the permanent folder used by AvernesTreiner.");
            locationItem.Activated += (sender, selected) =>
                UI.Notify("~b~User data: ~w~" + ConfigManager.UserDataDirectoryPath);
            settingsMenu.AddItem(locationItem);

            var reloadAddonsItem = new UIMenuItem(
                Localization.Get("Settings", "ReloadAddons", "Reload addons.json"),
                "Refreshes the vehicle list from Documents/AvernesTreiner/addons.json");
            reloadAddonsItem.Activated += (sender, selected) =>
            {
                AddonManager.Load();
                UI.Notify("~g~addons.json reloaded! " + AddonManager.Addons.Count + " vehicles found.");
            };
            settingsMenu.AddItem(reloadAddonsItem);

            var verItem = new UIMenuItem(
                Localization.Get("Settings", "VersionLabel", "Version: v1.0"),
                "Official AvernesTreiner version");
            settingsMenu.AddItem(verItem);

            var authorItem = new UIMenuItem(
                Localization.Get("Settings", "CreatorLabel", "Created by Avernes"),
                "Official AvernesTreiner");
            settingsMenu.AddItem(authorItem);
        }

        private void CreateUserDataBackup()
        {
            try
            {
                string source = ConfigManager.UserDataDirectoryPath;
                if (!Directory.Exists(source)) Directory.CreateDirectory(source);
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string backupRoot = Path.Combine(documents, "AvernesTreiner_Backup");
                string backupPath = Path.Combine(backupRoot, "manual_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupPath);

                string[] files = new string[] {
                    "Config.ini", "Config.ini.bak", "SavedVehicles.json",
                    "SavedVehicles.json.bak", "addons.json"
                };
                int copied = 0;
                for (int i = 0; i < files.Length; i++)
                {
                    string sourceFile = Path.Combine(source, files[i]);
                    if (File.Exists(sourceFile))
                    {
                        File.Copy(sourceFile, Path.Combine(backupPath, files[i]), true);
                        copied++;
                    }
                }

                UI.Notify("~g~User data backup created. Files: " + copied + ".");
            }
            catch (Exception ex)
            {
                TrainerLog.Write("Settings.Backup", ex);
                UI.Notify("~r~Could not create user data backup: " + ex.Message);
            }
        }

        public void OnTick() { }
    }
}