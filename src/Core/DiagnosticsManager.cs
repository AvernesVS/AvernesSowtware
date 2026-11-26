using AvernesTreiner.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using GTA;
using NativeUI;

namespace AvernesTreiner.Core
{
    public static class DiagnosticsManager
    {
        private const int MaxBufferedEntries = 256;
        private const int FlushIntervalMilliseconds = 1000;
        private const long MaxLogBytes = 2097152;
        private const int MaxEntryCharacters = 2000;

        private static readonly object SyncRoot = new object();
        private static readonly object FlushRoot = new object();
        private static readonly Queue<string> PendingEntries = new Queue<string>();
        private static readonly Dictionary<string, ExceptionThrottle> ExceptionWindows =
            new Dictionary<string, ExceptionThrottle>(StringComparer.OrdinalIgnoreCase);

        private static string _diagnosticsDirectory;
        private static string _logPath;
        private static string _previousLogPath;
        private static string _sessionMarkerPath;
        private static bool _initialized;
        private static bool _enabled = true;
        private static bool _detailedLogging;
        private static bool _hooksRegistered;
        private static bool _sessionStarted;
        private static int _lastFlushGameTime;
        private static int _droppedEntries;

        public static bool Enabled { get { return _enabled; } }
        public static bool DetailedLogging { get { return _detailedLogging; } }
        public static string LastError { get; private set; }

        private sealed class ExceptionThrottle
        {
            public DateTime LastWrittenUtc;
            public int SuppressedCount;
        }

        private static void MigrateLegacyDiagnostics()
        {
            string legacyDirectory = Path.Combine(ConfigManager.UserDataDirectoryPath, "Diagnostics");
            if (Directory.Exists(legacyDirectory))
            {
                foreach (string sourcePath in Directory.GetFiles(legacyDirectory))
                {
                    try
                    {
                        string destinationPath = Path.Combine(_diagnosticsDirectory, Path.GetFileName(sourcePath));
                        if (!File.Exists(destinationPath)) File.Move(sourcePath, destinationPath);
                    }
                    catch { }
                }

                try
                {
                    if (Directory.GetFiles(legacyDirectory).Length == 0 &&
                        Directory.GetDirectories(legacyDirectory).Length == 0)
                        Directory.Delete(legacyDirectory, false);
                }
                catch { }
            }

            if (Directory.Exists(ConfigManager.UserDataDirectoryPath))
            {
                string archiveDirectory = Path.GetDirectoryName(_diagnosticsDirectory);
                foreach (string sourcePath in Directory.GetFiles(ConfigManager.UserDataDirectoryPath,
                    "AvernesTreiner-Diagnostics-*.zip"))
                {
                    try
                    {
                        string destinationPath = Path.Combine(archiveDirectory, Path.GetFileName(sourcePath));
                        if (!File.Exists(destinationPath)) File.Move(sourcePath, destinationPath);
                    }
                    catch { }
                }
            }
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _enabled = ConfigManager.GetBool("Diagnostics", "Enabled", true);
            _detailedLogging = ConfigManager.GetBool("Diagnostics", "DetailedLogging", false);
            if (_enabled) StartSession();
        }

        private static bool EnsurePaths()
        {
            try
            {
                string scriptsDirectory = Path.GetDirectoryName(ConfigManager.RuntimeDirectoryPath);
                string bagsDirectory = Path.Combine(scriptsDirectory, "AvermesBags");
                _diagnosticsDirectory = Path.Combine(bagsDirectory, "Logs");
                _logPath = Path.Combine(_diagnosticsDirectory, "AvernesTreiner.log");
                _previousLogPath = Path.Combine(_diagnosticsDirectory, "AvernesTreiner.previous.log");
                _sessionMarkerPath = Path.Combine(_diagnosticsDirectory, "Session.active");
                Directory.CreateDirectory(_diagnosticsDirectory);
                MigrateLegacyDiagnostics();
                return true;
            }
            catch (Exception ex)
            {
                LastError = Sanitize(ex.Message);
                return false;
            }
        }

