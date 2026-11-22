using System;
using System.IO;
using GTA;

namespace AvernesTreiner.Core
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = @"scripts\AvernesTreiner\Config.ini";
        private static ScriptSettings _settings;

        private static string _language = "en-US";
        private static string _menuKey = "F10";
        private static string _version = "v0.3";
        private static string _title = "AvernesTreiner";
        private static string _speedUnit = "KM/H";

        public static string Language { get { return _language; } set { _language = value; } }
        public static string MenuKey { get { return _menuKey; } set { _menuKey = value; } }
        public static string Version { get { return _version; } set { _version = value; } }
        public static string Title { get { return _title; } set { _title = value; } }
        public static string SpeedUnit { get { return _speedUnit; } set { _speedUnit = value; } }

        private static void EnsureConfigFile()
        {
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(ConfigPath))
            {
                File.WriteAllText(ConfigPath,
                    "[General]\r\n" +
                    "Language=en-US\r\n" +
                    "MenuKey=F10\r\n" +
                    "Version=v0.3\r\n" +
                    "Title=AvernesTreiner\r\n" +
                    "SpeedUnit=KM/H\r\n");
            }
        }

        public static bool Load()
        {
            try
            {
                EnsureConfigFile();
                _settings = ScriptSettings.Load(ConfigPath);

                _language = _settings.GetValue("General", "Language", "en-US");
                _speedUnit = _settings.GetValue("General", "SpeedUnit", "KM/H");

                // These values are fixed by the AvernesTreiner release contract.
                // Migrate existing 0.1 configurations from F4 / v1.0.0 to F10 / v0.3.
                _menuKey = "F10";
                _version = "v0.3";
                _title = "AvernesTreiner";

                _settings.SetValue("General", "Language", _language);
                _settings.SetValue("General", "MenuKey", _menuKey);
                _settings.SetValue("General", "Version", _version);
                _settings.SetValue("General", "Title", _title);
                _settings.SetValue("General", "SpeedUnit", _speedUnit);
                _settings.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool GetBool(string section, string key, bool defaultValue)
        {
            try
            {
                if (_settings == null)
                {
                    EnsureConfigFile();
                    _settings = ScriptSettings.Load(ConfigPath);
                }

                string value = _settings.GetValue(section, key, defaultValue ? "true" : "false");
                bool parsed;
                return bool.TryParse(value, out parsed) ? parsed : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public static void SetBool(string section, string key, bool value)
        {
            try
            {
                EnsureConfigFile();
                if (_settings == null)
                {
                    _settings = ScriptSettings.Load(ConfigPath);
                }

                _settings.SetValue(section, key, value ? "true" : "false");
                Save();
            }
            catch { }
        }

        public static bool Save()
        {
            try
            {
                EnsureConfigFile();
                if (_settings == null)
                {
                    _settings = ScriptSettings.Load(ConfigPath);
                }

                _settings.SetValue("General", "Language", _language);
                _settings.SetValue("General", "MenuKey", "F10");
                _settings.SetValue("General", "Version", "v0.3");
                _settings.SetValue("General", "Title", "AvernesTreiner");
                _settings.SetValue("General", "SpeedUnit", _speedUnit);
                _settings.Save();

                _menuKey = "F10";
                _version = "v0.3";
                _title = "AvernesTreiner";
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static class Localization
    {
        private static ScriptSettings _langSettings;

        public static void LoadLanguage(string langCode)
        {
            try
            {
                string path = string.Format(@"scripts\AvernesTreiner\Languages\{0}.ini", langCode);
                if (!File.Exists(path))
                {
                    path = @"scripts\AvernesTreiner\Languages\en-US.ini";
                }
                if (File.Exists(path))
                {
                    _langSettings = ScriptSettings.Load(path);
                }
            }
            catch { }
        }

        public static string Get(string section, string key, string fallback)
        {
            try
            {
                if (_langSettings != null)
                {
                    return _langSettings.GetValue(section, key, fallback);
                }
            }
            catch { }
            return fallback;
        }

        public static string Get(string section, string key)
        {
            return Get(section, key, "");
        }
    }
}