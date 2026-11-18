using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AvernesSowtware
{
    public sealed partial class AvernesSowtwareScript
    {

        private readonly object _storageSync = new object();
        private readonly object _settingsSaveSync = new object();
        private string _pendingSettingsSnapshot;
        private bool _settingsSaveWorkerRunning;
        private const string BuiltInVehicleDatabaseSha256 = "7646c76b8de16967345a247968d22ef7974cf52e9175899ccd7aebcf7254cdf3";

        private static string ResolveWritableDataDirectory(string scriptsDirectory)
        {
            string scriptsData = Path.Combine(scriptsDirectory, "AvernesSowtware");
            if (CanWriteDirectory(scriptsData)) return scriptsData;

            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!String.IsNullOrEmpty(localData))
            {
                string fallback = Path.Combine(localData, "AvernesSowtware", "GTA V Legacy");
                if (CanWriteDirectory(fallback)) return fallback;
            }

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!String.IsNullOrEmpty(documents))
            {
                string fallback = Path.Combine(documents, "AvernesSowtware", "GTA V Legacy");
                if (CanWriteDirectory(fallback)) return fallback;
            }

            return scriptsData;
        }

        private static bool CanWriteDirectory(string directory)
        {
            string probe = null;
            try
            {
                Directory.CreateDirectory(directory);
                probe = Path.Combine(directory, ".write_test_" + Guid.NewGuid().ToString("N") + ".tmp");
                using (FileStream stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] marker = Encoding.UTF8.GetBytes("AvernesSowtware storage access test");
                    stream.Write(marker, 0, marker.Length);
                    stream.Flush(true);
                }
                File.Delete(probe);
                return true;
            }
            catch
            {
                try { if (!String.IsNullOrEmpty(probe) && File.Exists(probe)) File.Delete(probe); } catch { }
                return false;
            }
        }

        private static string GetSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder text = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static string GetSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder text = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static void WriteChecksumSidecar(string path, string hash)
        {
            string sidecar = path + ".sha256";
            string temporary = sidecar + ".tmp";
            byte[] bytes = Encoding.ASCII.GetBytes(hash + Environment.NewLine);
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                string written = File.ReadAllText(temporary).Trim();
                if (!String.Equals(written, hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Checksum sidecar verification failed: " + sidecar);
                if (File.Exists(sidecar)) File.Replace(temporary, sidecar, null);
                else File.Move(temporary, sidecar);
            }
            catch
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                throw;
            }
        }

        private bool TryAtomicWriteFile(string path, string content, out string error)
        {
            error = null;
            if (String.IsNullOrEmpty(path)) { error = "Target path is empty."; return false; }
            byte[] bytes = new UTF8Encoding(false).GetBytes(content ?? String.Empty);
            lock (_storageSync)
            {
                string directory = Path.GetDirectoryName(path);
                string temporary = path + ".tmp";
                string backup = path + ".bak";
                string backupTemporary = backup + ".tmp";
                bool backupVerified = false;
                try
                {
                    if (String.IsNullOrEmpty(directory)) throw new InvalidOperationException("Target directory is unavailable.");
                    Directory.CreateDirectory(directory);
                    using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }

                    if (!new FileInfo(temporary).Exists || new FileInfo(temporary).Length != bytes.Length ||
                        !String.Equals(GetSha256(temporary), GetSha256(bytes), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Temporary file size/hash verification failed.");

                    if (File.Exists(path))
                    {
                        // Refresh the last-known-good backup before replacing the active file.
                        File.Copy(path, backupTemporary, true);
                        if (new FileInfo(path).Length != new FileInfo(backupTemporary).Length ||
                            !String.Equals(GetSha256(path), GetSha256(backupTemporary), StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Backup verification failed; active file was not replaced.");
                        if (File.Exists(backup)) File.Replace(backupTemporary, backup, null);
                        else File.Move(backupTemporary, backup);
                        WriteChecksumSidecar(backup, GetSha256(backup));
                        backupVerified = true;
                        File.Replace(temporary, path, null);
                    }
                    else File.Move(temporary, path);

                    string finalHash = GetSha256(path);
                    if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length ||
                        !String.Equals(finalHash, GetSha256(bytes), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Final file verification failed.");
                    WriteChecksumSidecar(path, finalHash);
                    return true;
                }
                catch (ThreadAbortException)
                {
                    // A runtime abort must propagate; the active file remains protected by the verified backup.
                    throw;
                }
                catch (Exception ex)
                {
                    error = ex.GetType().FullName + ": " + ex.Message;
                    try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                    try { if (File.Exists(backupTemporary)) File.Delete(backupTemporary); } catch { }
                    if (backupVerified && File.Exists(backup))
                    {
                        try
                        {
                            string restore = path + ".restore.tmp";
                            File.Copy(backup, restore, true);
                            if (String.Equals(GetSha256(restore), GetSha256(backup), StringComparison.OrdinalIgnoreCase))
                            {
                                if (File.Exists(path)) File.Replace(restore, path, null); else File.Move(restore, path);
                                WriteChecksumSidecar(path, GetSha256(path));
                            }
                            else if (File.Exists(restore)) File.Delete(restore);
                        }
                        catch { }
                    }
                    return false;
                }
            }
        }

        private void QueueSettingsSnapshot(string snapshot)
        {
            lock (_settingsSaveSync)
            {
                _pendingSettingsSnapshot = snapshot;
                if (_settingsSaveWorkerRunning) return;
                _settingsSaveWorkerRunning = true;
            }

            bool queued = ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    while (true)
                    {
                        string next;
                        lock (_settingsSaveSync)
                        {
                            next = _pendingSettingsSnapshot;
                            _pendingSettingsSnapshot = null;
                            if (next == null)
                            {
                                _settingsSaveWorkerRunning = false;
                                return;
                            }
                        }

                        string error;
                        if (!TryAtomicWriteFile(_configPath, next, out error))
                            AppendLogFile(_errorLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                                " [ERROR] SaveSettings: " + error + Environment.NewLine);
                    }
                }
                catch (ThreadAbortException)
                {
                    lock (_settingsSaveSync) _settingsSaveWorkerRunning = false;
                    AppendLogFile(_errorLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                        " [WARN] Settings save worker was stopped during runtime shutdown." + Environment.NewLine);
                    throw;
                }
                catch (Exception ex)
                {
                    string pending = null;
                    lock (_settingsSaveSync)
                    {
                        _settingsSaveWorkerRunning = false;
                        pending = _pendingSettingsSnapshot;
                        _pendingSettingsSnapshot = null;
                    }
                    AppendLogFile(_errorLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                        " [ERROR] Settings save worker: " + ex + Environment.NewLine);
                    if (pending != null) QueueSettingsSnapshot(pending);
                }
            });

            if (!queued)
            {
                lock (_settingsSaveSync) _settingsSaveWorkerRunning = false;
                AppendLogFile(_errorLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                    " [ERROR] Settings save worker could not be queued." + Environment.NewLine);
            }
        }

        private void EnsureCriticalFileIntegrity()
        {
            try
            {
                EnsureVehicleDatabaseIntegrity(_vehicleCatalogPath, _legacyVehicleCatalogPath);
                EnsureVehicleDatabaseIntegrity(_legacyVehicleCatalogPath, _vehicleCatalogPath);
                EnsureSettingsIntegrity();
            }
            catch (Exception ex) { LogException("Critical file integrity", ex); }
        }

        private void EnsureVehicleDatabaseIntegrity(string path, string alternatePath)
        {
            try
            {
                bool exists = File.Exists(path);
                bool valid = exists && IsSaneVehicleDatabase(path);
                string sidecar = path + ".sha256";
                if (valid && File.Exists(sidecar))
                {
                    try
                    {
                        string expected = File.ReadAllText(sidecar).Trim();
                        valid = expected.Length == 64 && String.Equals(expected, GetSha256(path), StringComparison.OrdinalIgnoreCase);
                    }
                    catch { valid = false; }
                }
                else if (valid && !File.Exists(sidecar))
                {
                    // Existing user-edited databases are preserved on first 1.3.5 run.
                    WriteChecksumSidecar(path, GetSha256(path));
                    return;
                }
                else if (!exists) valid = false;

                if (valid) return;

                string builtIn = Path.Combine(Path.GetDirectoryName(path), "vehicles_builtin.json");
                string[] candidates = new string[] { path + ".bak", alternatePath, alternatePath + ".bak", builtIn };
                for (int i = 0; i < candidates.Length; i++)
                {
                    string candidate = candidates[i];
                    if (String.IsNullOrEmpty(candidate) || !File.Exists(candidate) || !IsSaneVehicleDatabase(candidate)) continue;
                    bool trusted = false;
                    try
                    {
                        string candidateHash = GetSha256(candidate);
                        string candidateSidecar = candidate + ".sha256";
                        if (File.Exists(candidateSidecar))
                            trusted = String.Equals(File.ReadAllText(candidateSidecar).Trim(), candidateHash, StringComparison.OrdinalIgnoreCase);
                        else
                            trusted = String.Equals(candidateHash, BuiltInVehicleDatabaseSha256, StringComparison.OrdinalIgnoreCase);
                    }
                    catch { trusted = false; }
                    if (!trusted) continue;

                    string restore = path + ".restore.tmp";
                    File.Copy(candidate, restore, true);
                    string restoreHash = GetSha256(restore);
                    if (restoreHash != GetSha256(candidate)) throw new InvalidDataException("Restored vehicle database failed SHA-256 verification.");
                    if (File.Exists(path)) File.Replace(restore, path, null); else File.Move(restore, path);
                    WriteChecksumSidecar(path, restoreHash);
                    Log("Restored vehicle database from verified copy: " + candidate);
                    return;
                }

                LogFailure("Vehicle database checksum", "Vehicle database is missing or damaged and no verified backup was available: " + path);
            }
            catch (Exception ex) { LogException("Vehicle database checksum " + path, ex); }
        }

        private bool IsSaneVehicleDatabase(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists || info.Length < 32 || info.Length > 64 * 1024 * 1024) return false;
                string prefix;
                using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
                    prefix = reader.ReadToEnd();
                return prefix.IndexOf("\"model\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    prefix.TrimStart().StartsWith("[", StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private bool IsSaneSettingsFile(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists || info.Length <= 16 || info.Length > 128 * 1024) return false;
                string text = File.ReadAllText(path);
                return text.IndexOf("[PLAYER]", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    text.IndexOf("GODMODE=", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private void EnsureSettingsIntegrity()
        {
            string targetPath = File.Exists(_configPath) ? _configPath : _sourceConfigPath;
            if (!File.Exists(targetPath)) return;
            string sidecar = targetPath + ".sha256";
            bool checksumExists = File.Exists(sidecar);
            bool valid = IsSaneSettingsFile(targetPath);
            if (checksumExists)
            {
                try
                {
                    string expected = File.ReadAllText(sidecar).Trim();
                    valid = valid && expected.Length == 64 &&
                        String.Equals(expected, GetSha256(targetPath), StringComparison.OrdinalIgnoreCase);
                }
                catch { valid = false; }
            }

            if (!valid)
            {
                string backup = targetPath + ".bak";
                string backupHashPath = backup + ".sha256";
                bool backupValid = IsSaneSettingsFile(backup);
                try
                {
                    if (backupValid && File.Exists(backupHashPath))
                        backupValid = String.Equals(File.ReadAllText(backupHashPath).Trim(), GetSha256(backup), StringComparison.OrdinalIgnoreCase);
                }
                catch { backupValid = false; }
                if (backupValid)
                {
                    string restore = targetPath + ".restore.tmp";
                    File.Copy(backup, restore, true);
                    string backupHash = GetSha256(backup);
                    if (new FileInfo(restore).Length != new FileInfo(backup).Length ||
                        !String.Equals(GetSha256(restore), backupHash, StringComparison.OrdinalIgnoreCase))
                    {
                        try { if (File.Exists(restore)) File.Delete(restore); } catch { }
                        throw new InvalidDataException("Settings backup copy failed size/SHA-256 verification.");
                    }
                    if (File.Exists(targetPath)) File.Replace(restore, targetPath, null); else File.Move(restore, targetPath);
                    if (!String.Equals(GetSha256(targetPath), backupHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Restored settings file failed SHA-256 verification.");
                    WriteChecksumSidecar(targetPath, backupHash);
                    Log("Restored settings from the verified .bak file after integrity check failure.");
                }
                else LogFailure("Settings checksum", "Settings file is invalid and no verified backup is available; original file was preserved.");
            }
            else if (!checksumExists) WriteChecksumSidecar(targetPath, GetSha256(targetPath));
        }

        private void CleanupObsoleteFiles()
        {
            try
            {
                if (!Directory.Exists(_dataDirectory)) return;
                DateTime cutoff = DateTime.UtcNow.AddDays(-30);
                List<string> files = new List<string>();
                bool traversalComplete = true;
                CollectFilesWithoutReparsePoints(_dataDirectory, files, ref traversalComplete);
                for (int i = 0; i < files.Count; i++)
                {
                    string path = files[i];
                    string name = Path.GetFileName(path);
                    if (path.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith(Path.Combine(_dataDirectory, "LegacyMigration"), StringComparison.OrdinalIgnoreCase)) continue;
                    FileInfo info = new FileInfo(path);
                    if (info.LastWriteTimeUtc >= cutoff) continue;

                    if (path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                    {
                        string primary = path.Substring(0, path.Length - 4);
                        if (!File.Exists(primary)) continue;
                        string[] siblingBackups = Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(primary) + "*.bak", SearchOption.TopDirectoryOnly);
                        if (siblingBackups.Length <= 1) continue;
                        File.Delete(path);
                        string checksum = path + ".sha256";
                        if (File.Exists(checksum)) File.Delete(checksum);
                        continue;
                    }

                    Match match = System.Text.RegularExpressions.Regex.Match(name, "^(.*) \\(\\d+\\)(\\.[^.]+)$");
                    if (match.Success && !IsProtectedDataPath(path))
                    {
                        string original = Path.Combine(Path.GetDirectoryName(path), match.Groups[1].Value + match.Groups[3].Value);
                        if (File.Exists(original) && info.Length == new FileInfo(original).Length &&
                            String.Equals(GetSha256(path), GetSha256(original), StringComparison.OrdinalIgnoreCase))
                            File.Delete(path);
                    }
                }
            }
            catch (Exception ex) { LogException("CleanupObsoleteFiles", ex); }
        }

        private bool IsProtectedDataPath(string path)
        {
            string[] protectedPaths = new string[] { _configPath, _sourceConfigPath, _vehicleCatalogPath,
                _favoriteLocationsPath, _savedVehiclesPath, _atlasProgressPath, _outfitsPath };
            for (int i = 0; i < protectedPaths.Length; i++)
                if (String.Equals(path, protectedPaths[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void LoadSettings()
        {
            string readPath = File.Exists(_configPath) ? _configPath : _sourceConfigPath;
            Log("Loading settings from " + readPath + (File.Exists(_configPath) ? " (Extended settings take priority over Config.ini)." : " (legacy Config.ini fallback)."));
            Dictionary<string, string> values = ReadIni(readPath);

            _settings.GodMode = GetBool(values, "PLAYER.GODMODE", true);
            _settings.AutoGiveWeapons = GetBool(values, "PLAYER.AUTOGIVEWEAPONS", true);
            _settings.InfiniteAmmo = GetBool(values, "PLAYER.INFINITEAMMO", true);
            _settings.NoReload = GetBool(values, "PLAYER.NORELOAD", true);
            _settings.NeverWanted = GetBool(values, "PLAYER.NEVERWANTED", true);
            _settings.BodyguardInvincible = GetBool(values, "BODYGUARDS.INVINCIBLE", false);
            _settings.BodyguardHealth = ClampInt(GetInt(values, "BODYGUARDS.HEALTH", 250), 100, 1000);
            int savedBodyguardArmour = GetInt(values, "BODYGUARDS.ARMOUR", 0);
            int bodyguardArmourMigration = GetInt(values, "BODYGUARDS.ARMOURDEFAULTMIGRATION", 0);
            if (bodyguardArmourMigration < 1 && savedBodyguardArmour == 100) savedBodyguardArmour = 0;
            _settings.BodyguardArmour = ClampInt(savedBodyguardArmour, 0, 100);
            _settings.InfiniteStamina = GetBool(values, "PLAYER.INFINITESTAMINA", true);
            _settings.AutoDeployParachute = GetBool(values, "PLAYER.AUTODEPLOYPARACHUTE", true);
            _settings.InfiniteSpecialAbility = GetBool(values, "PLAYER.INFINITESPECIALABILITY", false);
            _settings.UnlimitedOxygen = GetBool(values, "PLAYER.UNLIMITEDOXYGEN", true);
            _settings.FastRun = GetBool(values, "PLAYER.FASTRUN", true);
            _settings.FastSwim = GetBool(values, "PLAYER.FASTSWIM", true);
            _settings.SuperJump = GetBool(values, "PLAYER.SUPERJUMP", false);
            _settings.CopsIgnore = GetBool(values, "PLAYER.COPSIGNORE", false);
            _settings.Invisible = GetBool(values, "PLAYER.INVISIBLE", false);
            _settings.NoRagdoll = GetBool(values, "PLAYER.NORAGDOLL", false);
            _settings.FranklinHairProtection = GetBool(values, "PLAYER.FRANKLINHAIRPROTECTION", true);
            _settings.AntiEject = GetBool(values, "VEHICLE.ANTIEJECT", true);
            _settings.VehicleGodMode = GetBool(values, "VEHICLE.VEHICLEGODMODE", true);
            _settings.VehicleRockets = GetBool(values, "VEHICLE.VEHICLEROCKETS", false);
            _settings.Nitro = GetBool(values, "VEHICLE.NITRO", false);
            _settings.NitroPower = ClampFloat(GetFloat(values, "VEHICLE.NITROPOWER", 1.50f), 0.50f, 8.00f);
            _settings.DriftMode = GetBool(values, "VEHICLE.DRIFTMODE", false);
            _settings.Speedometer = GetBool(values, "VEHICLE.SPEEDOMETER", false);
            _settings.BulletproofTires = GetBool(values, "VEHICLE.BULLETPROOFTIRES", false);
            _settings.HornJump = GetBool(values, "VEHICLE.HORNJUMP", false);
            _settings.SpeedUnitMph = GetBool(values, "VEHICLE.SPEEDUNITMPH", false);
            _settings.ExplosiveAmmo = GetBool(values, "WEAPONS.EXPLOSIVEAMMO", false);
            _settings.FireAmmo = GetBool(values, "WEAPONS.FIREAMMO", false);
            _settings.SuperDamage = GetBool(values, "WEAPONS.SUPERDAMAGE", false);
            _settings.NoClip = GetBool(values, "POWERS.NOCLIP", false);
            _settings.NightVision = GetBool(values, "POWERS.NIGHTVISION", false);
            _settings.ThermalVision = GetBool(values, "POWERS.THERMALVISION", false);
            _settings.FreezeTime = GetBool(values, "WORLD.FREEZETIME", false);
            _settings.LockWeather = GetBool(values, "WORLD.LOCKWEATHER", true);
            _settings.Weather = GetString(values, "WORLD.WEATHER", "EXTRASUNNY");
            _settings.Weather = NormalizeWeather(_settings.Weather);

            _settings.MenuWidth = ClampFloat(GetFloat(values, "MENU.WIDTH", 0.340f), 0.28f, 0.44f);
            _settings.MenuScale = ClampFloat(GetFloat(values, "MENU.SCALE", 1.0f), 0.75f, 1.30f);
            _settings.MenuRows = ClampInt(GetInt(values, "MENU.VISIBLEROWS", 9), 6, 12);
            _settings.MenuX = ClampFloat(GetFloat(values, "MENU.POSITIONX", 0.755f), 0.005f, GetMaximumMenuX());
            _settings.MenuY = ClampFloat(GetFloat(values, "MENU.POSITIONY", 0.006f), 0.005f, GetMaximumMenuY());
            _settings.MenuFontScale = ClampFloat(GetFloat(values, "MENU.FONTSCALE", 1.0f), 0.70f, 1.40f);
            _settings.MenuFontStyle = ClampInt(GetInt(values, "MENU.FONTSTYLE", 0), 0, 3);
            _settings.MenuTheme = ClampInt(GetInt(values, "MENU.THEME", 0), 0, 4);
            _settings.MenuOpacity = ClampInt(GetInt(values, "MENU.OPACITY", 225), 100, 255);
        }
        private void SaveSettings()
        {
            try
            {
                List<string> lines = new List<string>();
                lines.Add("; AvernesSowtware settings. Original Config.ini is not modified.");
                lines.Add("[PLAYER]");
                lines.Add("GODMODE=" + _settings.GodMode.ToString().ToLowerInvariant());
                lines.Add("AUTOGIVEWEAPONS=" + _settings.AutoGiveWeapons.ToString().ToLowerInvariant());
                lines.Add("INFINITEAMMO=" + _settings.InfiniteAmmo.ToString().ToLowerInvariant());
                lines.Add("NORELOAD=" + _settings.NoReload.ToString().ToLowerInvariant());
                lines.Add("NEVERWANTED=" + _settings.NeverWanted.ToString().ToLowerInvariant());
                lines.Add("INFINITESTAMINA=" + _settings.InfiniteStamina.ToString().ToLowerInvariant());
                lines.Add("AUTODEPLOYPARACHUTE=" + _settings.AutoDeployParachute.ToString().ToLowerInvariant());
                lines.Add("INFINITESPECIALABILITY=" + _settings.InfiniteSpecialAbility.ToString().ToLowerInvariant());
                lines.Add("UNLIMITEDOXYGEN=" + _settings.UnlimitedOxygen.ToString().ToLowerInvariant());
                lines.Add("FASTRUN=" + _settings.FastRun.ToString().ToLowerInvariant());
                lines.Add("FASTSWIM=" + _settings.FastSwim.ToString().ToLowerInvariant());
                lines.Add("SUPERJUMP=" + _settings.SuperJump.ToString().ToLowerInvariant());
                lines.Add("COPSIGNORE=" + _settings.CopsIgnore.ToString().ToLowerInvariant());
                lines.Add("INVISIBLE=" + _settings.Invisible.ToString().ToLowerInvariant());
                lines.Add("NORAGDOLL=" + _settings.NoRagdoll.ToString().ToLowerInvariant());
                lines.Add("FRANKLINHAIRPROTECTION=" + _settings.FranklinHairProtection.ToString().ToLowerInvariant());
                lines.Add("");
                lines.Add("[BODYGUARDS]");
                lines.Add("INVINCIBLE=" + _settings.BodyguardInvincible.ToString().ToLowerInvariant());
                lines.Add("HEALTH=" + _settings.BodyguardHealth.ToString(CultureInfo.InvariantCulture));
                lines.Add("ARMOUR=" + _settings.BodyguardArmour.ToString(CultureInfo.InvariantCulture));
                lines.Add("ARMOURDEFAULTMIGRATION=1");
                lines.Add("");
                lines.Add("[WEAPONS]");
                lines.Add("EXPLOSIVEAMMO=" + _settings.ExplosiveAmmo.ToString().ToLowerInvariant());
                lines.Add("FIREAMMO=" + _settings.FireAmmo.ToString().ToLowerInvariant());
                lines.Add("SUPERDAMAGE=" + _settings.SuperDamage.ToString().ToLowerInvariant());
                lines.Add("");
                lines.Add("[POWERS]");
                lines.Add("NOCLIP=" + _settings.NoClip.ToString().ToLowerInvariant());
                lines.Add("NIGHTVISION=" + _settings.NightVision.ToString().ToLowerInvariant());
                lines.Add("THERMALVISION=" + _settings.ThermalVision.ToString().ToLowerInvariant());
                lines.Add("");
                lines.Add("[VEHICLE]");
                lines.Add("VEHICLEGODMODE=" + _settings.VehicleGodMode.ToString().ToLowerInvariant());
                lines.Add("VEHICLEROCKETS=" + _settings.VehicleRockets.ToString().ToLowerInvariant());
                lines.Add("ANTIEJECT=" + _settings.AntiEject.ToString().ToLowerInvariant());
                lines.Add("NITRO=" + _settings.Nitro.ToString().ToLowerInvariant());
                lines.Add("NITROPOWER=" + _settings.NitroPower.ToString("0.00", CultureInfo.InvariantCulture));
                lines.Add("DRIFTMODE=" + _settings.DriftMode.ToString().ToLowerInvariant());
                lines.Add("SPEEDOMETER=" + _settings.Speedometer.ToString().ToLowerInvariant());
                lines.Add("BULLETPROOFTIRES=" + _settings.BulletproofTires.ToString().ToLowerInvariant());
                lines.Add("HORNJUMP=" + _settings.HornJump.ToString().ToLowerInvariant());
                lines.Add("SPEEDUNITMPH=" + _settings.SpeedUnitMph.ToString().ToLowerInvariant());
                lines.Add("AUTOREPAIRVEHICLE=false");
                lines.Add("");
                lines.Add("[WORLD]");
                lines.Add("FREEZETIME=" + _settings.FreezeTime.ToString().ToLowerInvariant());
                lines.Add("LOCKWEATHER=" + _settings.LockWeather.ToString().ToLowerInvariant());
                lines.Add("WEATHER=" + _settings.Weather);
                lines.Add("");
                lines.Add("[MENU]");
                lines.Add("POSITIONX=" + _settings.MenuX.ToString("0.000", CultureInfo.InvariantCulture));
                lines.Add("POSITIONY=" + _settings.MenuY.ToString("0.000", CultureInfo.InvariantCulture));
                lines.Add("WIDTH=" + _settings.MenuWidth.ToString("0.000", CultureInfo.InvariantCulture));
                lines.Add("SCALE=" + _settings.MenuScale.ToString("0.00", CultureInfo.InvariantCulture));
                lines.Add("FONTSCALE=" + _settings.MenuFontScale.ToString("0.00", CultureInfo.InvariantCulture));
                lines.Add("FONTSTYLE=" + _settings.MenuFontStyle.ToString(CultureInfo.InvariantCulture));
                lines.Add("THEME=" + _settings.MenuTheme.ToString(CultureInfo.InvariantCulture));
                lines.Add("OPACITY=" + _settings.MenuOpacity.ToString(CultureInfo.InvariantCulture));
                lines.Add("VISIBLEROWS=" + _settings.MenuRows.ToString(CultureInfo.InvariantCulture));
                string snapshot = String.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine;
                QueueSettingsSnapshot(snapshot);
            }
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppendLogFile(_errorLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                    " [ERROR] SaveSettings snapshot: " + ex + Environment.NewLine);
            }
        }
        private Dictionary<string, string> ReadIni(string path)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return values;
            string section = "";
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
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
                string fullKey = section + "." + key;
                if (values.ContainsKey(fullKey))
                    Log("Duplicate INI key detected in " + path + ": " + fullKey + ". The last value is used.");
                values[fullKey] = value;
            }
            return values;
        }
        private bool GetBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string value;
            bool parsed;
            return values.TryGetValue(key, out value) && Boolean.TryParse(value, out parsed) ? parsed : fallback;
        }
        private int GetInt(Dictionary<string, string> values, string key, int fallback)
        {
            string value;
            int parsed;
            return values.TryGetValue(key, out value) && Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }
        private float GetFloat(Dictionary<string, string> values, string key, float fallback)
        {
            string value;
            float parsed;
            return values.TryGetValue(key, out value) && Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && !Single.IsNaN(parsed) && !Single.IsInfinity(parsed) ? parsed : fallback;
        }
        private string GetString(Dictionary<string, string> values, string key, string fallback)
        {
            string value;
            return values.TryGetValue(key, out value) && !String.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
        }
        private string NormalizeWeather(string weather)
        {
            string[] allowed = new string[] { "EXTRASUNNY", "CLEAR", "CLOUDS", "SMOG", "FOGGY", "OVERCAST", "RAIN", "THUNDER", "CLEARING", "NEUTRAL", "SNOW", "BLIZZARD", "SNOWLIGHT", "XMAS", "HALLOWEEN" };
            for (int i = 0; i < allowed.Length; i++)
                if (String.Equals(weather, allowed[i], StringComparison.OrdinalIgnoreCase)) return allowed[i];
            return "EXTRASUNNY";
        }
    }
}