        private static void StartSession()
        {
            if (!EnsurePaths()) return;
            try
            {
                if (File.Exists(_sessionMarkerPath))
                {
                    string previousMarker = "";
                    try { previousMarker = File.ReadAllText(_sessionMarkerPath, Encoding.UTF8).Trim(); }
                    catch { }
                    LogWarning("System", "Previous session marker found; clean shutdown was not recorded. " +
                        (string.IsNullOrEmpty(previousMarker) ? "" : "Marker: " + previousMarker) +
                        " This can indicate a crash, forced process termination, or script unload without cleanup.");
                }

                File.WriteAllText(_sessionMarkerPath,
                    "Session started UTC: " + DateTime.UtcNow.ToString("o") + Environment.NewLine,
                    new UTF8Encoding(false));
                EnsureMonitoringHooks();
                _sessionStarted = true;
                LogInfo("System", "Diagnostics started. Buffer=256 entries; flush=1 second; log cap=2 MB per file.");
                Flush();
                LastError = null;
            }
            catch (Exception ex)
            {
                LastError = Sanitize(ex.Message);
            }
        }

        private static void EnsureMonitoringHooks()
        {
            if (_hooksRegistered) return;
            try
            {
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                _hooksRegistered = true;
            }
            catch (Exception ex)
            {
                LastError = Sanitize(ex.Message);
            }
        }

        public static void OnTick(int gameTime)
        {
            if (!_enabled || !_sessionStarted) return;
            if (gameTime - _lastFlushGameTime < FlushIntervalMilliseconds) return;
            _lastFlushGameTime = gameTime;
            Flush();
        }

        public static void LogInfo(string area, string message) { Enqueue("INFO", area, message); }
        public static void LogWarning(string area, string message) { Enqueue("WARN", area, message); }

        public static void LogDetail(string area, string message)
        {
            if (_detailedLogging) Enqueue("DETAIL", area, message);
        }

        public static void LogException(string area, Exception exception)
        {
            if (!_enabled) return;
            string exceptionText = exception == null ? "Unknown exception." : exception.ToString();
            string key = (area ?? "Unknown") + "|" +
                (exception == null ? "Unknown" : exception.GetType().FullName) + "|" +
                (exception == null ? "Unknown" : exception.Message);
            DateTime now = DateTime.UtcNow;
            int suppressed = 0;

            lock (SyncRoot)
            {
                ExceptionThrottle window;
                if (ExceptionWindows.TryGetValue(key, out window))
                {
                    if ((now - window.LastWrittenUtc).TotalSeconds < 5.0)
                    {
                        window.SuppressedCount++;
                        return;
                    }
                    suppressed = window.SuppressedCount;
                    window.SuppressedCount = 0;
                    window.LastWrittenUtc = now;
                }
                else
                {
                    if (ExceptionWindows.Count >= 64) ExceptionWindows.Clear();
                    window = new ExceptionThrottle();
                    window.LastWrittenUtc = now;
                    ExceptionWindows[key] = window;
                }
            }

            string suffix = suppressed > 0
                ? Environment.NewLine + "Identical exceptions suppressed during previous interval: " + suppressed + "."
                : "";
            Enqueue("ERROR", area, exceptionText + suffix);
            Flush();
        }

        private static void Enqueue(string level, string area, string message)
        {
            if (!_enabled) return;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string safeArea = string.IsNullOrEmpty(area) ? "General" : area.Replace("\r", " ").Replace("\n", " ");
            string safeMessage = Sanitize(message ?? "");
            if (safeMessage.Length > MaxEntryCharacters)
                safeMessage = safeMessage.Substring(0, MaxEntryCharacters) + "... [entry truncated]";

            lock (SyncRoot)
            {
                if (PendingEntries.Count >= MaxBufferedEntries)
                {
                    PendingEntries.Dequeue();
                    _droppedEntries++;
                }
                PendingEntries.Enqueue(timestamp + " [" + level + "] [" + safeArea + "] " + safeMessage);
            }
        }

