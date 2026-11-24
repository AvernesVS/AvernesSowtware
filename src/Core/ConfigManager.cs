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
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AvernesTreiner");
        private static string _configPath;
        private static ScriptSettings _settings;

        private static string _language = "en-US";
        private static string _menuKey = "F10";
        private static string _version = "v1.0";
        private static string _title = "AvernesTreiner";
        private static string _speedUnit = "KM/H";

        public static string Language { get { return _language; } set { _language = value; } }
        public static string MenuKey { get { return _menuKey; } set { _menuKey = value; } }
        public static string Version { get { return _version; } set { _version = value; } }
        public static string Title { get { return _title; } set { _title = value; } }
        public static string SpeedUnit { get { return _speedUnit; } set { _speedUnit = value; } }
        public static string LastError { get; private set; }
        public static string RuntimeDirectoryPath { get { return RuntimeDirectory; } }
        public static string UserDataDirectoryPath { get { return UserConfigDirectory; } }
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
                "Version=v1.0\r\n" +
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
            if (!Directory.Exists(UserConfigDirectory))
                Directory.CreateDirectory(UserConfigDirectory);

            // Migrate existing files from the old game-local folder only when the
            // permanent Documents copy does not already exist. Never overwrite user data.
            string[] userFiles = new string[] { "Config.ini", "SavedVehicles.json", "addons.json" };
            for (int i = 0; i < userFiles.Length; i++)
            {
                string destination = Path.Combine(UserConfigDirectory, userFiles[i]);
                string legacy = Path.Combine(RuntimeDirectory, userFiles[i]);
                if (!File.Exists(destination) && File.Exists(legacy))
                {
                    string backup = destination + ".migration.bak";
                    File.Copy(legacy, backup, true);
                    File.Copy(legacy, destination, false);
                }
            }

            _configPath = Path.Combine(UserConfigDirectory, "Config.ini");
            if (!File.Exists(_configPath) || new FileInfo(_configPath).Length == 0)
                File.WriteAllText(_configPath, DefaultConfig(), Encoding.UTF8);
        }

        public static bool Load()
        {
            try
            {
                EnsureConfigFile();
                try
                {
                    _settings = ScriptSettings.Load(_configPath);
                }
                catch
                {
                    string backupPath = _configPath + ".bak";
                    if (!File.Exists(backupPath)) throw;
                    _settings = ScriptSettings.Load(backupPath);
                    File.Copy(backupPath, _configPath, true);
                    // Rebind ScriptSettings to the active Config.ini path; otherwise Save()
                    // would continue writing to the .bak file loaded above.
                    _settings = ScriptSettings.Load(_configPath);
                    LastError = "Config.ini was recovered from its backup.";
                }
                _language = _settings.GetValue("General", "Language", "en-US");
                if (!string.Equals(_language, "ru-RU", StringComparison.OrdinalIgnoreCase)) _language = "en-US";
                _speedUnit = _settings.GetValue("General", "SpeedUnit", "KM/H");
                _menuKey = "F10";
                _version = "v1.0";
                _title = "AvernesTreiner";
                // Loading must not write the file; saving is explicit through Settings.
                if (string.IsNullOrEmpty(LastError) || !LastError.StartsWith("Config.ini was recovered", StringComparison.Ordinal))
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
                TrainerLog.Write("ConfigManager.GetBool: " + section + "." + key, ex);
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
                TrainerLog.Write("ConfigManager.SetBool: " + section + "." + key, ex);
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
                _settings.SetValue("General", "Version", "v1.0");
                _settings.SetValue("General", "Title", "AvernesTreiner");
                _settings.SetValue("General", "SpeedUnit", _speedUnit);
                if (File.Exists(_configPath))
                    File.Copy(_configPath, _configPath + ".bak", true);
                _settings.Save();

                if (!File.Exists(_configPath) || new FileInfo(_configPath).Length == 0)
                    throw new IOException("Config.ini was not created.");

                _menuKey = "F10";
                _version = "v1.0";
                _title = "AvernesTreiner";
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                TrainerLog.Write("ConfigManager.Save", ex);
                return false;
            }
        }
    }

    public static class Localization
    {
        private static readonly System.Collections.Generic.Dictionary<string, string> Values =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void LoadLanguage(string langCode)
        {
            Values.Clear();
            if (!string.Equals(langCode, "ru-RU", StringComparison.OrdinalIgnoreCase))
                langCode = "en-US";
            string path = Path.Combine(ConfigManager.RuntimeDirectoryPath, "Languages", langCode + ".ini");
            if (!File.Exists(path))
                path = Path.Combine(ConfigManager.RuntimeDirectoryPath, "Languages", "en-US.ini");
            if (!File.Exists(path)) return;

            string section = "";
            foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = rawLine.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                int separator = line.IndexOf('=');
                if (separator <= 0) continue;
                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                Values[section + "." + key] = value;
            }
        }

        public static string Get(string section, string key, string fallback)
        {
            string value;
            return Values.TryGetValue(section + "." + key, out value) ? value : fallback;
        }

        public static string Get(string section, string key)
        {
            return Get(section, key, "");
        }
    }
}