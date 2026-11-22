using System;
using System.IO;
using System.Text;
using GTA;

namespace AvernesTreiner.Core
{
    public static class ConfigManager
    {
        private static readonly string RuntimeDirectory = ResolveRuntimeDirectory();
        private static readonly string UserConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvernesTreiner");
        private static string _configPath;
        private static ScriptSettings _settings;

        private static string _language = "en-US";
        private static string _menuKey = "F10";
        private static string _version = "v0.6";
        private static string _title = "AvernesTreiner";
        private static string _speedUnit = "KM/H";

        public static string Language { get { return _language; } set { _language = value; } }
        public static string MenuKey { get { return _menuKey; } set { _menuKey = value; } }
        public static string Version { get { return _version; } set { _version = value; } }
        public static string Title { get { return _title; } set { _title = value; } }
        public static string SpeedUnit { get { return _speedUnit; } set { _speedUnit = value; } }
        public static string LastError { get; private set; }
        public static string RuntimeDirectoryPath { get { return RuntimeDirectory; } }
        public static string ConfigPath { get { EnsureConfigFile(); return _configPath; } }
        public static string SaveLocationDescription
        {
            get
            {
                return !string.IsNullOrEmpty(_configPath) &&
                    _configPath.StartsWith(UserConfigDirectory, StringComparison.OrdinalIgnoreCase)
                    ? "Windows user profile"
                    : "GTA V scripts folder";
            }
        }

        private static string ResolveRuntimeDirectory()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string trimmed = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string leaf = Path.GetFileName(trimmed);

            // ScriptHookVDotNet can expose the game root, scripts folder,
            // or this trainer's runtime folder as the AppDomain base directory.
            if (string.Equals(leaf, "AvernesTreiner", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            if (string.Equals(leaf, "scripts", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(trimmed, "AvernesTreiner");
            return Path.Combine(trimmed, "scripts", "AvernesTreiner");
        }

        private static string DefaultConfig()
        {
            return "[General]\r\n" +
                "Language=en-US\r\n" +
                "MenuKey=F10\r\n" +
                "Version=v0.6\r\n" +
                "Title=AvernesTreiner\r\n" +
                "SpeedUnit=KM/H\r\n";
        }

        private static bool TryPrepareWritablePath(string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                using (FileStream stream = new FileStream(path, FileMode.OpenOrCreate,
                    FileAccess.Write, FileShare.ReadWrite))
                {
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureConfigFile()
        {
            if (!string.IsNullOrEmpty(_configPath) && File.Exists(_configPath))
                return;

            string primaryPath = Path.Combine(RuntimeDirectory, "Config.ini");
            if (TryPrepareWritablePath(primaryPath))
            {
                _configPath = primaryPath;
            }
            else
            {
                string fallbackPath = Path.Combine(UserConfigDirectory, "Config.ini");
                string fallbackDirectory = Path.GetDirectoryName(fallbackPath);
                if (!Directory.Exists(fallbackDirectory))
                    Directory.CreateDirectory(fallbackDirectory);

                if (!File.Exists(fallbackPath))
                {
                    if (File.Exists(primaryPath))
                        File.Copy(primaryPath, fallbackPath);
                    else
                        File.WriteAllText(fallbackPath, DefaultConfig(), Encoding.UTF8);
                }

                if (!TryPrepareWritablePath(fallbackPath))
                    throw new UnauthorizedAccessException("Neither the GTA scripts folder nor the Windows user config folder is writable.");

                _configPath = fallbackPath;
            }

            FileInfo configInfo = new FileInfo(_configPath);
            if (!configInfo.Exists || configInfo.Length == 0)
                File.WriteAllText(_configPath, DefaultConfig(), Encoding.UTF8);
        }

        public static bool Load()
        {
            try
            {
                EnsureConfigFile();
                _settings = ScriptSettings.Load(_configPath);
                _language = _settings.GetValue("General", "Language", "en-US");
                _speedUnit = _settings.GetValue("General", "SpeedUnit", "KM/H");
                _menuKey = "F10";
                _version = "v0.6";
                _title = "AvernesTreiner";
                // Loading must not write the file; saving is explicit through Settings.
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
                    _settings = ScriptSettings.Load(_configPath);
                }
                string value = _settings.GetValue(section, key, defaultValue ? "true" : "false");
                bool parsed;
                return bool.TryParse(value, out parsed) ? parsed : defaultValue;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return defaultValue;
            }
        }

        public static void SetBool(string section, string key, bool value)
        {
            try
            {
                if (_settings == null)
                {
                    EnsureConfigFile();
                    _settings = ScriptSettings.Load(_configPath);
                }
                // Keep changes in memory until the user presses Save Settings.
                _settings.SetValue(section, key, value ? "true" : "false");
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
                if (_settings == null)
                    _settings = ScriptSettings.Load(_configPath);

                _settings.SetValue("General", "Language", _language);
                _settings.SetValue("General", "MenuKey", "F10");
                _settings.SetValue("General", "Version", "v0.6");
                _settings.SetValue("General", "Title", "AvernesTreiner");
                _settings.SetValue("General", "SpeedUnit", _speedUnit);
                _settings.Save();

                if (!File.Exists(_configPath))
                    throw new IOException("Config.ini was not created.");

                _menuKey = "F10";
                _version = "v0.6";
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
                string directory = ConfigManager.RuntimeDirectoryPath;
                string path = Path.Combine(directory, "Languages", langCode + ".ini");
                if (!File.Exists(path))
                    path = Path.Combine(directory, "Languages", "en-US.ini");
                if (File.Exists(path))
                    _langSettings = ScriptSettings.Load(path);
            }
            catch { }
        }

        public static string Get(string section, string key, string fallback)
        {
            try
            {
                if (_langSettings != null)
                    return _langSettings.GetValue(section, key, fallback);
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