        public static void Flush()
        {
            lock (FlushRoot)
            {
                string payload;
                lock (SyncRoot)
                {
                    if (PendingEntries.Count == 0 || string.IsNullOrEmpty(_logPath)) return;
                    StringBuilder builder = new StringBuilder();
                    if (_droppedEntries > 0)
                    {
                        builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        builder.Append(" [WARN] [Diagnostics] Older buffered entries dropped: ");
                        builder.Append(_droppedEntries);
                        builder.AppendLine(".");
                        _droppedEntries = 0;
                    }
                    foreach (string entry in PendingEntries) builder.AppendLine(entry);
                    payload = builder.ToString();
                    PendingEntries.Clear();
                }

                try
                {
                    Directory.CreateDirectory(_diagnosticsDirectory);
                    long incomingBytes = Encoding.UTF8.GetByteCount(payload);
                    if (File.Exists(_logPath))
                    {
                        long existingBytes = new FileInfo(_logPath).Length;
                        if (existingBytes + incomingBytes > MaxLogBytes)
                        {
                            if (File.Exists(_previousLogPath)) File.Delete(_previousLogPath);
                            File.Move(_logPath, _previousLogPath);
                        }
                    }
                    byte[] bytes = new UTF8Encoding(false).GetBytes(payload);
                    using (FileStream stream = new FileStream(_logPath, FileMode.Append,
                        FileAccess.Write, FileShare.ReadWrite))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    LastError = null;
                }
                catch (Exception ex)
                {
                    LastError = Sanitize(ex.Message);
                    lock (SyncRoot)
                    {
                        string[] lines = payload.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < lines.Length && PendingEntries.Count < MaxBufferedEntries; i++)
                            PendingEntries.Enqueue(lines[i]);
                    }
                }
            }
        }

        public static bool SetEnabled(bool enabled)
        {
            if (enabled == _enabled)
            {
                ConfigManager.SetBool("Diagnostics", "Enabled", enabled);
                return string.IsNullOrEmpty(ConfigManager.LastError);
            }

            if (enabled)
            {
                ConfigManager.SetBool("Diagnostics", "Enabled", true);
                _enabled = true;
                StartSession();
                LogInfo("System", "Diagnostics enabled by user.");
                Flush();
            }
            else
            {
                LogInfo("System", "Diagnostics disabled by user.");
                Flush();
                _enabled = false;
                ConfigManager.SetBool("Diagnostics", "Enabled", false);
                RemoveSessionMarker();
            }
            return string.IsNullOrEmpty(LastError) && string.IsNullOrEmpty(ConfigManager.LastError);
        }

        public static bool SetDetailedLogging(bool enabled)
        {
            _detailedLogging = enabled;
            ConfigManager.SetBool("Diagnostics", "DetailedLogging", enabled);
            if (_enabled) LogInfo("Diagnostics", enabled ? "Detailed logging enabled." : "Detailed logging disabled.");
            return string.IsNullOrEmpty(ConfigManager.LastError);
        }

        public static void ReloadSettings()
        {
            bool enabled = ConfigManager.GetBool("Diagnostics", "Enabled", true);
            _detailedLogging = ConfigManager.GetBool("Diagnostics", "DetailedLogging", false);
            if (enabled && !_enabled)
            {
                _enabled = true;
                StartSession();
            }
            else if (!enabled && _enabled)
            {
                LogInfo("System", "Diagnostics disabled through settings reload.");
                Flush();
                _enabled = false;
                RemoveSessionMarker();
            }
        }

