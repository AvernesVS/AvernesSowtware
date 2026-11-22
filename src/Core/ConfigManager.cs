using System;
using System.IO;
using GTA;

namespace AvernesTreiner.Core
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "scripts", "AvernesTreiner", "Config.ini");
        private static ScriptSettings _settings;

        private static string _language = "en-US";
        private static string _menuKey = "F10";
        private static string _version = "v0.5";
        private static string _title = "AvernesTreiner";
        private static string _speedUnit = "KM/H";

        public static string Language { get { return _language; } set { _language = value; } }
        public static string MenuKey { get { return _menuKey; } set { _menuKey = value; } }
        public static string Version { get { return _version; } set { _version = value; } }
        public static string Title { get { return _title; } set { _title = value; } }
        public static string SpeedUnit { get { return _speedUnit; } set { _speedUnit = value; } }
        public static string LastError { get; private set; }

        private static void EnsureConfigFile()
        {
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            if (!File.Exists(ConfigPath))
            {
                File.WriteAllText(ConfigPath,
                    "[General]\r\n" +
                    "Language=en-US\r\n" +
                    "MenuKey=F10\r\n" +
                    "Version=v0.5\r\n" +
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

                // These values are fixed by the release contract.
                _menuKey = "F10";
                _version = "v0.5";
                _title = "AvernesTreiner";
                _settings.SetValue("General", "Language", _language);
                _settings.SetValue("General", "MenuKey", _menuKey);
                _settings.SetValue("General", "Version", _version);
                _settings.SetValue("General", "Title", _title);
                _settings.SetValue("General", "SpeedUnit", _speedUnit);
                _settings.Save();
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
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
                if (_settings == null) _settings = ScriptSettings.Load(ConfigPath);
                _settings.SetValue(section, key, value ? "true" : "false");
                Save();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        public static bool Save()
        {
            try
            {
                EnsureConfigFile();
                if (_settings == null) _settings = ScriptSettings.Load(ConfigPath);

                _settings.SetValue("General", "Language", _language);
                _settings.SetValue("General", "MenuKey", "F10");
                _settings.SetValue("General", "Version", "v0.5");
                _settings.SetValue("General", "Title", "AvernesTreiner");
                _settings.SetValue("General", "SpeedUnit", _speedUnit);

                // SetBool stores feature states in this same ScriptSettings instance.
                // Saving here flushes General and every feature section together.
                _settings.Save();
                if (!File.Exists(ConfigPath))
                    throw new IOException("Config.ini was not created.");
                _menuKey = "F10";
                _version = "v0.5";
                _title = "AvernesTreiner";
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
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
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts",
                    "AvernesTreiner", "Languages", langCode + ".ini");
                if (!File.Exists(path))
                    path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts",
                        "AvernesTreiner", "Languages", "en-US.ini");
                if (File.Exists(path)) _langSettings = ScriptSettings.Load(path);
            }
            catch { }
        }

        public static string Get(string section, string key, string fallback)
        {
            try
            {
                if (_langSettings != null) return _langSettings.GetValue(section, key, fallback);
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