        public static bool TryCreateReport(out string archivePath, out string error)
        {
            archivePath = null;
            error = null;
            if (!EnsurePaths())
            {
                error = LastError ?? "Could not create diagnostics folder.";
                return false;
            }

            try
            {
                Flush();
                File.WriteAllText(Path.Combine(_diagnosticsDirectory, "ReportInfo.txt"),
                    "AvernesTreiner Diagnostics Report" + Environment.NewLine +
                    "Created UTC: " + DateTime.UtcNow.ToString("o") + Environment.NewLine +
                    "Buffer: maximum 256 entries" + Environment.NewLine +
                    "Flush interval: 1 second" + Environment.NewLine +
                    "Current and previous log: maximum 2 MB each" + Environment.NewLine +
                    "No telemetry or automatic upload is performed." + Environment.NewLine,
                    Encoding.UTF8);
                archivePath = Path.Combine(Path.GetDirectoryName(_diagnosticsDirectory),
                    "AvernesTreiner-Diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
                if (File.Exists(archivePath)) File.Delete(archivePath);
                ZipFile.CreateFromDirectory(_diagnosticsDirectory, archivePath, CompressionLevel.Optimal, false);
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                error = Sanitize(ex.Message);
                LastError = error;
                archivePath = null;
                return false;
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (!_enabled) return;
            Exception exception = args.ExceptionObject as Exception;
            if (exception != null) LogException("Unhandled", exception);
            else
            {
                Enqueue("FATAL", "Unhandled", args.ExceptionObject == null ? "Unknown unhandled exception." : args.ExceptionObject.ToString());
                Flush();
            }
        }

        public static void OnScriptAborted()
        {
            if (!_enabled || !_sessionStarted) return;
            LogInfo("System", "Trainer script aborted/unloaded; flushing diagnostics and closing session marker.");
            Flush();
            RemoveSessionMarker();
        }

        private static void OnProcessExit(object sender, EventArgs args)
        {
            if (!_enabled) return;
            LogInfo("System", "Game process is exiting.");
            Flush();
            RemoveSessionMarker();
        }

        private static void RemoveSessionMarker()
        {
            try
            {
                if (!string.IsNullOrEmpty(_sessionMarkerPath) && File.Exists(_sessionMarkerPath))
                    File.Delete(_sessionMarkerPath);
            }
            catch { }
            _sessionStarted = false;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string safe = value;
            try
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(profile)) safe = safe.Replace(profile, "%USERPROFILE%");
            }
            catch { }
            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(documents)) safe = safe.Replace(documents, "%DOCUMENTS%");
            }
            catch { }
            return safe;
        }
    }
}

namespace AvernesTreiner.Modules
{
    public class DiagnosticsModule : IAvernesModule
    {
        public string Name { get { return "Diagnostics"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            UIMenu diagnosticsMenu = menuPool.AddSubMenu(mainMenu,
                Localization.Get("General", "SubmenuDiagnostics", "Diagnostics"));

            var enabledItem = new UIMenuCheckboxItem(
                Localization.Get("Diagnostics", "Enabled", "Diagnostics Enabled"),
                DiagnosticsManager.Enabled,
                Localization.Get("Diagnostics", "EnabledDesc", "Record important trainer events locally."));
            enabledItem.CheckboxEvent += (sender, state) =>
            {
                bool saved = DiagnosticsManager.SetEnabled(state);
                GTA.UI.Notify(saved ? (state ? "~g~Diagnostics enabled." : "~y~Diagnostics disabled.")
                    : "~r~Could not save diagnostics setting: " + ConfigManager.LastError);
            };
            diagnosticsMenu.AddItem(enabledItem);

            var detailedItem = new UIMenuCheckboxItem(
                Localization.Get("Diagnostics", "Detailed", "Detailed Logging"),
                DiagnosticsManager.DetailedLogging,
                Localization.Get("Diagnostics", "DetailedDesc", "Record target transitions; no per-frame logging."));
            detailedItem.CheckboxEvent += (sender, state) =>
            {
                bool saved = DiagnosticsManager.SetDetailedLogging(state);
                GTA.UI.Notify(saved ? (state ? "~g~Detailed logging enabled." : "~y~Detailed logging disabled.")
                    : "~r~Could not save detailed logging setting.");
            };
            diagnosticsMenu.AddItem(detailedItem);

            var exportItem = new UIMenuItem(
                Localization.Get("Diagnostics", "Export", "Create diagnostics ZIP"),
                Localization.Get("Diagnostics", "ExportDesc", "Creates a local ZIP report in scripts/AvermesBags."));
            exportItem.Activated += (sender, selected) =>
            {
                string archivePath;
                string error;
                if (DiagnosticsManager.TryCreateReport(out archivePath, out error))
                    GTA.UI.Notify("~g~Diagnostics report created: " + Path.GetFileName(archivePath));
                else
                    GTA.UI.Notify("~r~Diagnostics report failed: " + error);
            };
            diagnosticsMenu.AddItem(exportItem);
        }

        public void ReloadSettings() { DiagnosticsManager.ReloadSettings(); }
        public void OnTick() { }
    }
}
