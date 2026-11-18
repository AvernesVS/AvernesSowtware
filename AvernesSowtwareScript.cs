using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Web.Script.Serialization;
using System.Threading;
using System.Text;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

namespace AvernesSowtware
{
    public sealed partial class AvernesSowtwareScript : Script
    {
        private const string ProductName = "AvernesSowtware";
        private const string ProductVersion = "1.4.5";
        private const int AmmoPerWeapon = 9999;
        private const int PremiumWeaponTint = 7;

        private const int StoryCashLimit = Int32.MaxValue;

        private readonly string _gameDirectory;
        private readonly string _scriptsDirectory;
        private readonly string _dataDirectory;
        private readonly string _legacyDataDirectory;
        private readonly string _vehiclesDirectory;
        private readonly string _logDirectory;
        private readonly string _errorLogPath;
        private readonly string _crashReportPath;
        private readonly object _logSync = new object();
        private readonly string _sourceConfigPath;
        private readonly string _configPath;
        private readonly string _addonPath;
        private readonly string _vehicleCatalogPath;
        private readonly string _legacyVehicleCatalogPath;
        private readonly string _favoriteLocationsPath;
        private readonly string _savedVehiclesPath;
        private readonly string _atlasProgressPath;
        private readonly string _outfitsPath;
        private readonly string _hotkeysPath;
        private readonly string _logPath;
        private readonly string _sessionMarkerPath;
        private readonly TrainerSettings _settings;
        private readonly Dictionary<string, List<MenuEntry>> _pages;
        private readonly Stack<string> _pageStack;
        private readonly List<AddonVehicle> _addonVehicles;
        private readonly List<AddonVehicle> _vehicleCatalog;
        private readonly List<FavoriteLocation> _favoriteLocations;
        private readonly List<SavedVehicle> _savedVehicles;
        private readonly List<int> _trainerSpawnedObjectHandles = new List<int>();
        private HashSet<uint> _looseLitterModelHashes;
        private readonly List<string> _vehicleCategoryPageKeys;
        private readonly string[] _weaponNames;
        private readonly string[] _weaponComponentNames;

        private bool _menuOpen;
        private string _currentPage;
        private int _selectedIndex;
        private int _scrollOffset;
        private bool _autoLoadoutGranted;
        private bool _originalModelCaptured;
        private uint _originalModelHash;
        private readonly List<int> _bodyguards = new List<int>();
        private readonly Queue<int> _pendingBodyguardDismissals = new Queue<int>();
        private readonly HashSet<int> _queuedBodyguardDismissalHandles = new HashSet<int>();
        private readonly HashSet<int> _bodyguardDismissalDetachedHandles = new HashSet<int>();
        private DateTime _nextBodyguardDismissalUtc = DateTime.MinValue;
        private DateTime _bodyguardGroupCleanupNotBeforeUtc = DateTime.MinValue;
        private bool _bodyguardGroupCleanupPending;
        private bool _bodyguardGroupHasDeadMember;
        private int _lastWeatherApplyTick;
        private bool _clockPauseWasSet;
        private bool _weatherWasLocked;
        private bool _neverWantedMaxLevelCaptured;
        private int _previousMaxWantedLevel = 5;
        private bool _militaryIgnoreApplied;
        private bool _armyVehicleDispatchSuppressed;
        private DateTime _nextFortZancudoDispatchRefreshUtc = DateTime.MinValue;
        private DateTime _nextMilitaryCombatScanUtc = DateTime.MinValue;
        private bool _militaryRelationshipCaptured;
        private uint _militaryRelationshipPlayerGroup;
        private uint _militaryRelationshipArmyGroup;
        private int _militaryRelationshipOriginalArmyToPlayer;
        private int _militaryRelationshipOriginalPlayerToArmy;
        private bool _infiniteAmmoWasApplied;
        private int _lastProtectedVehicleHandle;
        private int _lastAppliedVehicleSettingsHandle;
        private bool _lastVehicleGodModeSetting;
        private bool _lastVehicleBulletproofTiresSetting;
        private bool _vehicleSettingsCacheValid;
        private DateTime _nextVehicleSettingsRefreshUtc = DateTime.MinValue;
        private bool _aborted;
        private int _addonIndex;
        private string _lastLoggedError;
        private DateTime _lastLoggedErrorUtc;
        private DateTime _lastErrorNotificationUtc;
        private DateTime _hideCashChangeUntilUtc;
        private bool _vehicleSearchInputPending;
        private string _vehicleSearchQuery;
        private int _pendingVehicleDeleteHandle;
        private bool _vehicleSpawnPending;
        private int _pendingVehicleModelHash;
        private string _pendingVehicleModelName;
        private string _pendingVehicleDisplayName;
        private DateTime _pendingVehicleSpawnDeadlineUtc;
        private int _bodyguardGroupId = -1;
        private uint _bodyguardRelationshipGroupHash;
        private int _lastBodyguardFollowTick;
        private int _lastBodyguardAimTick;
        private int _lastBodyguardAimTargetHandle;
        private int _bodyguardWeaponIndex = 5;
        private bool _bodyguardUseMicroSmg;
        private int _bodyguardPlayerVehicleHandle;
        private readonly Dictionary<int, int> _bodyguardBoardingVehicleHandles = new Dictionary<int, int>();
        private readonly Dictionary<int, DateTime> _bodyguardBoardingDeadlinesUtc = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, DateTime> _bodyguardBoardingEnterAtUtc = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, int> _bodyguardBoardingSeats = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _bodyguardBlips = new Dictionary<int, int>();
        private static readonly string[] BodyguardWeaponModels = new string[] {
            "WEAPON_PISTOL", "WEAPON_COMBATPISTOL", "WEAPON_SMG", "WEAPON_MICROSMG",
            "WEAPON_ASSAULTRIFLE", "WEAPON_CARBINERIFLE", "WEAPON_SPECIALCARBINE",
            "WEAPON_ADVANCEDRIFLE", "WEAPON_PUMPSHOTGUN", "WEAPON_HEAVYSNIPER"
        };
        private static readonly string[] BodyguardWeaponLabels = new string[] {
            "Pistol", "Combat Pistol", "SMG", "Micro SMG", "Assault Rifle",
            "Carbine Rifle", "Special Carbine", "Advanced Rifle", "Pump Shotgun", "Heavy Sniper"
        };
        private bool _noClipWasActive;
        private bool _nitroTrailerWarningShown;
        private int _nitroVehicleHandle;
        private DateTime _lastNitroAccelerationUtc = DateTime.MinValue;
        private bool _parachuteAutoDeploymentRequested;
        private int _antiEjectPedHandle;
        private bool _antiEjectFlagCaptured;
        private bool _antiEjectOriginalWindshieldFlag;
        private bool _vehicleRocketFirePending;
        private bool _vehicleRocketAssetRequested;
        private DateTime _lastVehicleRocketUtc = DateTime.MinValue;
        private bool _franklinHairCaptured;
        private int _franklinHairDrawable = -1;
        private int _franklinHairTexture;
        private int _franklinHairPalette;
        private DateTime _franklinHairRestoreAtUtc = DateTime.MinValue;
        private readonly Dictionary<int, uint> _trainerSpawnedVehicleHashes = new Dictionary<int, uint>();
        private readonly Dictionary<int, DateTime> _trainerSpawnedVehicleCreatedUtc = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, DateTime> _trainerVehicleDeleteRetryUtc = new Dictionary<int, DateTime>();
        private DateTime _nextVehicleSpawnAllowedUtc = DateTime.MinValue;
        private DateTime _nextVehicleRegistrySweepUtc = DateTime.MinValue;
        private SavedVehicle _pendingSavedVehicle;
        private bool _groundTeleportPending;
        private bool _groundLoadSceneStarted;
        private TeleportPoint _pendingHistoryRollbackTarget;
        private float _groundTeleportZHint;
        private int _groundTeleportEntityHandle;
        private float _groundTeleportX;
        private float _groundTeleportY;
        private DateTime _groundTeleportDeadlineUtc;
        private bool _skinSnapshotCaptured;
        private bool _pendingSkinStateRestore;
        private bool _clearSkinSnapshotAfterRestore;
        private int _suppressPauseControlsFrames;
        private int _skinRestoreFrames;
        private Vector3 _skinRestorePosition;
        private float _skinRestoreHeading;
        private int _skinRestoreHealth;
        private int _skinRestoreArmour;
        private uint _skinSelectedWeapon;
        private readonly List<WeaponSnapshot> _skinWeaponSnapshot = new List<WeaponSnapshot>();

        private void MigrateLegacyRuntimeData()
        {
            MigrateInstalledDataToWritableLocation();
            bool hasLegacyFolder = Directory.Exists(_legacyDataDirectory);
            string rootLegacyIni = Path.Combine(_scriptsDirectory, "YnixTrainerExtended.ini");
            if (!hasLegacyFolder && !File.Exists(rootLegacyIni))
            {
                TryDeleteLegacyRuntimeBinary();
                return;
            }

            bool complete = true;
            if (hasLegacyFolder)
            {
                try
                {
                    List<string> legacyFiles = new List<string>();
                    CollectFilesWithoutReparsePoints(_legacyDataDirectory, legacyFiles, ref complete);
                    for (int i = 0; i < legacyFiles.Count; i++)
                    {
                        string source = legacyFiles[i];
                        string relative = source.Substring(_legacyDataDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                        {
                            complete = false;
                            LogFailure("Legacy migration", "Skipped reparse-point file to avoid unsafe traversal: " + source);
                            continue;
                        }
                        string targetRelative = String.Equals(relative, "YnixTrainerExtended.ini", StringComparison.OrdinalIgnoreCase)
                            ? "AvernesSowtware.ini" : relative;
                        string destination = Path.Combine(_dataDirectory, targetRelative);
                        if (!MigrateFileWithVerification(source, destination, relative)) complete = false;
                    }
                }
                catch (Exception ex)
                {
                    complete = false;
                    LogException("MigrateLegacyRuntimeData enumeration", ex);
                }
            }

            if (File.Exists(rootLegacyIni) && !MigrateFileWithVerification(rootLegacyIni,
                _configPath, "scripts/YnixTrainerExtended.ini")) complete = false;

            if (complete)
            {
                try
                {
                    if (Directory.Exists(_legacyDataDirectory)) Directory.Delete(_legacyDataDirectory, true);
                    if (File.Exists(rootLegacyIni)) File.Delete(rootLegacyIni);
                    Log("Legacy YnixTrainer data migration completed with size and SHA-256 verification.");
                    TryDeleteLegacyRuntimeBinary();
                }
                catch (Exception ex)
                {
                    LogException("Legacy migration cleanup", ex);
                }
            }
            else LogFailure("Legacy migration", "At least one file could not be copied and verified; the old YnixTrainer folder was retained.");
        }

        private bool MigrateFileWithVerification(string source, string destination, string relative)
        {
            try
            {
                string target = destination;
                if (File.Exists(destination))
                {
                    if (new FileInfo(source).Length == new FileInfo(destination).Length &&
                        String.Equals(GetSha256(source), GetSha256(destination), StringComparison.OrdinalIgnoreCase)) return true;
                    target = Path.Combine(_dataDirectory, "LegacyMigration", "Conflicts", relative);
                    string conflictDirectory = Path.GetDirectoryName(target);
                    if (!String.IsNullOrEmpty(conflictDirectory)) Directory.CreateDirectory(conflictDirectory);
                    if (File.Exists(target))
                    {
                        if (new FileInfo(source).Length == new FileInfo(target).Length &&
                            String.Equals(GetSha256(source), GetSha256(target), StringComparison.OrdinalIgnoreCase)) return true;
                        string stem = Path.GetFileNameWithoutExtension(target);
                        string ext = Path.GetExtension(target);
                        string uniqueTarget = Path.Combine(conflictDirectory, stem + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ext);
                        int suffix = 2;
                        while (File.Exists(uniqueTarget))
                        {
                            uniqueTarget = Path.Combine(conflictDirectory, stem + "_" + suffix.ToString(CultureInfo.InvariantCulture) + ext);
                            suffix++;
                        }
                        target = uniqueTarget;
                    }
                }

                string directory = Path.GetDirectoryName(target);
                if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporary = target + ".migration.tmp";
                File.Copy(source, temporary, true);
                if (new FileInfo(source).Length != new FileInfo(temporary).Length ||
                    !String.Equals(GetSha256(source), GetSha256(temporary), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Size or SHA-256 mismatch for " + relative);
                if (File.Exists(target)) File.Delete(target);
                File.Move(temporary, target);
                if (new FileInfo(source).Length != new FileInfo(target).Length ||
                    !String.Equals(GetSha256(source), GetSha256(target), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Final migration verification failed for " + relative);
                return true;
            }
            catch (Exception ex)
            {
                LogException("Legacy migration " + relative, ex);
                return false;
            }
        }

        private void MigrateInstalledDataToWritableLocation()
        {
            string installedDirectory = Path.Combine(_scriptsDirectory, "AvernesSowtware");
            try
            {
                string installedFull = Path.GetFullPath(installedDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string activeFull = Path.GetFullPath(_dataDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (String.Equals(installedFull, activeFull, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(installedDirectory)) return;

                bool complete = true;
                List<string> files = new List<string>();
                CollectFilesWithoutReparsePoints(installedDirectory, files, ref complete);
                for (int i = 0; i < files.Count; i++)
                {
                    string source = files[i];
                    string relative = source.Substring(installedDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string destination = Path.Combine(_dataDirectory, relative);
                    if (!MigrateFileWithVerification(source, destination, "AvernesSowtware/" + relative)) complete = false;
                }
                if (complete) Log("Copied existing AvernesSowtware data into the writable AppData/Documents location with SHA-256 verification.");
                else LogFailure("Data directory fallback", "One or more files could not be copied to the writable data directory; original scripts data was retained.");
            }
            catch (Exception ex) { LogException("MigrateInstalledDataToWritableLocation", ex); }
        }

        private void CollectFilesWithoutReparsePoints(string directory, List<string> files, ref bool complete)
        {
            string[] currentFiles = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < currentFiles.Length; i++)
            {
                if ((File.GetAttributes(currentFiles[i]) & FileAttributes.ReparsePoint) != 0)
                {
                    complete = false;
                    LogFailure("Data migration", "Skipped reparse-point file: " + currentFiles[i]);
                    continue;
                }
                files.Add(currentFiles[i]);
            }

            string[] directories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < directories.Length; i++)
            {
                if ((File.GetAttributes(directories[i]) & FileAttributes.ReparsePoint) != 0)
                {
                    complete = false;
                    LogFailure("Data migration", "Skipped reparse-point directory: " + directories[i]);
                    continue;
                }
                CollectFilesWithoutReparsePoints(directories[i], files, ref complete);
            }
        }

        private void TryDeleteLegacyRuntimeBinary()
        {
            string legacyDll = Path.Combine(_scriptsDirectory, "YnixTrainerExtended.dll");
            try
            {
                if (File.Exists(legacyDll)) File.Delete(legacyDll);
                if (File.Exists(legacyDll)) LogFailure("Legacy cleanup", "YnixTrainerExtended.dll remains; it may be locked by the script host. The 1.3.5 installer will remove it before launch.");
            }
            catch (Exception ex) { LogException("Legacy DLL cleanup", ex); }
        }

        private static string ResolveGameDirectory()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                string fullPath = Path.GetFullPath(baseDirectory);
                string folderName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (String.Equals(folderName, "scripts", StringComparison.OrdinalIgnoreCase))
                {
                    DirectoryInfo parent = Directory.GetParent(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (parent != null) return parent.FullName;
                }
                return fullPath;
            }
            catch
            {
                return baseDirectory;
            }
        }

        public AvernesSowtwareScript()
        {
            _gameDirectory = ResolveGameDirectory();
            _scriptsDirectory = Path.Combine(_gameDirectory, "scripts");
            _dataDirectory = ResolveWritableDataDirectory(_scriptsDirectory);
            _legacyDataDirectory = Path.Combine(_scriptsDirectory, "YnixTrainer");
            _vehiclesDirectory = Path.Combine(_dataDirectory, "Vehicles");
            // Keep diagnostics directly inside GTA V\scripts so users can find logs
            // and crash markers without having to discover a second-level folder.
            _logDirectory = _scriptsDirectory;
            _sourceConfigPath = Path.Combine(_dataDirectory, "Config.ini");
            _configPath = Path.Combine(_dataDirectory, "AvernesSowtware.ini");
            _addonPath = Path.Combine(_dataDirectory, "addons.json");
            _vehicleCatalogPath = Path.Combine(_vehiclesDirectory, "vehicles.json");
            _legacyVehicleCatalogPath = Path.Combine(_dataDirectory, "vehicle_catalog.json");
            _favoriteLocationsPath = Path.Combine(_dataDirectory, "favorite_locations.json");
            _savedVehiclesPath = Path.Combine(_vehiclesDirectory, "saved_vehicles.json");
            _atlasProgressPath = Path.Combine(_dataDirectory, "atlas_progress.json");
            _outfitsPath = Path.Combine(_dataDirectory, "outfits.json");
            _hotkeysPath = Path.Combine(_dataDirectory, "hotkeys.json");
            _logPath = Path.Combine(_logDirectory, "AvernesSowtware.log");
            _sessionMarkerPath = Path.Combine(_logDirectory, "AvernesSowtware.session");
            _errorLogPath = _logPath;
            _crashReportPath = _logPath;
            _vehicleSearchQuery = String.Empty;

            _settings = new TrainerSettings();
            _pages = new Dictionary<string, List<MenuEntry>>(StringComparer.OrdinalIgnoreCase);
            _pageStack = new Stack<string>();
            _addonVehicles = new List<AddonVehicle>();
            _vehicleCatalog = new List<AddonVehicle>();
            _favoriteLocations = new List<FavoriteLocation>();
            _savedVehicles = new List<SavedVehicle>();
            _vehicleCategoryPageKeys = new List<string>();

            _weaponNames = BuildWeaponCatalog();
            _weaponComponentNames = BuildWeaponComponentCatalog();

            Interval = 0;
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            AppDomain.CurrentDomain.ProcessExit += OnDomainProcessExit;

            // Write the startup marker before any migration or validation can log an error.
            // AppendLogFile creates the target directory itself and falls back to LocalAppData.
            Log("Startup reached script constructor. BaseDirectory=" + AppDomain.CurrentDomain.BaseDirectory +
                "; gameDirectory=" + _gameDirectory + "; scriptsDirectory=" + _scriptsDirectory +
                "; assembly=" + GetType().Assembly.Location);

            try
            {
                Directory.CreateDirectory(_dataDirectory);
                Directory.CreateDirectory(_vehiclesDirectory);
                Directory.CreateDirectory(_logDirectory);
                StartDiagnosticSession();
                MigrateLegacyRuntimeData();
                EnsureCriticalFileIntegrity();
                CleanupObsoleteFiles();
            }
            catch (Exception ex)
            {
                LogException("Create data directory", ex);
            }

            try { LoadFavoriteLocations(); }
            catch (Exception ex) { LogException("LoadFavoriteLocations", ex); }

            try { LoadSavedVehicles(); }
            catch (Exception ex) { LogException("LoadSavedVehicles", ex); }

            // Build the menu independently: malformed settings or JSON must not
            // prevent the trainer from opening so the user can still inspect/fix it.
            try { BuildMenus(); }
            catch (Exception ex) { LogException("BuildMenus", ex); }

            try { LoadSettings(); }
            catch (Exception ex) { LogException("LoadSettings", ex); }

            try { LoadHotkeys(); }
            catch (Exception ex) { LogException("LoadHotkeys", ex); }

            try { LoadVehicleCatalog(); }
            catch (Exception ex) { LogException("LoadVehicleCatalog", ex); }

            try { LoadAddonVehicles(); }
            catch (Exception ex)
            {
                LogException("LoadAddonVehicles", ex);
                try { RebuildVehicleSpawnerMenu(); } catch (Exception rebuildEx) { LogException("RebuildVehicleSpawnerMenu", rebuildEx); }
            }

            Notify(ProductName + " " + ProductVersion + " loaded. Logs: " + _logPath + ". Press " + GetHotkeyLabel("MenuToggle") + " to open.");
            Log("Initialized. Legacy settings and save paths were preserved. Vehicle DB: " + _vehicleCatalogPath +
                "; diagnostics: " + _logDirectory);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_aborted) return;
            try
            {
                // Keep pause/front-end controls disabled while the trainer is open and
                // for several script ticks after closing it. GTA can process the same
                // ESC press after ScriptHookVDotNet raises KeyDown, so one frame is not
                // sufficient on every game/build configuration.
                if (_menuOpen || _suppressPauseControlsFrames > 0)
                {
                    BlockMenuGameControls();
                    if (!_menuOpen && _suppressPauseControlsFrames > 0)
                        _suppressPauseControlsFrames--;
                }

                // GTA's CASH_CHANGE HUD component is the floating "+$amount" popup.
                // Hide only that component briefly after our Story Mode cash update;
                // the normal cash balance remains visible.
                if (DateTime.UtcNow < _hideCashChangeUntilUtc)
                    Function.Call((Hash)0x6806C51AD12B83B8UL, 13); // HIDE_HUD_COMPONENT_THIS_FRAME(CASH_CHANGE)

                if (_vehicleSearchInputPending) ProcessVehicleSearchInput();

                // Delete at most one companion per interval. Mass entity deletion in
                // one game tick can destabilize GTA/SHVDN when several group peds exist.
                ProcessPendingBodyguardDismissals();

                Ped ped = Game.Player.Character;
                ProcessPendingVehicleSpawn(ped);
                if (!IsEntityUsable(ped))
                {
                    LogStaleEntityOnce(0, "OnTick skipped because the player ped no longer exists.");
                    return;
                }

                RunEntityWatchdog(ped);
                long managerStarted = BeginManagerTick();
                ApplyPopulationDensity();
                ProcessPendingGroundTeleport(ped);
                ProcessDestroyedTrainerVehicles(ped);
                RecordManagerTick("World and entity registry", managerStarted);

                managerStarted = BeginManagerTick();
                if (!_originalModelCaptured)
                {
                    if (!IsEntityUsable(ped)) return;
                    _originalModelHash = Function.Call<uint>(Hash.GET_ENTITY_MODEL, ped.Handle);
                    _originalModelCaptured = true;
                }
                ProcessPendingSkinStateRestore();
                ProcessFranklinHairProtection(ped);
                ApplyPlayerSettings(ped);
                ApplyLegacyPlayerEffects(ped);
                ProcessAutomaticParachuteDeployment(ped);
                RecordManagerTick("Player systems", managerStarted);

                managerStarted = BeginManagerTick();
                ProcessPendingVehicleDeletion(ped);
                ProcessBodyguardFollowing(ped);
                ApplyAntiEject(ped);
                RecordManagerTick("Bodyguard and protection", managerStarted);

                managerStarted = BeginManagerTick();
                ApplyVehicleSettings(ped);
                ProcessVehicleRocketFire(ped);
                ApplyLegacyVehicleEffects(ped);
                RecordManagerTick("Vehicle systems", managerStarted);

                managerStarted = BeginManagerTick();
                ApplyWorldSettings();
                if (_settings.Speedometer) DrawSpeedometer(ped);
                RecordManagerTick("World and rendering", managerStarted);

                // Grant automatically once per script session. Do not re-grant on every
                // ped/model transition because missions may deliberately replace equipment.
                if (_settings.AutoGiveWeapons && !_autoLoadoutGranted)
                {
                    GiveFullWeaponLoadout(ped);
                    _autoLoadoutGranted = true;
                    Notify("Full weapon loadout delivered.");
                }

                if (_menuOpen && !_vehicleSearchInputPending) DrawMenu();
            }
            catch (Exception ex)
            {
                LogException("Tick", ex);
                NotifyError("Tick");
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (ProcessHotkeyCapture(e))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }

                if (IsHotkey("MenuToggle", e))
                {
                    _menuOpen = !_menuOpen;
                    if (!_menuOpen) _vehicleSearchInputPending = false;
                    if (_menuOpen)
                    {
                        _currentPage = "Main";
                        _pageStack.Clear();
                        _selectedIndex = 0;
                        _scrollOffset = 0;
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }

                if (!_menuOpen && _settings.VehicleRockets && IsHotkey("VehicleRockets", e))
                {
                    _vehicleRocketFirePending = true;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
                if (!_menuOpen) return;

                // Consume menu keys so they are not also processed by the game.
                if (IsHotkey("MenuUp", e) || IsHotkey("MenuDown", e) || IsHotkey("MenuSelect", e) ||
                    e.KeyCode == Keys.NumPad5 || IsHotkey("MenuBack", e) || e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }

                if (e.KeyCode == Keys.Escape)
                {
                    // ESC always closes the trainer. It never opens the pause/map screen.
                    _menuOpen = false;
                    _vehicleSearchInputPending = false;
                    _suppressPauseControlsFrames = 12;
                    _pageStack.Clear();
                    // The actual DISABLE_CONTROL_ACTION calls are repeated at the start
                    // of the next script tick, where GTA requires them to run.
                    BlockMenuGameControls();
                    return;
                }

                List<MenuEntry> entries = GetCurrentEntries();
                if (entries.Count == 0) return;

                if (IsHotkey("MenuUp", e))
                {
                    _selectedIndex = (_selectedIndex - 1 + entries.Count) % entries.Count;
                    KeepSelectionVisible(entries.Count);
                    return;
                }
                if (IsHotkey("MenuDown", e))
                {
                    _selectedIndex = (_selectedIndex + 1) % entries.Count;
                    KeepSelectionVisible(entries.Count);
                    return;
                }
                if (IsHotkey("MenuSelect", e) || e.KeyCode == Keys.NumPad5)
                {
                    MenuEntry selected = entries[_selectedIndex];
                    if (!String.IsNullOrEmpty(selected.ChildPage))
                    {
                        _pageStack.Push(_currentPage);
                        _currentPage = selected.ChildPage;
                        _selectedIndex = 0;
                        _scrollOffset = 0;
                    }
                    else if (selected.Action != null)
                    {
                        selected.Action();
                    }
                    return;
                }
                if (IsHotkey("MenuBack", e))
                {
                    if (_pageStack.Count > 0)
                    {
                        _currentPage = _pageStack.Pop();
                        _selectedIndex = 0;
                        _scrollOffset = 0;
                    }
                    else
                    {
                        _menuOpen = false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogException("KeyDown", ex);
                NotifyError("Menu action");
            }
        }

        private void BlockMenuGameControls()
        {
            // Control 200 is INPUT_FRONTEND_PAUSE (ESC). Block pause/menu controls
            // in all input groups because GTA may read the pause input from a
            // different group than the one used by the trainer's menu navigation.
            int[] controls = new int[] { 27, 172, 173, 174, 175, 176, 177, 178, 199, 200, 202 };
            for (int group = 0; group <= 2; group++)
            {
                for (int i = 0; i < controls.Length; i++)
                    Function.Call((Hash)0xFE99B66D079CF6BCUL, group, controls[i], true);
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            _aborted = true;
            _menuOpen = false;
            _vehicleSearchInputPending = false;
            _suppressPauseControlsFrames = 0;
            try
            {
                CancelPendingGroundTeleport();
                RestoreAntiEject();
                RestoreNitroSpeedLimit();
                if (_neverWantedMaxLevelCaptured)
                {
                    Function.Call((Hash)0xAA5F02DB48D704B9UL, ClampInt(_previousMaxWantedLevel, 0, 5));
                    _neverWantedMaxLevelCaptured = false;
                }
                Ped ped = Game.Player.Character;
                if (IsEntityUsable(ped))
                {
                    Function.Call(Hash.SET_ENTITY_INVINCIBLE, ped.Handle, false);
                    Function.Call((Hash)0xEA1C610A04DB6BBBUL, ped.Handle, true, false);
                    Function.Call((Hash)0x1A9205C1B9EE827FUL, ped.Handle, true, true);
                    Function.Call((Hash)0xB128377056A54E2AUL, ped.Handle, true);
                    Function.Call((Hash)0xCE07B9F7817AADA3UL, Game.Player, 1.0f);
                    Function.Call((Hash)0x18F621F7A5B1F85DUL, false);
                    Function.Call((Hash)0x7E08924259E08CE0UL, false);
                    Function.Call(Hash.SET_PED_MAX_TIME_UNDERWATER, ped.Handle, 10.0f);
                    DisableInfiniteAmmoForCatalog(ped);
                    Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, false);
                    ApplyMilitaryIgnore(ped, false);
                    RestoreFortZancudoArmyDispatch();
                    Function.Call((Hash)0xB128377056A54E2AUL, ped.Handle, true);
                    Function.Call((Hash)0xEA1C610A04DB6BBBUL, ped.Handle, true, false);
                }
                Function.Call((Hash)0x18F621F7A5B1F85DUL, false);
                Function.Call((Hash)0x7E08924259E08CE0UL, false);
                RestoreFortZancudoArmyDispatch();
                CancelPendingVehicleSpawn();
                ReleaseBodyguardsOnScriptAbort();
                if (_lastProtectedVehicleHandle != 0) RestoreVehicleProtection(_lastProtectedVehicleHandle);
                if (_clockPauseWasSet)
                {
                    Function.Call(Hash.PAUSE_CLOCK, false);
                    _clockPauseWasSet = false;
                }
                if (_weatherWasLocked)
                {
                    Function.Call(Hash.CLEAR_WEATHER_TYPE_PERSIST);
                    Function.Call(Hash.CLEAR_OVERRIDE_WEATHER);
                    _weatherWasLocked = false;
                }
                Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Game.Player, 1.0f);
                Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, Game.Player, 1.0f);
                Log("Script aborted/unloaded; temporary protections and pending operations were cleaned up.");
                AppendLogFile(_logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [SCRIPT ABORTED] ScriptHookVDotNet unloaded the trainer. Native GTA crash details are not available to a managed script." + Environment.NewLine);
                DeleteSessionMarker();
            }
            catch (Exception ex)
            {
                LogException("Abort cleanup", ex);
            }
            finally
            {
                AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
                AppDomain.CurrentDomain.ProcessExit -= OnDomainProcessExit;
            }
        }

        private void ProcessFranklinHairProtection(Ped ped)
        {
            if (!IsEntityUsable(ped)) return;
            uint modelHash = Function.Call<uint>(Hash.GET_ENTITY_MODEL, ped.Handle);
            if (modelHash != GetHash("player_one")) return;

            const int hairComponent = 2;
            int drawable = Function.Call<int>((Hash)0x67F3780DD425D4FCUL, ped.Handle, hairComponent);
            int texture = Function.Call<int>((Hash)0x04A355E041E004E6UL, ped.Handle, hairComponent);
            int palette = Function.Call<int>((Hash)0xE3DD5F2A84B42281UL, ped.Handle, hairComponent);
            bool cutsceneActive = Function.Call<bool>((Hash)0xD3C2E180A40F031EUL);
            if (drawable > 0)
            {
                // Keep the most recent valid hairstyle so a later barber change
                // is not overwritten by an outdated snapshot after a mission cutscene.
                if (!cutsceneActive && (!_franklinHairCaptured || drawable != _franklinHairDrawable ||
                    texture != _franklinHairTexture || palette != _franklinHairPalette))
                {
                    _franklinHairDrawable = drawable;
                    _franklinHairTexture = texture;
                    _franklinHairPalette = palette;
                    _franklinHairCaptured = true;
                    Log("Updated Franklin hairstyle snapshot: drawable=" + drawable + ", texture=" + texture + ", palette=" + palette + ".");
                }
                _franklinHairRestoreAtUtc = DateTime.MinValue;
                return;
            }
            if (!_franklinHairCaptured || _franklinHairDrawable <= 0) return;
            if (cutsceneActive)
            {
                _franklinHairRestoreAtUtc = DateTime.MinValue;
                return;
            }
            if (_franklinHairRestoreAtUtc == DateTime.MinValue)
            {
                _franklinHairRestoreAtUtc = DateTime.UtcNow.AddSeconds(3);
                return;
            }
            if (DateTime.UtcNow < _franklinHairRestoreAtUtc) return;

            try
            {
                bool valid = Function.Call<bool>((Hash)0xE825F6B6CEA7671DUL,
                    ped.Handle, hairComponent, _franklinHairDrawable, _franklinHairTexture);
                if (!valid)
                {
                    Log("Franklin hair restore skipped: saved component variation is not valid for the current model.");
                    _franklinHairRestoreAtUtc = DateTime.MinValue;
                    return;
                }
                Function.Call((Hash)0x262B14F48D29DE80UL, ped.Handle, hairComponent,
                    _franklinHairDrawable, _franklinHairTexture, _franklinHairPalette);
                Log("Restored Franklin hairstyle after cutscene. Drawable=" + _franklinHairDrawable + ".");
                _franklinHairRestoreAtUtc = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                LogException("ProcessFranklinHairProtection", ex);
                _franklinHairRestoreAtUtc = DateTime.UtcNow.AddSeconds(5);
            }
        }



        private void ApplyPlayerSettings(Ped ped)
        {
            if (!IsEntityUsable(ped)) return;
            Function.Call(Hash.SET_ENTITY_INVINCIBLE, ped.Handle, _settings.GodMode);
            Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, ped.Handle, _settings.NoReload);
            Function.Call((Hash)0xEA1C610A04DB6BBBUL, ped.Handle, !_settings.Invisible, false);
            Function.Call((Hash)0xB128377056A54E2AUL, ped.Handle, !_settings.NoRagdoll);
            // This is a per-frame native. Calling the wrong hash here can destabilize the game.
            if (_settings.ExplosiveAmmo) Function.Call((Hash)0xA66C71C98D5F2CFBUL, Game.Player);
            if (_settings.FireAmmo) Function.Call((Hash)0x11879CDD803D30F4UL, Game.Player);
            Function.Call((Hash)0xCE07B9F7817AADA3UL, Game.Player, _settings.SuperDamage ? 10.0f : 1.0f);
            Function.Call(Hash.SET_PED_MAX_TIME_UNDERWATER, ped.Handle, _settings.UnlimitedOxygen ? 1000.0f : 10.0f);
            Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Game.Player, _settings.FastRun ? 1.49f : 1.0f);
            Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, Game.Player, _settings.FastSwim ? 1.49f : 1.0f);

            if (_settings.InfiniteStamina)
                Function.Call(Hash.RESET_PLAYER_STAMINA, Game.Player);

            if (_settings.InfiniteSpecialAbility &&
                !Function.Call<bool>((Hash)0x05A1FE504B7F2587UL, Game.Player))
                Function.Call((Hash)0x3DACA8DDC6FD4980UL, Game.Player, true); // SPECIAL_ABILITY_FILL_METER

            if (_settings.SuperJump)
                Function.Call(Hash.SET_SUPER_JUMP_THIS_FRAME, Game.Player);

            bool ignoreLawAndMilitary = _settings.CopsIgnore || _settings.NeverWanted;
            if (ignoreLawAndMilitary)
            {
                if (!_neverWantedMaxLevelCaptured)
                {
                    _previousMaxWantedLevel = Function.Call<int>((Hash)0x462E0DB9B137DC5FUL);
                    _neverWantedMaxLevelCaptured = true;
                }
                Function.Call((Hash)0xAA5F02DB48D704B9UL, 0); // SET_MAX_WANTED_LEVEL
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player, 0, false);
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, true);
            }
            else
            {
                if (_neverWantedMaxLevelCaptured)
                {
                    Function.Call((Hash)0xAA5F02DB48D704B9UL, ClampInt(_previousMaxWantedLevel, 0, 5));
                    _neverWantedMaxLevelCaptured = false;
                }
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, false);
            }

            UpdateFortZancudoArmyDispatch(ped, ignoreLawAndMilitary);
            ApplyMilitaryIgnore(ped, ignoreLawAndMilitary);
            if (ignoreLawAndMilitary) SuppressActiveMilitaryCombat(ped);

            if (_settings.InfiniteAmmo)
            {
                uint weaponHash = Function.Call<uint>(Hash.GET_SELECTED_PED_WEAPON, ped.Handle);
                Function.Call(Hash.SET_PED_INFINITE_AMMO, ped.Handle, true, weaponHash);
                _infiniteAmmoWasApplied = true;
            }
            else if (_infiniteAmmoWasApplied)
            {
                DisableInfiniteAmmoForCatalog(ped);
                _infiniteAmmoWasApplied = false;
            }
        }



        private void UpdateFortZancudoArmyDispatch(Ped player, bool ignoreLawAndMilitary)
        {
            bool insideFortZancudo = false;
            if (ignoreLawAndMilitary && IsEntityUsable(player))
            {
                Vector3 position = player.Position;
                insideFortZancudo = position.X >= -3000.0f && position.X <= -1300.0f &&
                    position.Y >= 2200.0f && position.Y <= 4100.0f;
            }

            if (!insideFortZancudo)
            {
                if (_armyVehicleDispatchSuppressed) RestoreFortZancudoArmyDispatch();
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (_armyVehicleDispatchSuppressed && now < _nextFortZancudoDispatchRefreshUtc) return;
            try
            {
                // Dispatch service 15 is DT_ArmyVehicle (14 is DT_PoliceBoat).
                // Reassert it because mission/script activity can restore dispatch services.
                Function.Call((Hash)0xDC0F817884CDD856UL, 15, false); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle
                Function.Call((Hash)0x9B2BD3773123EA2FUL, 15, true);  // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION
                Function.Call((Hash)0xDC0F817884CDD856UL, 2, false);  // ENABLE_DISPATCH_SERVICE: DT_PoliceHelicopter
                Function.Call((Hash)0x9B2BD3773123EA2FUL, 2, true);   // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION
                _armyVehicleDispatchSuppressed = true;
                _nextFortZancudoDispatchRefreshUtc = now.AddMilliseconds(900);
                Log("Fort Zancudo Army and helicopter dispatch suppression refreshed.");
            }
            catch (Exception ex)
            {
                LogException("UpdateFortZancudoArmyDispatch", ex);
            }
        }

        private void RestoreFortZancudoArmyDispatch()
        {
            if (!_armyVehicleDispatchSuppressed) return;
            try
            {
                Function.Call((Hash)0xDC0F817884CDD856UL, 15, true); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle
                Function.Call((Hash)0xDC0F817884CDD856UL, 2, true);  // ENABLE_DISPATCH_SERVICE: DT_PoliceHelicopter
                Function.Call((Hash)0x9B2BD3773123EA2FUL, 15, false); // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION
                Function.Call((Hash)0x9B2BD3773123EA2FUL, 2, false);  // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION
                _armyVehicleDispatchSuppressed = false;
                _nextFortZancudoDispatchRefreshUtc = DateTime.MinValue;
                Log("Fort Zancudo Army vehicle dispatch restored during script shutdown.");
            }
            catch (Exception ex) { LogException("RestoreFortZancudoArmyDispatch", ex); }
        }

        private void SuppressActiveMilitaryCombat(Ped player)
        {
            if (!IsEntityUsable(player)) return;
            // Fort Zancudo bounds with a margin. Never scan the full ambient population
            // while Never Wanted is enabled in ordinary city/free-roam areas.
            Vector3 playerPosition = player.Position;
            if (playerPosition.X < -3000.0f || playerPosition.X > -1300.0f ||
                playerPosition.Y < 2200.0f || playerPosition.Y > 4100.0f) return;

            DateTime now = DateTime.UtcNow;
            if (now < _nextMilitaryCombatScanUtc) return;
            _nextMilitaryCombatScanUtc = now.AddMilliseconds(750);

            try
            {
                uint armyGroup = Function.Call<uint>((Hash)0xD24D37CC275948CCUL, "ARMY"); // GET_HASH_KEY
                if (armyGroup == 0) return;

                // Inspect a wider Fort Zancudo air-response bubble. Existing jets are
                // mission-owned entities and are not cancelled by disabling new dispatch.
                Ped[] nearbyArmy = World.GetNearbyPeds(player, 3200.0f);
                if (nearbyArmy == null || nearbyArmy.Length == 0) return;
                for (int i = 0; i < nearbyArmy.Length; i++)
                {
                    Ped npc = nearbyArmy[i];
                    if (!IsEntityUsable(npc)) continue;
                    uint npcGroup = Function.Call<uint>((Hash)0x7DBDD04862D95F04UL, npc.Handle);
                    if (npcGroup != armyGroup) continue;

                    bool attackingPlayer = Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, npc.Handle, player.Handle);
                    bool pilotInAircraft = false;
                    if (Function.Call<bool>((Hash)0x997ABD671D25CA0BUL, npc.Handle, false)) // IS_PED_IN_ANY_VEHICLE
                    {
                        int aircraft = Function.Call<int>((Hash)0x9A9112A0FE9A4713UL, npc.Handle, false);
                        if (aircraft != 0 && IsVehicleHandleUsable(aircraft))
                        {
                            int vehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, aircraft); // GET_VEHICLE_CLASS
                            pilotInAircraft = vehicleClass == 15 || vehicleClass == 16; // Helicopter / Plane
                        }
                    }
                    if (!attackingPlayer && !pilotInAircraft) continue;
                    Function.Call(Hash.CLEAR_PED_TASKS, npc.Handle);
                }
            }
            catch (Exception ex)
            {
                LogException("SuppressActiveMilitaryCombat", ex);
            }
        }

        private void ApplyMilitaryIgnore(Ped ped, bool enabled)
        {
            try
            {
                if (enabled)
                {
                    // Do not globally freeze ambient combat AI and do not disable Army vehicle dispatch.
                    // Neutralize only the ARMY/player relationship below; this keeps world NPCs and
                    // emergency/military traffic running while Never Wanted is active.
                    _militaryIgnoreApplied = true;
                    if (!IsEntityUsable(ped)) return;

                    uint playerGroup = Function.Call<uint>((Hash)0x7DBDD04862D95F04UL, ped.Handle); // GET_PED_RELATIONSHIP_GROUP_HASH
                    uint armyGroup = Function.Call<uint>((Hash)0xD24D37CC275948CCUL, "ARMY"); // GET_HASH_KEY
                    if (playerGroup == 0 || armyGroup == 0) return;

                    if (_militaryRelationshipCaptured &&
                        (_militaryRelationshipPlayerGroup != playerGroup || _militaryRelationshipArmyGroup != armyGroup))
                    {
                        Function.Call((Hash)0xBF25EB89375A37ADUL, _militaryRelationshipOriginalArmyToPlayer,
                            _militaryRelationshipArmyGroup, _militaryRelationshipPlayerGroup);
                        Function.Call((Hash)0xBF25EB89375A37ADUL, _militaryRelationshipOriginalPlayerToArmy,
                            _militaryRelationshipPlayerGroup, _militaryRelationshipArmyGroup);
                        _militaryRelationshipCaptured = false;
                    }
                    if (!_militaryRelationshipCaptured)
                    {
                        _militaryRelationshipPlayerGroup = playerGroup;
                        _militaryRelationshipArmyGroup = armyGroup;
                        _militaryRelationshipOriginalArmyToPlayer = Function.Call<int>((Hash)0x9E6B70061662AE5CUL, armyGroup, playerGroup);
                        _militaryRelationshipOriginalPlayerToArmy = Function.Call<int>((Hash)0x9E6B70061662AE5CUL, playerGroup, armyGroup);
                        _militaryRelationshipCaptured = true;
                    }

                    // GTA relationship levels are directional, so neutralize both directions.
                    Function.Call((Hash)0xBF25EB89375A37ADUL, 3, armyGroup, playerGroup); // SET_RELATIONSHIP_BETWEEN_GROUPS
                    Function.Call((Hash)0xBF25EB89375A37ADUL, 3, playerGroup, armyGroup);
                    return;
                }

                if (!_militaryIgnoreApplied) return;
                // Global ignore and Army dispatch are no longer modified by this trainer.
                if (_militaryRelationshipCaptured)
                {
                    Function.Call((Hash)0xBF25EB89375A37ADUL, _militaryRelationshipOriginalArmyToPlayer,
                        _militaryRelationshipArmyGroup, _militaryRelationshipPlayerGroup);
                    Function.Call((Hash)0xBF25EB89375A37ADUL, _militaryRelationshipOriginalPlayerToArmy,
                        _militaryRelationshipPlayerGroup, _militaryRelationshipArmyGroup);
                    _militaryRelationshipCaptured = false;
                }
                _militaryIgnoreApplied = false;
            }
            catch (Exception ex)
            {
                LogException("ApplyMilitaryIgnore", ex);
            }
        }

        private void DisableInfiniteAmmoForCatalog(Ped ped)
        {
            for (int i = 0; i < _weaponNames.Length; i++)
            {
                uint weaponHash = GetHash(_weaponNames[i]);
                if (weaponHash != 0 && Function.Call<bool>(Hash.IS_WEAPON_VALID, weaponHash))
                    Function.Call(Hash.SET_PED_INFINITE_AMMO, ped.Handle, false, weaponHash);
            }
        }

        private void ApplyPopulationDensity()
        {
            // These per-frame multipliers are reset by GTA every frame. Explicitly restore
            // vanilla density so trainer cleanup/other transient scripts cannot leave it at zero.
            Function.Call((Hash)0x95E3D6257B166CF2UL, 1.0f); // SET_PED_DENSITY_MULTIPLIER_THIS_FRAME
            Function.Call((Hash)0x7A556143A1C03898UL, 1.0f, 1.0f); // SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME
            Function.Call((Hash)0x245A6883D966D537UL, 1.0f); // SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME
            Function.Call((Hash)0xB3B3359379FE77D3UL, 1.0f); // SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME
            Function.Call((Hash)0xEAE6DCC7EEE3DB1DUL, 1.0f); // SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME
        }

        private void ApplyWorldSettings()
        {
            if (_settings.FreezeTime)
            {
                Function.Call(Hash.PAUSE_CLOCK, true);
                _clockPauseWasSet = true;
            }
            else if (_clockPauseWasSet)
            {
                Function.Call(Hash.PAUSE_CLOCK, false);
                _clockPauseWasSet = false;
            }

            if (_settings.LockWeather)
            {
                int now = Environment.TickCount;
                if (!_weatherWasLocked || unchecked(now - _lastWeatherApplyTick) > 1500)
                {
                    Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, _settings.Weather);
                    _lastWeatherApplyTick = now;
                    _weatherWasLocked = true;
                }
            }
            else if (_weatherWasLocked)
            {
                Function.Call(Hash.CLEAR_WEATHER_TYPE_PERSIST);
                Function.Call(Hash.CLEAR_OVERRIDE_WEATHER);
                _weatherWasLocked = false;
            }
        }

        private void BuildMenus()
        {
            List<MenuEntry> main = NewPage("Main");
            main.Add(PageEntry("Player", "Player", "Player options from Extended and legacy 1.6."));
            main.Add(PageEntry("Weapons", "Weapons", "Weapon inventory, ammo and combat effects."));
            main.Add(PageEntry("Transport", "Transport", "Vehicle spawning, protection, tuning and customization."));

            main.Add(PageEntry("Powers & Vision", "Powers", "No-clip and vision modes."));
            main.Add(PageEntry("Bodyguards", "Bodyguards", "Spawn and dismiss armed companions."));
            main.Add(PageEntry("Animations", "Animations", "Scenario animations."));
            main.Add(PageEntry("Teleport", "Teleport", "Fixed landmark teleportation with terrain streaming."));
            main.Add(PageEntry("Saved Locations", "Locations", "Save and revisit your own Story Mode locations."));
            main.Add(PageEntry("World", "World", "Weather, time and local cleanup."));
            main.Add(PageEntry("Settings", "Settings", "Save and reload trainer settings."));
            main.Add(ActionEntry("Close menu", "Close", null, delegate { _menuOpen = false; }));

            List<MenuEntry> player = NewPage("Player");
            player.Add(ToggleEntry("God Mode", "GodMode", delegate { return _settings.GodMode; }, delegate(bool v) { _settings.GodMode = v; }));
            player.Add(ToggleEntry("Never Wanted", "NeverWanted", delegate { return _settings.NeverWanted; }, delegate(bool v) { _settings.NeverWanted = v; }));
            player.Add(ToggleEntry("Police Ignore Player", "CopsIgnore", delegate { return _settings.CopsIgnore; }, delegate(bool v) { _settings.CopsIgnore = v; }));
            player.Add(ToggleEntry("Invisibility", "Invisible", delegate { return _settings.Invisible; }, delegate(bool v) { _settings.Invisible = v; }));
            player.Add(ToggleEntry("No Ragdoll", "NoRagdoll", delegate { return _settings.NoRagdoll; }, delegate(bool v) { _settings.NoRagdoll = v; }));
            player.Add(ToggleEntry("Infinite Stamina", "InfiniteStamina", delegate { return _settings.InfiniteStamina; }, delegate(bool v) { _settings.InfiniteStamina = v; }));
            player.Add(ToggleEntry("Infinite Special Ability", "InfiniteSpecialAbility", delegate { return _settings.InfiniteSpecialAbility; }, delegate(bool v) { _settings.InfiniteSpecialAbility = v; }));
            player.Add(ToggleEntry("Unlimited Oxygen", "UnlimitedOxygen", delegate { return _settings.UnlimitedOxygen; }, delegate(bool v) { _settings.UnlimitedOxygen = v; }));
            player.Add(ToggleEntry("Automatic Parachute Deployment", "AutoDeployParachute", delegate { return _settings.AutoDeployParachute; }, delegate(bool v) { _settings.AutoDeployParachute = v; _parachuteAutoDeploymentRequested = false; }));
            player.Add(ToggleEntry("Fast Run", "FastRun", delegate { return _settings.FastRun; }, delegate(bool v) { _settings.FastRun = v; }));
            player.Add(ToggleEntry("Fast Swim", "FastSwim", delegate { return _settings.FastSwim; }, delegate(bool v) { _settings.FastSwim = v; }));
            player.Add(ToggleEntry("Super Jump", "SuperJump", delegate { return _settings.SuperJump; }, delegate(bool v) { _settings.SuperJump = v; }));
            player.Add(PageEntry("Money", "Money", "Add Story Mode cash to the currently controlled protagonist."));
            player.Add(ActionEntry("Heal + full armour", "Heal", "Restore health and armour.", HealPlayer));
            player.Add(ActionEntry("Clear wanted level", "Wanted", "Remove current wanted stars.", ClearWantedLevel));
            player.Add(ActionEntry("Wanted level: 1 star", "Wanted1", null, delegate { SetWantedLevel(1); }));
            player.Add(ActionEntry("Wanted level: 3 stars", "Wanted3", null, delegate { SetWantedLevel(3); }));
            player.Add(ActionEntry("Wanted level: 5 stars", "Wanted5", null, delegate { SetWantedLevel(5); }));

            player.Add(ActionEntry("Clean clothes & blood", "Clean", null, CleanPlayer));
            player.Add(ActionEntry("Suicide", "Suicide", "Kills the current player character.", SuicidePlayer));
            player.Add(BackEntry());

            List<MenuEntry> money = NewPage("Money");
            money.Add(ActionEntry("Add $100,000", "Money100K", null, delegate { AddStoryModeMoney(100000); }));
            money.Add(ActionEntry("Add $1,000,000", "Money1M", null, delegate { AddStoryModeMoney(1000000); }));
            money.Add(ActionEntry("Add $10,000,000", "Money10M", null, delegate { AddStoryModeMoney(10000000); }));
            money.Add(ActionEntry("Add $100,000,000", "Money100M", null, delegate { AddStoryModeMoney(100000000); }));
            money.Add(ActionEntry("Add $1,000,000,000", "Money1B", null, delegate { AddStoryModeMoney(1000000000); }));
            money.Add(ActionEntry("Set cash to maximum ($2.147B)", "MoneyMax", "Sets current protagonist cash to the signed 32-bit limit.", delegate { SetStoryModeMoney(StoryCashLimit); }));
            money.Add(BackEntry());

            List<MenuEntry> weapons = NewPage("Weapons");
            weapons.Add(ToggleEntry("Infinite Ammo", "InfiniteAmmo", delegate { return _settings.InfiniteAmmo; }, delegate(bool v) { _settings.InfiniteAmmo = v; }));
            weapons.Add(ToggleEntry("No Reload", "NoReload", delegate { return _settings.NoReload; }, delegate(bool v) { _settings.NoReload = v; }));
            weapons.Add(ToggleEntry("Auto Give Weapons", "AutoGiveWeapons", delegate { return _settings.AutoGiveWeapons; }, delegate(bool v) { _settings.AutoGiveWeapons = v; if (v) _autoLoadoutGranted = false; }));
            weapons.Add(ToggleEntry("Explosive Ammo", "ExplosiveAmmo", delegate { return _settings.ExplosiveAmmo; }, delegate(bool v) { _settings.ExplosiveAmmo = v; }));
            weapons.Add(ToggleEntry("Incendiary Ammo", "FireAmmo", delegate { return _settings.FireAmmo; }, delegate(bool v) { _settings.FireAmmo = v; }));
            weapons.Add(ToggleEntry("Super Damage", "SuperDamage", delegate { return _settings.SuperDamage; }, delegate(bool v) { _settings.SuperDamage = v; }));
            weapons.Add(ActionEntry("Give full weapon arsenal", "GiveWeapons", "Give all weapons supported by this game build.", GiveFullWeaponLoadoutToPlayer));
            weapons.Add(ActionEntry("Apply premium weapon finish", "WeaponFinish", "Apply available tint and compatible attachments.", ApplyWeaponFinishToCurrentLoadout));
            weapons.Add(ActionEntry("Remove all weapons", "RemoveWeapons", null, RemoveAllWeapons));
            weapons.Add(BackEntry());

            List<MenuEntry> transport = NewPage("Transport");
            transport.Add(PageEntry("Vehicle Spawner", "VehicleSpawner", "Search and spawn the complete local vehicle database."));
            transport.Add(PageEntry("Saved Vehicles (50 slots)", "SavedVehicles", "Save the current vehicle build and restore it later."));
            transport.Add(ActionEntry("Search by name / Model ID", "VehicleSearch", "Search the local vehicle database by display name, model code or model hash.", OpenVehicleSearchKeyboard));
            transport.Add(ToggleEntry("Vehicle God Mode", "VehicleGodMode", delegate { return _settings.VehicleGodMode; }, delegate(bool v) { _settings.VehicleGodMode = v; }));
            transport.Add(ToggleEntry("Vehicle Rockets (Numpad +)", "VehicleRockets", delegate { return _settings.VehicleRockets; }, delegate(bool v) { _settings.VehicleRockets = v; }));
            transport.Add(ToggleEntry("Anti-Eject (stay inside vehicle)", "AntiEject", delegate { return _settings.AntiEject; }, delegate(bool v) { _settings.AntiEject = v; if (!v) RestoreAntiEject(); }));
            transport.Add(ToggleEntry("Bulletproof Tires", "BulletproofTires", delegate { return _settings.BulletproofTires; }, delegate(bool v) { _settings.BulletproofTires = v; }));
            transport.Add(ToggleEntry("Nitro (hold SHIFT)", "Nitro", delegate { return _settings.Nitro; }, delegate(bool v) { _settings.Nitro = v; }));
            transport.Add(ValueActionEntry("Nitro power -", "Reduce boost acceleration.", delegate { return "x" + _settings.NitroPower.ToString("0.00", CultureInfo.InvariantCulture); }, delegate { ChangeNitroPower(-0.25f); }));
            transport.Add(ValueActionEntry("Nitro power +", "Increase boost acceleration.", delegate { return "x" + _settings.NitroPower.ToString("0.00", CultureInfo.InvariantCulture); }, delegate { ChangeNitroPower(0.25f); }));
            transport.Add(ToggleEntry("Drift Mode", "DriftMode", delegate { return _settings.DriftMode; }, delegate(bool v) { _settings.DriftMode = v; }));
            transport.Add(ToggleEntry("Speedometer", "Speedometer", delegate { return _settings.Speedometer; }, delegate(bool v) { _settings.Speedometer = v; }));
            transport.Add(ToggleEntry("Horn Jump", "HornJump", delegate { return _settings.HornJump; }, delegate(bool v) { _settings.HornJump = v; }));
            transport.Add(ActionEntry("Максимальный тюнинг", "MaxTuning", "Максимум доступных улучшений, броня, двигатель, турбо и внешний вид.", MaxTuneVehicle));
            transport.Add(ActionEntry("Neon underglow ON", "NeonOn", null, delegate { SetVehicleNeons(true); }));
            transport.Add(ActionEntry("Neon underglow OFF", "NeonOff", null, delegate { SetVehicleNeons(false); }));
            transport.Add(ActionEntry("Limo window tint", "Tint", null, SetVehicleTint));
            transport.Add(ActionEntry("Matte black paint", "BlackPaint", null, delegate { SetVehicleColours(0, 0); }));
            transport.Add(ActionEntry("Black / red paint", "RedPaint", null, delegate { SetVehicleColours(0, 27); }));
            transport.Add(ActionEntry("Custom plate: AVERNES", "Plate", null, delegate { SetVehiclePlate("AVERNES"); }));
            transport.Add(PageEntry("Внешний вид и диски", "VehicleCustomization", "Установка обвесов, выбор семейств дисков и отдельных деталей."));
            transport.Add(ActionEntry("Repair & wash vehicle", "Repair", "Manual action only; no automatic repair.", RepairCurrentVehicle));
            transport.Add(ActionEntry("Flip vehicle upright", "Flip", null, FlipVehicle));
            transport.Add(ActionEntry("Open all doors", "OpenDoors", null, delegate { SetAllVehicleDoors(true); }));
            transport.Add(ActionEntry("Close all doors", "CloseDoors", null, delegate { SetAllVehicleDoors(false); }));
            transport.Add(ActionEntry("Delete current vehicle", "DeleteVehicle", "Deletes the vehicle you are currently driving.", DeleteCurrentVehicle));
            transport.Add(ActionEntry("Switch speed unit KM/H / MPH", "SpeedUnit", null, ToggleSpeedUnit));
            transport.Add(ActionEntry("Reload vehicle database", "ReloadVehicleDatabase", null, delegate
            {
                bool catalogLoaded = LoadVehicleCatalog();
                bool addonsLoaded = LoadAddonVehicles();
                RebuildVehicleSpawnerMenu();
                if (catalogLoaded || addonsLoaded)
                    Notify("Vehicle lists reloaded: " + GetCombinedVehicleList().Count + " unique models.");
                else
                    Notify("Vehicle database reload failed. Previous valid list was retained; check scripts\\AvernesSowtware\\AvernesSowtware.log.");
            }));
            transport.Add(BackEntry());

            List<MenuEntry> vehicleCustomization = NewPage("VehicleCustomization");
            vehicleCustomization.Add(ActionEntry("Установить все обвесы", "ApplyAllBodyKits", "Устанавливает только существующие детали; отсутствующие слоты пропускаются.", ApplyAllVehicleBodyKits));
            vehicleCustomization.Add(PageEntry("Выбор дисков и типа колёс", "VehicleWheels", "Семейство дисков и доступный дизайн колёс."));
            vehicleCustomization.Add(PageEntry("Визуальные элементы", "VehicleVisuals", "Выбор доступных спойлеров, бамперов, порогов и других деталей."));
            vehicleCustomization.Add(BackEntry());

            List<MenuEntry> vehicleWheels = NewPage("VehicleWheels");
            vehicleWheels.Add(ValueActionEntry("Семейство дисков", "Переключить на следующее семейство, поддерживаемое машиной.", GetCurrentVehicleWheelTypeLabel, delegate { CycleVehicleWheelType(1); }));
            vehicleWheels.Add(ActionEntry("Предыдущее семейство дисков", "WheelFamilyPrevious", null, delegate { CycleVehicleWheelType(-1); }));
            vehicleWheels.Add(ActionEntry("Следующее семейство дисков", "WheelFamilyNext", null, delegate { CycleVehicleWheelType(1); }));
            vehicleWheels.Add(ValueActionEntry("Стиль дисков", "Переключить дизайн установленных дисков.", GetCurrentVehicleWheelStyleLabel, delegate { CycleVehicleWheelStyle(1); }));
            vehicleWheels.Add(ActionEntry("Предыдущий стиль дисков", "WheelStylePrevious", null, delegate { CycleVehicleWheelStyle(-1); }));
            vehicleWheels.Add(ActionEntry("Следующий стиль дисков", "WheelStyleNext", null, delegate { CycleVehicleWheelStyle(1); }));
            vehicleWheels.Add(BackEntry());

            List<MenuEntry> vehicleVisuals = NewPage("VehicleVisuals");
            vehicleVisuals.Add(ValueActionEntry("Спойлер", "Следующий доступный вариант спойлера.", delegate { return GetCurrentVehicleModLabel(0); }, delegate { CycleVehicleMod(0, 1, "Спойлер"); }));
            vehicleVisuals.Add(ValueActionEntry("Передний бампер", "Следующий доступный вариант.", delegate { return GetCurrentVehicleModLabel(1); }, delegate { CycleVehicleMod(1, 1, "Передний бампер"); }));
            vehicleVisuals.Add(ValueActionEntry("Задний бампер", "Следующий доступный вариант.", delegate { return GetCurrentVehicleModLabel(2); }, delegate { CycleVehicleMod(2, 1, "Задний бампер"); }));
            vehicleVisuals.Add(ValueActionEntry("Пороги", "Следующий доступный вариант.", delegate { return GetCurrentVehicleModLabel(3); }, delegate { CycleVehicleMod(3, 1, "Пороги"); }));
            vehicleVisuals.Add(ValueActionEntry("Выхлоп", "Следующий доступный вариант.", delegate { return GetCurrentVehicleModLabel(4); }, delegate { CycleVehicleMod(4, 1, "Выхлоп"); }));
            vehicleVisuals.Add(ValueActionEntry("Каркас / решётка", "Следующий доступный вариант.", delegate { return GetCurrentVehicleModLabel(5); }, delegate { CycleVehicleMod(5, 1, "Каркас / решётка"); }));
            vehicleVisuals.Add(ValueActionEntry("Решётка радиатора", "Следующий доступный вариант.", delegate { return GetCurrentVehicleModLabel(6); }, delegate { CycleVehicleMod(6, 1, "Решётка радиатора"); }));
            vehicleVisuals.Add(ValueActionEntry("Капот", "Следующий доступный вариант.", delegate { return GetCurrentVehicleModLabel(7); }, delegate { CycleVehicleMod(7, 1, "Капот"); }));
            vehicleVisuals.Add(ValueActionEntry("Левое крыло", "Следующий доступный вариант.", delegate { return GetCurrentVehicleModLabel(8); }, delegate { CycleVehicleMod(8, 1, "Левое крыло"); }));
            vehicleVisuals.Add(ValueActionEntry("Правое крыло", "Следующий доступный вариант.", delegate { return GetCurrentVehicleModLabel(9); }, delegate { CycleVehicleMod(9, 1, "Правое крыло"); }));
            vehicleVisuals.Add(ValueActionEntry("Крыша", "Следующий доступный вариант.", delegate { return GetCurrentVehicleModLabel(10); }, delegate { CycleVehicleMod(10, 1, "Крыша"); }));
            vehicleVisuals.Add(BackEntry());

            List<MenuEntry> vehicleSpawner = NewPage("VehicleSpawner");
            vehicleSpawner.Add(new MenuEntry("Loading vehicle catalog...", "", null, null, null));
            vehicleSpawner.Add(BackEntry());

            List<MenuEntry> powers = NewPage("Powers");
            powers.Add(ToggleEntry("No-Clip / Fly Mode", "NoClip", delegate { return _settings.NoClip; }, delegate(bool v) { _settings.NoClip = v; if (!v) RestoreNoClip(); }));
            powers.Add(ToggleEntry("Night Vision", "NightVision", delegate { return _settings.NightVision; }, delegate(bool v) { _settings.NightVision = v; }));
            powers.Add(ToggleEntry("Thermal Vision", "ThermalVision", delegate { return _settings.ThermalVision; }, delegate(bool v) { _settings.ThermalVision = v; }));
            powers.Add(BackEntry());

            List<MenuEntry> bodyguards = NewPage("Bodyguards");
            bodyguards.Add(ActionEntry("Spawn SWAT bodyguard", "SpawnSwat", null, delegate { SpawnBodyguard("s_m_y_swat_01"); }));
            bodyguards.Add(ActionEntry("Spawn military bodyguard", "SpawnMilitary", null, delegate { SpawnBodyguard("s_m_y_marine_01"); }));
            bodyguards.Add(ActionEntry("Spawn agent bodyguard", "SpawnAgent", null, delegate { SpawnBodyguard("s_m_m_fibsec_01"); }));
            bodyguards.Add(ValueActionEntry("Оружие телохранителей", "Выбрать оружие для новых и действующих телохранителей.", GetSelectedBodyguardWeaponLabel, CycleBodyguardWeapon));
            bodyguards.Add(ToggleEntry("Бессмертие телохранителей", "BodyguardInvincible", delegate { return _settings.BodyguardInvincible; }, delegate(bool v) { _settings.BodyguardInvincible = v; ApplyBodyguardVitalsToAll(); SaveSettings(); }));
            bodyguards.Add(ValueActionEntry("Здоровье телохранителей -", "Уменьшить максимум HP на 50.", delegate { return _settings.BodyguardHealth.ToString(CultureInfo.InvariantCulture) + " HP"; }, delegate { ChangeBodyguardHealth(-50); }));
            bodyguards.Add(ValueActionEntry("Здоровье телохранителей +", "Увеличить максимум HP на 50.", delegate { return _settings.BodyguardHealth.ToString(CultureInfo.InvariantCulture) + " HP"; }, delegate { ChangeBodyguardHealth(50); }));
            bodyguards.Add(ValueActionEntry("Броня телохранителей -", "Уменьшить броню на 25.", delegate { return _settings.BodyguardArmour.ToString(CultureInfo.InvariantCulture); }, delegate { ChangeBodyguardArmour(-25); }));
            bodyguards.Add(ValueActionEntry("Броня телохранителей +", "Увеличить броню на 25.", delegate { return _settings.BodyguardArmour.ToString(CultureInfo.InvariantCulture); }, delegate { ChangeBodyguardArmour(25); }));
            bodyguards.Add(ActionEntry("Выдать выбранное оружие всем", "GiveBodyguardWeapon", null, GiveSelectedWeaponToBodyguards));
            bodyguards.Add(ActionEntry("Dismiss all bodyguards", "DismissGuards", null, DismissAllBodyguards));
            bodyguards.Add(BackEntry());

            List<MenuEntry> animations = NewPage("Animations");
            animations.Add(ActionEntry("Stop animation", "StopAnim", null, StopPlayerAnimation));
            animations.Add(ActionEntry("Smoke cigarette", "Smoke", null, delegate { StartPlayerScenario("WORLD_HUMAN_SMOKING"); }));
            animations.Add(ActionEntry("Drink", "Drink", null, delegate { StartPlayerScenario("WORLD_HUMAN_DRINKING"); }));
            animations.Add(ActionEntry("Dance", "Dance", null, delegate { StartPlayerScenario("WORLD_HUMAN_PARTYING"); }));
            animations.Add(ActionEntry("Sit", "Sit", null, delegate { StartPlayerScenario("WORLD_HUMAN_PICNIC"); }));
            animations.Add(ActionEntry("Push-ups", "Pushups", null, delegate { StartPlayerScenario("WORLD_HUMAN_PUSH_UPS"); }));
            animations.Add(BackEntry());

            List<MenuEntry> teleport = NewPage("Teleport");
            teleport.Add(ActionEntry("Teleport to waypoint", "Waypoint", "Load the marked destination before moving the player or current vehicle.", TeleportToWaypoint));
            teleport.Add(ValueActionEntry("Return to previous location", "Undo the last teleport. History stores up to 10 starting positions.", GetTeleportHistoryLabel, UndoLastTeleport));
            teleport.Add(ActionEntry("Teleport 3m forward", "Forward", null, TeleportForward));
            teleport.Add(ActionEntry("Maze Bank roof", "MazeBank", null, delegate { TeleportTo( -75.01f, -818.91f, 326.18f); }));
            teleport.Add(ActionEntry("Vinewood Sign", "Vinewood", null, delegate { TeleportTo(707.3f, 1197.2f, 348.5f); }));
            teleport.Add(ActionEntry("Mount Chiliad", "Chiliad", null, delegate { TeleportToGround(501.2f, 5604.5f, 797.9f); }));
            teleport.Add(ActionEntry("LS International Airport", "Airport", null, delegate { TeleportTo(-1034.6f, -2733.6f, 20.1f); }));
            teleport.Add(ActionEntry("Del Perro Pier", "Pier", null, delegate { TeleportTo(-1850.1f, -1231.8f, 13.0f); }));
            teleport.Add(ActionEntry("Fort Zancudo", "Fort", null, delegate { TeleportTo(-2047.4f, 3132.1f, 32.8f); }));
            teleport.Add(ActionEntry("Bolingbroke Prison", "Prison", null, delegate { TeleportTo(1691.5f, 2565.9f, 45.6f); }));
            teleport.Add(ActionEntry("Sandy Shores Airfield", "Sandy", null, delegate { TeleportTo(1747.8f, 3273.7f, 41.1f); }));
            teleport.Add(ActionEntry("Humane Labs", "Humane", null, delegate { TeleportTo(3540.0f, 3675.0f, 28.1f); }));
            teleport.Add(ActionEntry("Galileo Observatory", "Galileo", null, delegate { TeleportToGround(-438.7f, 1075.0f, 327.7f); }));
            teleport.Add(BackEntry());

            List<MenuEntry> world = NewPage("World");
            world.Add(ToggleEntry("Freeze Time", "FreezeTime", delegate { return _settings.FreezeTime; }, delegate(bool v) { _settings.FreezeTime = v; }));
            world.Add(ToggleEntry("Lock Weather", "LockWeather", delegate { return _settings.LockWeather; }, delegate(bool v) { _settings.LockWeather = v; }));
            world.Add(ActionEntry("Extra Sunny", "Sunny", null, delegate { SetWeather("EXTRASUNNY"); }));
            world.Add(ActionEntry("Clear", "Clear", null, delegate { SetWeather("CLEAR"); }));
            world.Add(ActionEntry("Clouds", "Clouds", null, delegate { SetWeather("CLOUDS"); }));
            world.Add(ActionEntry("Rain", "Rain", null, delegate { SetWeather("RAIN"); }));
            world.Add(ActionEntry("Thunderstorm", "Thunder", null, delegate { SetWeather("THUNDER"); }));
            world.Add(ActionEntry("Snow", "Snow", null, delegate { SetWeather("SNOW"); }));
            world.Add(ActionEntry("Blizzard", "Blizzard", null, delegate { SetWeather("BLIZZARD"); }));
            world.Add(ActionEntry("Halloween", "Halloween", null, delegate { SetWeather("HALLOWEEN"); }));
            world.Add(ActionEntry("Time: Morning", "Morning", null, delegate { SetClockTime(6, 0); }));
            world.Add(ActionEntry("Time: Noon", "Noon", null, delegate { SetClockTime(12, 0); }));
            world.Add(ActionEntry("Time: Evening", "Evening", null, delegate { SetClockTime(18, 0); }));
            world.Add(ActionEntry("Time: Midnight", "Midnight", null, delegate { SetClockTime(0, 0); }));
            world.Add(ActionEntry("Clear trainer vehicles, bodyguards & litter", "ClearTrainerWorld", "Removes only entities created by this trainer plus known loose litter nearby. Mission entities and occupied vehicles are protected.", ClearTrainerSpawnedWorldEntities));
            world.Add(ActionEntry("Clear nearby traffic & peds", "ClearArea", "Legacy area cleanup. Can remove nearby ambient traffic and pedestrians; avoid using during missions.", ClearNearbyArea));
            world.Add(BackEntry());

            List<MenuEntry> addons = NewPage("Addons");
            addons.Add(new MenuEntry("Selected add-on", "Current selection", delegate { return GetSelectedAddonLabel(); }, null, null));
            addons.Add(ActionEntry("Previous add-on", "PreviousAddon", null, PreviousAddon));
            addons.Add(ActionEntry("Next add-on", "NextAddon", null, NextAddon));
            addons.Add(ActionEntry("Spawn selected add-on", "SpawnAddon", null, SpawnSelectedAddon));
            addons.Add(ActionEntry("Reload addons.json", "ReloadAddons", null, delegate
            {
                if (LoadAddonVehicles()) Notify("Add-on list reloaded.");
            }));
            addons.Add(BackEntry());

            NewPage("SavedVehicles");
            RebuildSavedVehiclesMenu();

            List<MenuEntry> settings = NewPage("Settings");
            settings.Add(PageEntry("Interface customization", "Interface", "Position, width, scale, font, colors and transparency."));
            settings.Add(PageEntry("Custom hotkeys", "Hotkeys", "Assign single-key shortcuts and detect conflicts."));
            settings.Add(PageEntry("Developer tools", "DeveloperTools", "Reload JSON data and developer configuration."));
            settings.Add(ActionEntry("Save settings", "Save", null, delegate { SaveSettings(); Notify("Settings saved."); }));
            settings.Add(ActionEntry("Reload settings", "Reload", null, delegate { LoadSettings(); Notify("Settings reloaded."); }));
            settings.Add(ActionEntry("Reload add-on list", "ReloadAddons", null, delegate
            {
                if (LoadAddonVehicles()) Notify("Add-on list reloaded.");
            }));
            settings.Add(BackEntry());

            List<MenuEntry> hotkeys = NewPage("Hotkeys");
            hotkeys.Add(ValueActionEntry("Menu open / close", "Select to wait for a key.", delegate { return GetHotkeyLabel("MenuToggle"); }, delegate { BeginHotkeyCapture("MenuToggle"); }));
            hotkeys.Add(ValueActionEntry("Menu up", "Select to wait for a key.", delegate { return GetHotkeyLabel("MenuUp"); }, delegate { BeginHotkeyCapture("MenuUp"); }));
            hotkeys.Add(ValueActionEntry("Menu down", "Select to wait for a key.", delegate { return GetHotkeyLabel("MenuDown"); }, delegate { BeginHotkeyCapture("MenuDown"); }));
            hotkeys.Add(ValueActionEntry("Menu select", "Select to wait for a key.", delegate { return GetHotkeyLabel("MenuSelect"); }, delegate { BeginHotkeyCapture("MenuSelect"); }));
            hotkeys.Add(ValueActionEntry("Menu back", "Select to wait for a key. ESC remains the emergency close/cancel key.", delegate { return GetHotkeyLabel("MenuBack"); }, delegate { BeginHotkeyCapture("MenuBack"); }));
            hotkeys.Add(ValueActionEntry("Vehicle rockets", "Select to wait for a key.", delegate { return GetHotkeyLabel("VehicleRockets"); }, delegate { BeginHotkeyCapture("VehicleRockets"); }));
            hotkeys.Add(ActionEntry("Restore default hotkeys", "HotkeysDefault", null, ResetHotkeys));
            hotkeys.Add(BackEntry());

            List<MenuEntry> developerTools = NewPage("DeveloperTools");
            developerTools.Add(ActionEntry("Reload JSON configurations", "ReloadJsonConfigurations", "Reload hotkeys, vehicle catalogs, favorite locations and saved vehicles.", ReloadJsonConfigurations));
            developerTools.Add(BackEntry());

            List<MenuEntry> interfacePage = NewPage("Interface");
            interfacePage.Add(ValueActionEntry("Move left", "Shift the menu toward the left edge.", delegate { return _settings.MenuX.ToString("0.000", CultureInfo.InvariantCulture); }, delegate { AdjustMenuPosition(-0.02f, 0.0f); }));
            interfacePage.Add(ValueActionEntry("Move right", "Shift the menu toward the right edge.", delegate { return _settings.MenuX.ToString("0.000", CultureInfo.InvariantCulture); }, delegate { AdjustMenuPosition(0.02f, 0.0f); }));
            interfacePage.Add(ValueActionEntry("Move up", "Shift the menu upward.", delegate { return _settings.MenuY.ToString("0.000", CultureInfo.InvariantCulture); }, delegate { AdjustMenuPosition(0.0f, -0.02f); }));
            interfacePage.Add(ValueActionEntry("Move down", "Shift the menu downward.", delegate { return _settings.MenuY.ToString("0.000", CultureInfo.InvariantCulture); }, delegate { AdjustMenuPosition(0.0f, 0.02f); }));
            interfacePage.Add(ValueActionEntry("Menu width -", "Make the menu narrower.", delegate { return _settings.MenuWidth.ToString("0.00", CultureInfo.InvariantCulture); }, delegate { ChangeMenuWidth(-0.01f); }));
            interfacePage.Add(ValueActionEntry("Menu width +", "Make the menu wider.", delegate { return _settings.MenuWidth.ToString("0.00", CultureInfo.InvariantCulture); }, delegate { ChangeMenuWidth(0.01f); }));
            interfacePage.Add(ValueActionEntry("Menu scale -", "Reduce the overall menu size.", delegate { return _settings.MenuScale.ToString("0.00", CultureInfo.InvariantCulture); }, delegate { ChangeMenuScale(-0.05f); }));
            interfacePage.Add(ValueActionEntry("Menu scale +", "Increase the overall menu size.", delegate { return _settings.MenuScale.ToString("0.00", CultureInfo.InvariantCulture); }, delegate { ChangeMenuScale(0.05f); }));
            interfacePage.Add(ValueActionEntry("Font size -", "Reduce menu text size.", delegate { return _settings.MenuFontScale.ToString("0.00", CultureInfo.InvariantCulture); }, delegate { ChangeMenuFontScale(-0.05f); }));
            interfacePage.Add(ValueActionEntry("Font size +", "Increase menu text size.", delegate { return _settings.MenuFontScale.ToString("0.00", CultureInfo.InvariantCulture); }, delegate { ChangeMenuFontScale(0.05f); }));
            interfacePage.Add(ValueActionEntry("Font style", "Cycle through readable GTA font styles.", delegate { return GetMenuFontName(); }, CycleMenuFont));
            interfacePage.Add(ValueActionEntry("Background opacity -", "Make the menu background more transparent.", delegate { return _settings.MenuOpacity.ToString(CultureInfo.InvariantCulture); }, delegate { ChangeMenuOpacity(-15); }));
            interfacePage.Add(ValueActionEntry("Background opacity +", "Make the menu background less transparent.", delegate { return _settings.MenuOpacity.ToString(CultureInfo.InvariantCulture); }, delegate { ChangeMenuOpacity(15); }));
            interfacePage.Add(ValueActionEntry("Visible rows -", "Show fewer entries at once.", delegate { return _settings.MenuRows.ToString(CultureInfo.InvariantCulture); }, delegate { ChangeMenuRows(-1); }));
            interfacePage.Add(ValueActionEntry("Visible rows +", "Show more entries at once.", delegate { return _settings.MenuRows.ToString(CultureInfo.InvariantCulture); }, delegate { ChangeMenuRows(1); }));
            interfacePage.Add(ActionEntry("Reset interface settings", "ResetInterface", null, ResetMenuInterfaceSettings));
            interfacePage.Add(ActionEntry("Save interface settings", "SaveInterface", null, delegate { SaveSettings(); Notify("Interface settings saved."); }));
            interfacePage.Add(BackEntry());

            NewPage("Locations");
            RebuildFavoriteLocationsMenu();
        }

        private List<MenuEntry> NewPage(string name)
        {
            List<MenuEntry> list = new List<MenuEntry>();
            _pages[name] = list;
            return list;
        }

        private MenuEntry PageEntry(string title, string page, string description)
        {
            return new MenuEntry(title, description, null, page, null);
        }

        private MenuEntry BackEntry()
        {
            return ActionEntry("Back", "Back", "Return to the previous menu.", delegate
            {
                if (_pageStack.Count > 0) _currentPage = _pageStack.Pop();
                else _currentPage = "Main";
                _selectedIndex = 0;
                _scrollOffset = 0;
            });
        }

        private MenuEntry ActionEntry(string title, string key, string description, Action action)
        {
            Action safeAction = null;
            if (action != null)
            {
                safeAction = delegate
                {
                    try { action(); }
                    catch (Exception ex)
                    {
                        LogException("Action " + key, ex);
                        NotifyError(title);
                    }
                };
            }
            return new MenuEntry(title, description, null, null, safeAction);
        }

        private MenuEntry ValueActionEntry(string title, string description, Func<string> value, Action action)
        {
            Action safeAction = null;
            if (action != null)
            {
                safeAction = delegate
                {
                    try { action(); }
                    catch (Exception ex)
                    {
                        LogException("Interface setting " + title, ex);
                        NotifyError(title);
                    }
                };
            }
            return new MenuEntry(title, description, value, null, safeAction);
        }

        private MenuEntry ToggleEntry(string title, string key, Func<bool> getter, Action<bool> setter)
        {
            return new MenuEntry(title, key, delegate { return getter() ? "ON" : "OFF"; }, null, delegate
            {
                try
                {
                    bool newValue = !getter();
                    setter(newValue);
                    SaveSettings();
                }
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogException("Toggle " + key, ex);
                    NotifyError(title);
                }
            });
        }

        private List<MenuEntry> GetCurrentEntries()
        {
            if (String.IsNullOrEmpty(_currentPage) || !_pages.ContainsKey(_currentPage)) return new List<MenuEntry>();
            return _pages[_currentPage];
        }

        private void KeepSelectionVisible(int count)
        {
            if (_selectedIndex < _scrollOffset) _scrollOffset = _selectedIndex;
            if (_selectedIndex >= _scrollOffset + GetVisibleMenuRows()) _scrollOffset = _selectedIndex - GetVisibleMenuRows() + 1;
            if (_scrollOffset < 0) _scrollOffset = 0;
            if (_scrollOffset > Math.Max(0, count - GetVisibleMenuRows())) _scrollOffset = Math.Max(0, count - GetVisibleMenuRows());
        }

        private void LoadFavoriteLocations()
        {
            if (!File.Exists(_favoriteLocationsPath)) return;
            try
            {
                FileInfo info = new FileInfo(_favoriteLocationsPath);
                if (info.Length > 256 * 1024) throw new InvalidDataException("favorite_locations.json exceeds 256 KB.");
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 256 * 1024;
                serializer.RecursionLimit = 8;
                List<FavoriteLocation> parsed = serializer.Deserialize<List<FavoriteLocation>>(File.ReadAllText(_favoriteLocationsPath));
                if (parsed == null) throw new InvalidDataException("Favorite locations root must be a JSON array.");

                _favoriteLocations.Clear();
                for (int i = 0; i < parsed.Count && _favoriteLocations.Count < 100; i++)
                {
                    FavoriteLocation item = parsed[i];
                    if (item == null || Single.IsNaN(item.x) || Single.IsInfinity(item.x) ||
                        Single.IsNaN(item.y) || Single.IsInfinity(item.y) ||
                        Single.IsNaN(item.z) || Single.IsInfinity(item.z)) continue;
                    if (item.x < -20000.0f || item.x > 20000.0f || item.y < -20000.0f || item.y > 20000.0f ||
                        item.z < -2000.0f || item.z > 10000.0f) continue;
                    string name = SanitizeMenuText(item.name, 48, "Saved location " + (_favoriteLocations.Count + 1).ToString("000"));
                    _favoriteLocations.Add(new FavoriteLocation { name = name, x = item.x, y = item.y, z = item.z });
                }
                Log("Loaded saved locations: " + _favoriteLocations.Count);
            }
            catch (Exception ex)
            {
                LogException("LoadFavoriteLocations", ex);
                Notify("Saved locations could not be loaded. The trainer will continue without them.");
            }
        }

        private void SaveFavoriteLocations()
        {
            try
            {
                Directory.CreateDirectory(_dataDirectory);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 256 * 1024;
                string json = serializer.Serialize(_favoriteLocations);
                string error;
                if (!TryAtomicWriteFile(_favoriteLocationsPath, json, out error))
                    throw new IOException("Atomic save failed: " + error);
                Log("Saved locations written: " + _favoriteLocations.Count);
            }
            catch (Exception ex)
            {
                LogException("SaveFavoriteLocations", ex);
                Notify("Could not save favorite locations. See log.");
            }
        }

        private void RebuildFavoriteLocationsMenu()
        {
            if (!_pages.ContainsKey("Locations")) return;
            List<MenuEntry> page = _pages["Locations"];
            page.Clear();
            page.Add(ActionEntry("Save current position", "SaveFavoriteLocation", "Save the current player position as a quick teleport destination.", SaveCurrentFavoriteLocation));
            if (_favoriteLocations.Count == 0)
            {
                page.Add(new MenuEntry("No saved locations", "Use Save current position to add one.", null, null, null));
            }
            else
            {
                for (int i = 0; i < _favoriteLocations.Count; i++)
                {
                    FavoriteLocation captured = _favoriteLocations[i];
                    string title = captured.name;
                    page.Add(ActionEntry("Go to: " + title, "GoFavorite" + i.ToString(CultureInfo.InvariantCulture),
                        "Teleport to saved coordinates.", delegate { TeleportToFavoriteLocation(captured); }));
                    page.Add(ActionEntry("Delete: " + title, "DeleteFavorite" + i.ToString(CultureInfo.InvariantCulture),
                        "Remove this saved location.", delegate { DeleteFavoriteLocation(captured); }));
                }
                page.Add(ActionEntry("Clear all saved locations", "ClearFavoriteLocations", null, ClearFavoriteLocations));
            }
            page.Add(BackEntry());
        }

        private void SaveCurrentFavoriteLocation()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped))
            {
                Notify("Player is not available.");
                return;
            }
            if (_favoriteLocations.Count >= 100)
            {
                Notify("Saved location limit reached (100).");
                return;
            }
            int sequence = 1;
            string name;
            do
            {
                name = "Saved location " + sequence.ToString("000", CultureInfo.InvariantCulture);
                sequence++;
            }
            while (_favoriteLocations.Any(delegate(FavoriteLocation item) { return String.Equals(item.name, name, StringComparison.OrdinalIgnoreCase); }) && sequence <= 101);

            Vector3 position = ped.Position;
            _favoriteLocations.Add(new FavoriteLocation { name = name, x = position.X, y = position.Y, z = position.Z });
            SaveFavoriteLocations();
            RebuildFavoriteLocationsMenu();
            Notify("Saved: " + name);
        }

        private void TeleportToFavoriteLocation(FavoriteLocation location)
        {
            if (location == null) return;
            TeleportTo(location.x, location.y, location.z + 1.0f);
        }

        private void DeleteFavoriteLocation(FavoriteLocation location)
        {
            if (location == null) return;
            _favoriteLocations.Remove(location);
            SaveFavoriteLocations();
            RebuildFavoriteLocationsMenu();
            Notify("Saved location deleted.");
            _selectedIndex = 0;
            _scrollOffset = 0;
        }

        private void ClearFavoriteLocations()
        {
            _favoriteLocations.Clear();
            SaveFavoriteLocations();
            RebuildFavoriteLocationsMenu();
            Notify("All saved locations cleared.");
            _selectedIndex = 0;
            _scrollOffset = 0;
        }

        private void LoadSavedVehicles()
        {
            string backupPath = _savedVehiclesPath + ".bak";
            if (!File.Exists(_savedVehiclesPath) && !File.Exists(backupPath)) return;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 4 * 1024 * 1024;
                serializer.RecursionLimit = 12;
                List<SavedVehicle> parsed = null;
                Exception primaryReadError = null;
                try
                {
                    if (File.Exists(_savedVehiclesPath))
                    {
                        FileInfo info = new FileInfo(_savedVehiclesPath);
                        if (info.Length > 4 * 1024 * 1024) throw new InvalidDataException("saved_vehicles.json exceeds 4 MB.");
                        parsed = serializer.Deserialize<List<SavedVehicle>>(File.ReadAllText(_savedVehiclesPath));
                    }
                }
                catch (Exception primaryEx) { primaryReadError = primaryEx; }
                if (parsed == null && File.Exists(backupPath))
                {
                    try
                    {
                        FileInfo backupInfo = new FileInfo(backupPath);
                        if (backupInfo.Length > 4 * 1024 * 1024) throw new InvalidDataException("saved_vehicles.json.bak exceeds 4 MB.");
                        parsed = serializer.Deserialize<List<SavedVehicle>>(File.ReadAllText(backupPath));
                        if (parsed != null) Log("Recovered saved vehicle slots from .bak after primary JSON error: " + (primaryReadError == null ? "missing or invalid root" : primaryReadError.Message));
                    }
                    catch (Exception backupEx) { LogException("LoadSavedVehicles backup", backupEx); }
                }
                if (parsed == null) throw new InvalidDataException("Saved vehicle root must be a JSON array; primary and backup could not be loaded.", primaryReadError);
                _savedVehicles.Clear();
                for (int i = 0; i < parsed.Count && _savedVehicles.Count < 50; i++)
                {
                    SavedVehicle item = parsed[i];
                    if (item == null || item.Slot < 1 || item.Slot > 50 || item.ModelHash == 0) continue;
                    if (_savedVehicles.Any(delegate(SavedVehicle existing) { return existing.Slot == item.Slot; })) continue;
                    if (item.Mods == null || item.Mods.Length != 50) item.Mods = CreateDefaultModArray();
                    if (item.ModVariations == null || item.ModVariations.Length != 50) item.ModVariations = new bool[50];
                    if (item.ToggleMods == null || item.ToggleMods.Length != 3) item.ToggleMods = new bool[3];
                    if (item.Extras == null || item.Extras.Length != 15) item.Extras = new bool[15];
                    item.Name = SanitizeMenuText(item.Name, 48, "Vehicle slot " + item.Slot.ToString("00", CultureInfo.InvariantCulture));
                    _savedVehicles.Add(item);
                }
                _savedVehicles.Sort(delegate(SavedVehicle a, SavedVehicle b) { return a.Slot.CompareTo(b.Slot); });
                Log("Loaded saved vehicle slots: " + _savedVehicles.Count);
            }
            catch (Exception ex)
            {
                LogException("LoadSavedVehicles", ex);
                Notify("Saved vehicle slots could not be loaded. Existing file was left untouched.");
            }
        }

        private int[] CreateDefaultModArray()
        {
            int[] values = new int[50];
            for (int i = 0; i < values.Length; i++) values[i] = -1;
            return values;
        }

        private bool SaveSavedVehicles()
        {
            try
            {
                Directory.CreateDirectory(_vehiclesDirectory);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 4 * 1024 * 1024;
                serializer.RecursionLimit = 12;
                string error;
                if (!TryAtomicWriteFile(_savedVehiclesPath, serializer.Serialize(_savedVehicles), out error))
                    throw new IOException("Atomic save failed: " + error);
                Log("Saved vehicle slots written: " + _savedVehicles.Count);
                return true;
            }
            catch (Exception ex)
            {
                LogException("SaveSavedVehicles", ex);
                Notify("Could not save vehicle slots. Existing save was preserved; see AvernesSowtware.log.");
                return false;
            }
        }

        private void RebuildSavedVehiclesMenu()
        {
            List<MenuEntry> page;
            if (!_pages.TryGetValue("SavedVehicles", out page)) return;
            page.Clear();
            page.Add(ActionEntry("Save current vehicle to next free slot", "SaveVehicleNext", "Uses the first empty slot.", SaveCurrentVehicleToNextSlot));
            for (int slot = 1; slot <= 50; slot++)
            {
                SavedVehicle captured = _savedVehicles.FirstOrDefault(delegate(SavedVehicle value) { return value.Slot == slot; });
                if (captured == null)
                {
                    int capturedSlot = slot;
                    page.Add(ActionEntry("Slot " + slot.ToString("00", CultureInfo.InvariantCulture) + " — empty: save here", "SaveVehicleSlot" + slot.ToString(CultureInfo.InvariantCulture), null,
                        delegate { SaveCurrentVehicleToSlot(capturedSlot); }));
                }
                else
                {
                    SavedVehicle capturedVehicle = captured;
                    page.Add(ActionEntry("Slot " + slot.ToString("00", CultureInfo.InvariantCulture) + " — " + capturedVehicle.Name, "SpawnSavedVehicle" + slot.ToString(CultureInfo.InvariantCulture),
                        "Spawn this saved model with its stored modifications.", delegate { SpawnSavedVehicle(capturedVehicle); }));
                    page.Add(ActionEntry("Overwrite slot " + slot.ToString("00", CultureInfo.InvariantCulture), "OverwriteSavedVehicle" + slot.ToString(CultureInfo.InvariantCulture),
                        "Replace the stored build with the vehicle you are driving now.", delegate { SaveCurrentVehicleToSlot(capturedVehicle.Slot); }));
                    page.Add(ActionEntry("Delete slot " + slot.ToString("00", CultureInfo.InvariantCulture), "DeleteSavedVehicle" + slot.ToString(CultureInfo.InvariantCulture),
                        null, delegate { DeleteSavedVehicle(capturedVehicle); }));
                }
            }
            page.Add(BackEntry());
        }

        private void SaveCurrentVehicleToNextSlot()
        {
            for (int slot = 1; slot <= 50; slot++)
            {
                if (!_savedVehicles.Any(delegate(SavedVehicle item) { return item.Slot == slot; }))
                {
                    SaveCurrentVehicleToSlot(slot);
                    return;
                }
            }
            Notify("All 50 vehicle slots are occupied. Overwrite or delete a slot first.");
        }

        private void SaveCurrentVehicleToSlot(int slot)
        {
            Vehicle vehicle;
            if (slot < 1 || slot > 50 || !TryGetCurrentVehicle(out vehicle)) return;
            if (!IsEntityUsable(vehicle)) { Notify("Current vehicle is no longer available."); return; }
            try
            {
                SavedVehicle saved = CaptureSavedVehicle(vehicle, slot);
                SavedVehicle existing = _savedVehicles.FirstOrDefault(delegate(SavedVehicle item) { return item.Slot == slot; });
                if (existing != null) _savedVehicles.Remove(existing);
                _savedVehicles.Add(saved);
                _savedVehicles.Sort(delegate(SavedVehicle a, SavedVehicle b) { return a.Slot.CompareTo(b.Slot); });
                if (!SaveSavedVehicles())
                {
                    _savedVehicles.Remove(saved);
                    if (existing != null) _savedVehicles.Add(existing);
                    _savedVehicles.Sort(delegate(SavedVehicle a, SavedVehicle b) { return a.Slot.CompareTo(b.Slot); });
                    RebuildSavedVehiclesMenu();
                    return;
                }
                RebuildSavedVehiclesMenu();
                Notify("Vehicle saved to slot " + slot.ToString("00", CultureInfo.InvariantCulture) + ": " + saved.Name);
            }
            catch (Exception ex)
            {
                LogException("SaveCurrentVehicleToSlot " + slot, ex);
                Notify("Vehicle could not be saved. See AvernesSowtware.log.");
            }
        }

        private SavedVehicle CaptureSavedVehicle(Vehicle vehicle, int slot)
        {
            if (!IsEntityUsable(vehicle)) throw new InvalidOperationException("Vehicle is no longer available for saving.");
            int handle = vehicle.Handle;
            SavedVehicle saved = new SavedVehicle();
            saved.Slot = slot;
            saved.ModelHash = unchecked((int)Function.Call<uint>(Hash.GET_ENTITY_MODEL, handle));
            saved.Name = GetVehicleDisplayName(saved.ModelHash);
            saved.SavedX = vehicle.Position.X;
            saved.SavedY = vehicle.Position.Y;
            saved.SavedZ = vehicle.Position.Z;
            saved.SavedHeading = vehicle.Heading;
            saved.Mods = CreateDefaultModArray();
            saved.ModVariations = new bool[50];
            saved.ToggleMods = new bool[3];
            saved.Extras = new bool[15];
            saved.NeonEnabled = new bool[4];

            Function.Call((Hash)0x1F2AA07F00B3217AUL, handle, 0); // SET_VEHICLE_MOD_KIT
            for (int modType = 0; modType < 50; modType++)
            {
                try
                {
                    int modIndex = Function.Call<int>((Hash)0x772960298DA26FDBUL, handle, modType);
                    saved.Mods[modType] = modIndex;
                    if (modIndex >= 0)
                        saved.ModVariations[modType] = Function.Call<bool>((Hash)0xB3924ECD70E095DCUL, handle, modType);
                }
                catch (Exception ex) { LogException("Capture vehicle mod " + modType, ex); }
            }
            int[] toggleTypes = new int[] { 18, 20, 22 };
            for (int i = 0; i < toggleTypes.Length; i++)
            {
                try { saved.ToggleMods[i] = Function.Call<bool>((Hash)0x84B233A8C8FC8AE7UL, handle, toggleTypes[i]); }
                catch (Exception ex) { LogException("Capture vehicle toggle mod " + toggleTypes[i], ex); }
            }
            saved.WheelType = Function.Call<int>((Hash)0xB3ED1BFB4BE636DCUL, handle);
            using (OutputArgument primary = new OutputArgument())
            using (OutputArgument secondary = new OutputArgument())
            {
                Function.Call((Hash)0xA19435F193E081ACUL, handle, primary, secondary); // GET_VEHICLE_COLOURS
                saved.PrimaryColor = primary.GetResult<int>();
                saved.SecondaryColor = secondary.GetResult<int>();
            }
            using (OutputArgument pearl = new OutputArgument())
            using (OutputArgument wheel = new OutputArgument())
            {
                Function.Call((Hash)0x3BC4245933A166F7UL, handle, pearl, wheel); // GET_VEHICLE_EXTRA_COLOURS
                saved.PearlescentColor = pearl.GetResult<int>();
                saved.WheelColor = wheel.GetResult<int>();
            }
            using (OutputArgument colorType = new OutputArgument())
            using (OutputArgument colorIndex = new OutputArgument())
            using (OutputArgument pearlescent = new OutputArgument())
            {
                Function.Call((Hash)0xE8D65CA700C9A693UL, handle, colorType, colorIndex, pearlescent);
                saved.ModColor1Type = colorType.GetResult<int>();
                saved.ModColor1Index = colorIndex.GetResult<int>();
                saved.ModColor1Pearlescent = pearlescent.GetResult<int>();
            }
            using (OutputArgument colorType = new OutputArgument())
            using (OutputArgument colorIndex = new OutputArgument())
            {
                Function.Call((Hash)0x81592BE4E3878728UL, handle, colorType, colorIndex);
                saved.ModColor2Type = colorType.GetResult<int>();
                saved.ModColor2Index = colorIndex.GetResult<int>();
            }
            saved.PrimaryCustom = Function.Call<bool>((Hash)0xF095C0405307B21BUL, handle);
            if (saved.PrimaryCustom)
            {
                using (OutputArgument red = new OutputArgument())
                using (OutputArgument green = new OutputArgument())
                using (OutputArgument blue = new OutputArgument())
                {
                    Function.Call((Hash)0xB64CF2CCA9D95F52UL, handle, red, green, blue);
                    saved.PrimaryRed = red.GetResult<int>(); saved.PrimaryGreen = green.GetResult<int>(); saved.PrimaryBlue = blue.GetResult<int>();
                }
            }
            saved.SecondaryCustom = Function.Call<bool>((Hash)0x910A32E7AAD2656CUL, handle);
            if (saved.SecondaryCustom)
            {
                using (OutputArgument red = new OutputArgument())
                using (OutputArgument green = new OutputArgument())
                using (OutputArgument blue = new OutputArgument())
                {
                    Function.Call((Hash)0x8389CD56CA8072DCUL, handle, red, green, blue);
                    saved.SecondaryRed = red.GetResult<int>(); saved.SecondaryGreen = green.GetResult<int>(); saved.SecondaryBlue = blue.GetResult<int>();
                }
            }
            saved.WindowTint = Function.Call<int>((Hash)0x0EE21293DAD47C95UL, handle);
            saved.XenonColor = Function.Call<int>((Hash)0x3DFF319A831E0CDBUL, handle);
            using (OutputArgument interiorColor = new OutputArgument())
            {
                Function.Call((Hash)0x7D1464D472D32136UL, handle, interiorColor);
                saved.InteriorColor = interiorColor.GetResult<int>();
            }
            using (OutputArgument dashboardColor = new OutputArgument())
            {
                Function.Call((Hash)0xB7635E80A5C31BFFUL, handle, dashboardColor);
                saved.DashboardColor = dashboardColor.GetResult<int>();
            }
            saved.PlateText = Function.Call<string>((Hash)0x7CE1CCB9B293020EUL, handle);
            saved.PlateStyle = Function.Call<int>((Hash)0xF11BC2DD9A3E7195UL, handle);
            saved.Livery = Function.Call<int>((Hash)0x2BB9230590DA5E8AUL, handle);
            using (OutputArgument red = new OutputArgument())
            using (OutputArgument green = new OutputArgument())
            using (OutputArgument blue = new OutputArgument())
            {
                Function.Call((Hash)0x7619EEE8C886757FUL, handle, red, green, blue);
                saved.NeonRed = red.GetResult<int>(); saved.NeonGreen = green.GetResult<int>(); saved.NeonBlue = blue.GetResult<int>();
            }
            using (OutputArgument smokeRed = new OutputArgument())
            using (OutputArgument smokeGreen = new OutputArgument())
            using (OutputArgument smokeBlue = new OutputArgument())
            {
                Function.Call((Hash)0xB635392A4938B3C3UL, handle, smokeRed, smokeGreen, smokeBlue);
                saved.TyreSmokeRed = smokeRed.GetResult<int>();
                saved.TyreSmokeGreen = smokeGreen.GetResult<int>();
                saved.TyreSmokeBlue = smokeBlue.GetResult<int>();
            }
            for (int side = 0; side < 4; side++)
                saved.NeonEnabled[side] = Function.Call<bool>((Hash)0x8C4B92553E4766A5UL, handle, side);
            for (int extra = 0; extra < 15; extra++)
            {
                if (Function.Call<bool>((Hash)0x1262D55792428154UL, handle, extra))
                    saved.Extras[extra] = Function.Call<bool>((Hash)0xD2E6822DBFD6C8BDUL, handle, extra);
            }
            return saved;
        }

        private string GetVehicleDisplayName(int modelHash)
        {
            try
            {
                string label = Function.Call<string>((Hash)0xB215AAC32D25D019UL, modelHash);
                if (!String.IsNullOrWhiteSpace(label) && !String.Equals(label, "CARNOTFOUND", StringComparison.OrdinalIgnoreCase))
                {
                    string display = Function.Call<string>((Hash)0x7B5280EBA9840C72UL, label);
                    if (!String.IsNullOrWhiteSpace(display) && !String.Equals(display, "NULL", StringComparison.OrdinalIgnoreCase)) return SanitizeMenuText(display, 48, label);
                    return SanitizeMenuText(label, 48, "Vehicle");
                }
            }
            catch (Exception ex) { LogException("GetVehicleDisplayName", ex); }
            return "Vehicle 0x" + unchecked((uint)modelHash).ToString("X8", CultureInfo.InvariantCulture);
        }

        private void SpawnSavedVehicle(SavedVehicle saved)
        {
            if (saved == null || saved.ModelHash == 0) { Notify("Saved vehicle slot is invalid."); return; }
            if (_vehicleSpawnPending) { Notify("A vehicle is already loading. Please wait."); return; }
            if (DateTime.UtcNow < _nextVehicleSpawnAllowedUtc) { Notify("Wait briefly before spawning another vehicle."); return; }
            try
            {
                Model model = new Model(saved.ModelHash);
                if (!model.IsValid || !model.IsInCdImage || !model.IsVehicle)
                {
                    LogFailure("Saved vehicle spawn", "Stored model is unavailable: 0x" + unchecked((uint)saved.ModelHash).ToString("X8", CultureInfo.InvariantCulture));
                    Notify("This saved vehicle model is unavailable in the current game build.");
                    return;
                }
                _pendingVehicleModelHash = saved.ModelHash;
                _pendingVehicleModelName = "0x" + unchecked((uint)saved.ModelHash).ToString("X8", CultureInfo.InvariantCulture);
                _pendingVehicleDisplayName = saved.Name;
                _pendingSavedVehicle = saved;
                _pendingVehicleSpawnDeadlineUtc = DateTime.UtcNow.AddSeconds(15);
                _vehicleSpawnPending = true;
                _nextVehicleSpawnAllowedUtc = DateTime.UtcNow.AddMilliseconds(1100);
                Function.Call((Hash)0x963D27A58DF860ACUL, _pendingVehicleModelHash);
                Notify("Loading saved vehicle: " + saved.Name);
                Log("Requested saved vehicle slot " + saved.Slot + "; hash=" + _pendingVehicleModelHash);
            }
            catch (Exception ex)
            {
                CancelPendingVehicleSpawn();
                LogException("SpawnSavedVehicle", ex);
                Notify("Saved vehicle spawn could not be started.");
            }
        }

        private void ApplySavedVehicleTuning(int handle, SavedVehicle saved)
        {
            if (saved == null || !IsVehicleHandleUsable(handle)) return;
            try { Function.Call((Hash)0x1F2AA07F00B3217AUL, handle, 0); } catch (Exception ex) { LogException("Restore vehicle mod kit", ex); }
            try { Function.Call((Hash)0x487EB21CC7295BA1UL, handle, saved.WheelType); } catch (Exception ex) { LogException("Restore wheel type", ex); }
            if (saved.Mods != null)
            {
                for (int modType = 0; modType < Math.Min(50, saved.Mods.Length); modType++)
                {
                    int index = saved.Mods[modType];
                    if (index < 0) continue;
                    try
                    {
                        int count = Function.Call<int>((Hash)0xE38E9162A2500646UL, handle, modType);
                        if (index < count)
                            Function.Call((Hash)0x6AF0636DDEDCB6DDUL, handle, modType, index,
                                saved.ModVariations != null && modType < saved.ModVariations.Length ? saved.ModVariations[modType] : false);
                    }
                    catch (Exception ex) { LogException("Restore saved mod " + modType, ex); }
                }
            }
            int[] toggleTypes = new int[] { 18, 20, 22 };
            for (int i = 0; i < toggleTypes.Length; i++)
            {
                try { Function.Call((Hash)0x2A1F4F37F95BAD08UL, handle, toggleTypes[i], saved.ToggleMods != null && i < saved.ToggleMods.Length && saved.ToggleMods[i]); }
                catch (Exception ex) { LogException("Restore saved toggle " + toggleTypes[i], ex); }
            }
            try
            {
                Function.Call((Hash)0x55E1D2758F34E437UL, handle);
                Function.Call((Hash)0x5FFBDEEC3E8E2009UL, handle);
                Function.Call((Hash)0x4F1D4BE3A7F24601UL, handle, saved.PrimaryColor, saved.SecondaryColor);
                Function.Call((Hash)0x43FEB945EE7F85B8UL, handle, saved.ModColor1Type, saved.ModColor1Index, saved.ModColor1Pearlescent);
                Function.Call((Hash)0x816562BADFDEC83EUL, handle, saved.ModColor2Type, saved.ModColor2Index);
                Function.Call((Hash)0x2036F561ADD12E33UL, handle, saved.PearlescentColor, saved.WheelColor);
                if (saved.PrimaryCustom) Function.Call((Hash)0x7141766F91D15BEAUL, handle, saved.PrimaryRed, saved.PrimaryGreen, saved.PrimaryBlue);
                if (saved.SecondaryCustom) Function.Call((Hash)0x36CED73BFED89754UL, handle, saved.SecondaryRed, saved.SecondaryGreen, saved.SecondaryBlue);
            }
            catch (Exception ex) { LogException("Restore saved vehicle colours", ex); }
            try { Function.Call((Hash)0x57C51E6BAD752696UL, handle, saved.WindowTint); } catch (Exception ex) { LogException("Restore saved window tint", ex); }
            try { Function.Call((Hash)0xE41033B25D003A07UL, handle, saved.XenonColor); } catch (Exception ex) { LogException("Restore saved xenon color", ex); }
            try { Function.Call((Hash)0xF40DD601A65F7F19UL, handle, saved.InteriorColor); } catch (Exception ex) { LogException("Restore saved interior color", ex); }
            try { Function.Call((Hash)0x6089CDF6A57F326CUL, handle, saved.DashboardColor); } catch (Exception ex) { LogException("Restore saved dashboard color", ex); }
            try
            {
                if (!String.IsNullOrWhiteSpace(saved.PlateText)) Function.Call((Hash)0x95A88F0B409CDA47UL, handle, saved.PlateText);
                Function.Call((Hash)0x9088EB5A43FFB0A1UL, handle, saved.PlateStyle);
            }
            catch (Exception ex) { LogException("Restore saved plate", ex); }
            try { if (saved.Livery >= 0) Function.Call((Hash)0x60BF608F1B8CD1B6UL, handle, saved.Livery); }
            catch (Exception ex) { LogException("Restore saved livery", ex); }
            try
            {
                for (int side = 0; side < 4; side++)
                    Function.Call((Hash)0x2AA720E4287BF269UL, handle, side, saved.NeonEnabled != null && side < saved.NeonEnabled.Length && saved.NeonEnabled[side]);
                Function.Call((Hash)0x8E0A582209A62695UL, handle, saved.NeonRed, saved.NeonGreen, saved.NeonBlue);
            }
            catch (Exception ex) { LogException("Restore saved neon", ex); }
            try { Function.Call((Hash)0xB5BA80F839791C0FUL, handle, saved.TyreSmokeRed, saved.TyreSmokeGreen, saved.TyreSmokeBlue); }
            catch (Exception ex) { LogException("Restore saved tyre smoke color", ex); }
            if (saved.Extras != null)
            {
                for (int extra = 0; extra < Math.Min(15, saved.Extras.Length); extra++)
                {
                    try
                    {
                        if (Function.Call<bool>((Hash)0x1262D55792428154UL, handle, extra))
                            Function.Call((Hash)0x7EE3A3C5E4A40CC9UL, handle, extra, !saved.Extras[extra]);
                    }
                    catch (Exception ex) { LogException("Restore vehicle extra " + extra, ex); }
                }
            }
            Log("Restored saved vehicle slot " + saved.Slot + "; model=" + saved.ModelHash + "; mod entries=" + (saved.Mods == null ? 0 : saved.Mods.Count(delegate(int value) { return value >= 0; })) + ".");
        }

        private void DeleteSavedVehicle(SavedVehicle saved)
        {
            if (saved == null) return;
            int oldIndex = _savedVehicles.IndexOf(saved);
            _savedVehicles.Remove(saved);
            if (!SaveSavedVehicles())
            {
                if (oldIndex < 0 || oldIndex > _savedVehicles.Count) _savedVehicles.Add(saved);
                else _savedVehicles.Insert(oldIndex, saved);
                RebuildSavedVehiclesMenu();
                return;
            }
            RebuildSavedVehiclesMenu();
            Notify("Saved vehicle slot " + saved.Slot.ToString("00", CultureInfo.InvariantCulture) + " deleted.");
            _selectedIndex = 0;
            _scrollOffset = 0;
        }

        private int GetVisibleMenuRows()
        {
            return ClampInt(_settings.MenuRows, 6, 12);
        }

        private static float ClampFloat(float value, float minimum, float maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private static int ClampInt(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private void SaveInterfaceAdjustment(string message)
        {
            SaveSettings();
            if (!String.IsNullOrEmpty(message)) Notify(message);
        }

        private float GetMaximumMenuX()
        {
            float width = ClampFloat(_settings.MenuWidth, 0.28f, 0.44f) * ClampFloat(_settings.MenuScale, 0.75f, 1.30f);
            return Math.Max(0.005f, 0.995f - width);
        }

        private float GetMaximumMenuY()
        {
            float scale = ClampFloat(_settings.MenuScale, 0.75f, 1.30f);
            float estimatedHeight = (0.008f + 0.105f + GetVisibleMenuRows() * 0.038f + 0.038f) * scale;
            return Math.Max(0.005f, 0.995f - estimatedHeight);
        }

        private void AdjustMenuPosition(float dx, float dy)
        {
            _settings.MenuX = ClampFloat(_settings.MenuX + dx, 0.005f, GetMaximumMenuX());
            _settings.MenuY = ClampFloat(_settings.MenuY + dy, 0.005f, GetMaximumMenuY());
            SaveInterfaceAdjustment("Menu position updated.");
        }

        private void ChangeMenuWidth(float delta)
        {
            _settings.MenuWidth = ClampFloat(_settings.MenuWidth + delta, 0.28f, 0.44f);
            _settings.MenuX = ClampFloat(_settings.MenuX, 0.005f, GetMaximumMenuX());
            SaveInterfaceAdjustment("Menu width updated.");
        }

        private void ChangeMenuScale(float delta)
        {
            _settings.MenuScale = ClampFloat(_settings.MenuScale + delta, 0.75f, 1.30f);
            _settings.MenuX = ClampFloat(_settings.MenuX, 0.005f, GetMaximumMenuX());
            _settings.MenuY = ClampFloat(_settings.MenuY, 0.005f, GetMaximumMenuY());
            SaveInterfaceAdjustment("Menu scale updated.");
        }

        private void ChangeMenuFontScale(float delta)
        {
            _settings.MenuFontScale = ClampFloat(_settings.MenuFontScale + delta, 0.70f, 1.40f);
            SaveInterfaceAdjustment("Menu font size updated.");
        }

        private void ChangeMenuOpacity(int delta)
        {
            _settings.MenuOpacity = ClampInt(_settings.MenuOpacity + delta, 100, 255);
            SaveInterfaceAdjustment("Menu opacity updated.");
        }

        private void ChangeMenuRows(int delta)
        {
            _settings.MenuRows = ClampInt(_settings.MenuRows + delta, 6, 12);
            _settings.MenuY = ClampFloat(_settings.MenuY, 0.005f, GetMaximumMenuY());
            KeepSelectionVisible(GetCurrentEntries().Count);
            SaveInterfaceAdjustment("Visible menu rows updated.");
        }

        private void CycleMenuFont()
        {
            _settings.MenuFontStyle = (_settings.MenuFontStyle + 1) % 4;
            SaveInterfaceAdjustment("Font: " + GetMenuFontName());
        }

        private void CycleMenuTheme()
        {
            _settings.MenuTheme = (_settings.MenuTheme + 1) % 5;
            SaveInterfaceAdjustment("Color theme: " + GetMenuThemeName());
        }

        private string GetMenuFontName()
        {
            string[] names = new string[] { "Standard", "Monospace", "Condensed", "Script" };
            return names[ClampInt(_settings.MenuFontStyle, 0, names.Length - 1)];
        }

        private string GetMenuThemeName()
        {
            string[] names = new string[] { "Red", "Blue", "Purple", "Green", "Monochrome" };
            return names[ClampInt(_settings.MenuTheme, 0, names.Length - 1)];
        }

        private void GetMenuThemeColor(out int red, out int green, out int blue)
        {
            // 1.3.0 uses the fixed black/red identity; legacy MENU.THEME remains
            // read and written for config compatibility but no longer changes palette.
            red = 210;
            green = 35;
            blue = 55;
        }

        private void ResetMenuInterfaceSettings()
        {
            _settings.MenuX = 0.650f;
            _settings.MenuY = 0.006f;
            _settings.MenuWidth = 0.380f;
            _settings.MenuScale = 1.0f;
            _settings.MenuFontScale = 1.0f;
            _settings.MenuFontStyle = 0;
            _settings.MenuTheme = 0;
            _settings.MenuOpacity = 225;
            _settings.MenuRows = 9;
            _selectedIndex = 0;
            _scrollOffset = 0;
            SaveInterfaceAdjustment("Interface settings reset.");
        }






        private void DrawText(string text, float x, float y, float scale, int r, int g, int b, int a)
        {
            DrawTextWithFont(text, x, y, scale, r, g, b, a, 0);
        }

        private void DrawTextWithFont(string text, float x, float y, float scale, int r, int g, int b, int a, int font)
        {
            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, r, g, b, a);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.SET_TEXT_CENTRE, false);
            Function.Call((Hash)0x25FBB336DF1804CBUL, "STRING");
            Function.Call((Hash)0x6C188BE134E074AAUL, text);
            Function.Call((Hash)0xCD015E5BB0D96A57UL, x, y, 0);
        }

        private void DrawRect(float x, float y, float width, float height, int r, int g, int b, int a)
        {
            Function.Call(Hash.DRAW_RECT, x, y, width, height, r, g, b, a);
        }

        private void Notify(string message)
        {
            if (_aborted || String.IsNullOrEmpty(message)) return;
            try
            {
                if (Game.Player == null) return;
                Ped currentPlayer = Game.Player.Character;
                if (currentPlayer == null || currentPlayer.Handle == 0) return;
                Function.Call((Hash)0xB87A37EEB7FAA67DUL, "STRING");
                Function.Call((Hash)0x6C188BE134E074AAUL, message);
                Function.Call((Hash)0x9D77056A530643F6UL, 3000, true);
            }
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppendLogFile(_errorLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                    " [WARN] Notification skipped: " + ex.GetType().FullName + ": " + ex.Message + Environment.NewLine);
            }
        }

        private void ApplyLegacyPlayerEffects(Ped ped)
        {
            if (!IsEntityUsable(ped)) return;
            if (_settings.NoClip)
            {
                ApplyNoClipMovement(ped);
                _noClipWasActive = true;
            }
            else if (_noClipWasActive)
            {
                RestoreNoClip();
            }
            Function.Call((Hash)0x18F621F7A5B1F85DUL, _settings.NightVision);
            bool thermalScopeOwnsVision = false;
            try
            {
                uint selectedWeapon = Function.Call<uint>(Hash.GET_SELECTED_PED_WEAPON, ped.Handle);
                uint heavySniperMkII = GetHash("WEAPON_HEAVYSNIPER_MK2");
                uint thermalScope = GetHash("COMPONENT_AT_SCOPE_THERMAL");
                bool scopeInstalled = selectedWeapon == heavySniperMkII && thermalScope != 0 &&
                    Function.Call<bool>((Hash)0xC593212475FAE340UL, ped.Handle, heavySniperMkII, thermalScope);
                bool aiming = Function.Call<bool>((Hash)0x2E397FD2ECD37C87UL, Game.Player);
                thermalScopeOwnsVision = scopeInstalled && aiming;
            }
            catch { thermalScopeOwnsVision = false; }
            // The game's thermal optic toggles its own see-through state while aiming.
            // Do not force SET_SEETHROUGH(false) over that native weapon-accessory state.
            if (!thermalScopeOwnsVision)
                Function.Call((Hash)0x7E08924259E08CE0UL, _settings.ThermalVision);
        }

        private void ApplyNoClipMovement(Ped ped)
        {
            Function.Call((Hash)0x1A9205C1B9EE827FUL, ped.Handle, false, false);
            Vector3 forward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, ped.Handle);
            Vector3 right = new Vector3(forward.Y, -forward.X, 0.0f);
            Vector3 position = ped.Position;
            float speed = IsControlPressed(21) ? 0.75f : 0.28f;
            if (IsControlPressed(32)) position += forward * speed;
            if (IsControlPressed(33)) position -= forward * speed;
            if (IsControlPressed(34)) position -= right * speed;
            if (IsControlPressed(35)) position += right * speed;
            if (IsControlPressed(22)) position.Z += speed;
            if (IsControlPressed(36)) position.Z -= speed;
            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, ped.Handle, position.X, position.Y, position.Z, false, false, false);
        }

        private bool IsControlPressed(int control)
        {
            return Function.Call<bool>((Hash)0xF3A21BCD95725A4AUL, 0, control);
        }

        private void RestoreNoClip()
        {
            try
            {
                Ped ped = Game.Player.Character;
                if (IsEntityUsable(ped))
                    Function.Call((Hash)0x1A9205C1B9EE827FUL, ped.Handle, true, true);
                _noClipWasActive = false;
            }
            catch (Exception ex) { LogException("RestoreNoClip", ex); }
        }




        private void SetWantedLevel(int level)
        {
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player, level, false);
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
            Notify("Wanted level set to " + level + ".");
        }

        private void CleanPlayer()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            Function.Call(Hash.CLEAR_PED_BLOOD_DAMAGE, ped.Handle);
            Function.Call((Hash)0x9C720776DAA43E7EUL, ped.Handle);
            Function.Call((Hash)0x6585D955A68452A5UL, ped.Handle);
            Notify("Clothes and character cleaned.");
        }

        private void SuicidePlayer()
        {
            Ped ped = Game.Player.Character;
            if (IsEntityUsable(ped))
                Function.Call(Hash.SET_ENTITY_HEALTH, ped.Handle, 0);
        }

        private void RemoveAllWeapons()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            Function.Call((Hash)0xF25DF915FA38C5F3UL, ped.Handle, true);
            // AutoGiveWeapons is a one-time startup action; do not immediately re-grant
            // the arsenal on the next Tick after the user explicitly removes it.
            _autoLoadoutGranted = true;
            Notify("All weapons removed.");
        }
















        private void CaptureSkinState(Ped ped)
        {
            if (_skinSnapshotCaptured || !IsEntityUsable(ped)) return;
            _skinRestorePosition = ped.Position;
            _skinRestoreHeading = ped.Heading;
            _skinRestoreHealth = ped.Health;
            _skinRestoreArmour = Function.Call<int>(Hash.GET_PED_ARMOUR, ped.Handle);
            _skinSelectedWeapon = Function.Call<uint>(Hash.GET_SELECTED_PED_WEAPON, ped.Handle);
            _skinWeaponSnapshot.Clear();

            for (int i = 0; i < _weaponNames.Length; i++)
            {
                uint weaponHash = GetHash(_weaponNames[i]);
                if (weaponHash == 0 || !Function.Call<bool>(Hash.IS_WEAPON_VALID, weaponHash)) continue;
                if (!Function.Call<bool>((Hash)0x8DECB02F88F428BCUL, ped.Handle, weaponHash, false)) continue;
                int ammo = Function.Call<int>((Hash)0x015A522136D7F951UL, ped.Handle, weaponHash);
                _skinWeaponSnapshot.Add(new WeaponSnapshot(weaponHash, Math.Max(0, ammo)));
            }
            _skinSnapshotCaptured = true;
            Log("Captured skin-change state. Preserved weapons: " + _skinWeaponSnapshot.Count);
        }

        private void QueueSkinStateRestore()
        {
            _pendingSkinStateRestore = true;
            _skinRestoreFrames = 12;
        }

        private void ProcessPendingSkinStateRestore()
        {
            if (!_pendingSkinStateRestore) return;
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;

            try
            {
                // Re-apply the original location and equipment while the game replaces the
                // player ped. If a protagonist model request starts the cinematic character
                // switch, stop that sequence and keep the replacement ped at the current spot.
                if (Function.Call<bool>((Hash)0xD9D2CFFF49FAB35FUL))
                    Function.Call((Hash)0x95C0A5BBDC189AA1UL); // STOP_PLAYER_SWITCH
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, ped.Handle,
                    _skinRestorePosition.X, _skinRestorePosition.Y, _skinRestorePosition.Z, false, false, false);
                Function.Call(Hash.SET_ENTITY_HEADING, ped.Handle, _skinRestoreHeading);

                if (_skinRestoreFrames == 12 && _skinSnapshotCaptured)
                {
                    int maxHealth = Function.Call<int>(Hash.GET_ENTITY_MAX_HEALTH, ped.Handle);
                    int health = Math.Max(1, Math.Min(_skinRestoreHealth, maxHealth > 0 ? maxHealth : 200));
                    Function.Call(Hash.SET_ENTITY_HEALTH, ped.Handle, health);
                    Function.Call(Hash.SET_PED_ARMOUR, ped.Handle, _skinRestoreArmour);
                    RestoreSkinWeapons(ped);
                    if (_clearSkinSnapshotAfterRestore)
                    {
                        _skinSnapshotCaptured = false;
                        _skinWeaponSnapshot.Clear();
                        _clearSkinSnapshotAfterRestore = false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogException("Skin state restore", ex);
            }
            finally
            {
                _skinRestoreFrames--;
                if (_skinRestoreFrames <= 0) _pendingSkinStateRestore = false;
            }
        }

        private void RestoreSkinWeapons(Ped ped)
        {
            for (int i = 0; i < _skinWeaponSnapshot.Count; i++)
            {
                WeaponSnapshot item = _skinWeaponSnapshot[i];
                if (item == null) continue;
                try
                {
                    if (!Function.Call<bool>(Hash.IS_WEAPON_VALID, item.Hash)) continue;
                    Function.Call(Hash.GIVE_WEAPON_TO_PED, ped.Handle, item.Hash, item.Ammo, false, false);
                    ApplyPremiumToWeapon(ped, item.Hash);
                }
                catch (Exception ex) { LogException("Restore skin weapon " + item.Hash, ex); }
            }
            try
            {
                if (_skinSelectedWeapon != 0 && Function.Call<bool>(Hash.IS_WEAPON_VALID, _skinSelectedWeapon))
                    Function.Call(Hash.SET_CURRENT_PED_WEAPON, ped.Handle, _skinSelectedWeapon, true);
            }
            catch (Exception ex) { LogException("Restore selected skin weapon", ex); }
        }

        private void ChangePlayerModel(string modelName)
        {
            try
            {
                Ped currentPed = Game.Player.Character;
                if (currentPed == null || currentPed.Handle == 0) return;
                Model model = new Model(modelName);
                if (!model.IsValid || !model.IsInCdImage || !model.Request(5000))
                {
                    Notify("Ped model unavailable: " + modelName);
                    model.MarkAsNoLongerNeeded();
                    return;
                }

                CaptureSkinState(currentPed);
                Function.Call((Hash)0x00A1CADD00108836UL, Game.Player, (uint)model.Hash);
                model.MarkAsNoLongerNeeded();
                QueueSkinStateRestore();
                Notify("Player skin changed. Original weapons and position are being preserved.");
            }
            catch (Exception ex)
            {
                LogException("ChangePlayerModel " + modelName, ex);
                Notify("Could not change player model. See log.");
            }
        }

        private void RestoreOriginalModel()
        {
            if (!_originalModelCaptured || _originalModelHash == 0)
            {
                Notify("Original player model has not been captured yet.");
                return;
            }
            try
            {
                Function.Call((Hash)0x00A1CADD00108836UL, Game.Player, _originalModelHash);
                QueueSkinStateRestore();
                _pendingSkinStateRestore = true;
                _skinRestoreFrames = 12;
                Notify("Original model restored. Weapons, health, and position are being restored.");
                _clearSkinSnapshotAfterRestore = true;
            }
            catch (Exception ex)
            {
                LogException("RestoreOriginalModel", ex);
                Notify("Could not restore original model. See log.");
            }
        }

        private void SpawnBodyguard(string modelName)
        {
            if (_pendingBodyguardDismissals.Count > 0 || _bodyguardGroupCleanupPending)
            {
                Notify("Wait until the current bodyguard dismissal finishes.");
                return;
            }
            try
            {
                Model model = new Model(modelName);
                if (!model.IsValid || !model.IsInCdImage || !model.Request(5000))
                {
                    Notify("Bodyguard model unavailable: " + modelName);
                    model.MarkAsNoLongerNeeded();
                    return;
                }
                Ped player = Game.Player.Character;
                if (!IsEntityUsable(player))
                {
                    model.MarkAsNoLongerNeeded();
                    Notify("Player character is not available right now.");
                    return;
                }
                Vector3 forward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, player.Handle);
                Vector3 position = player.Position + forward * (2.0f + _bodyguards.Count);
                int guard = Function.Call<int>((Hash)0xD49F9B0955C367DEUL, 4, (uint)model.Hash, position.X, position.Y, position.Z, player.Heading, true, false);
                model.MarkAsNoLongerNeeded();
                if (guard == 0) { Notify("Could not create bodyguard."); return; }
                Function.Call((Hash)0xAD738C3085FE7E11UL, guard, true, true); // SET_ENTITY_AS_MISSION_ENTITY
                Function.Call(Hash.SET_ENTITY_INVINCIBLE, guard, false);
                Function.Call((Hash)0xEBD76F2359F190ACUL, guard, true); // SET_PED_SUFFERS_CRITICAL_HITS
                Function.Call((Hash)0x63F58F7C80513AADUL, guard, true); // SET_PED_CAN_BE_TARGETTED
                Function.Call((Hash)0x1913FE4CBF41C463UL, guard, 72, true); // ClearRadarBlipOnDeath: GTA removes the ped blip on death.
                EnsureBodyguardGroup(player);
                EnsureBodyguardRelationshipGroup(player);
                if (_bodyguardRelationshipGroupHash != 0)
                    Function.Call((Hash)0xC80A74AC829DDD92UL, guard, _bodyguardRelationshipGroupHash); // SET_PED_RELATIONSHIP_GROUP_HASH
                Function.Call((Hash)0x9F3480FE65DB31B5UL, guard, _bodyguardGroupId); // SET_PED_AS_GROUP_MEMBER
                Function.Call((Hash)0x3DBFC55D5C9BB447UL, guard, true); // SET_PED_NEVER_LEAVES_GROUP
                Function.Call((Hash)0x2E2F4240B3F24647UL, guard, true); // SET_PED_CAN_TELEPORT_TO_GROUP_LEADER
                Function.Call((Hash)0xC7622C0D36B2FDA8UL, guard, 2);
                Function.Call((Hash)0x9F7794730795E019UL, guard, 46, true);
                Function.Call((Hash)0x304AE42E357B8C7EUL, guard, player.Handle,
                    -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true); // TASK_FOLLOW_TO_OFFSET_OF_ENTITY
                Function.Call((Hash)0x971D38760FBC02EFUL, guard, true); // SET_PED_KEEP_TASK
                Function.Call(Hash.SET_PED_ACCURACY, guard, 70);
                Function.Call((Hash)0xB128377056A54E2AUL, guard, true); // SET_PED_CAN_RAGDOLL
                ApplyBodyguardVitals(guard, true);
                GiveSelectedWeaponToBodyguard(guard);
                ApplyBodyguardCombatTactics(guard);
                _bodyguards.Add(guard);
                try
                {
                    int guardBlip = Function.Call<int>((Hash)0x5CDE92C702A8FCE7UL, guard); // ADD_BLIP_FOR_ENTITY
                    if (guardBlip != 0)
                    {
                        Function.Call((Hash)0x03D7FB09E75D6B7EUL, guardBlip, 2); // SET_BLIP_COLOUR: green
                        _bodyguardBlips[guard] = guardBlip;
                    }
                }
                catch (Exception blipException) { LogException("Create bodyguard map marker " + guard, blipException); }
                // Re-dispatch the current aim target so newly spawned guards join an
                // engagement already in progress instead of waiting for target change.
                _lastBodyguardAimTargetHandle = 0;
                _lastBodyguardAimTick = 0;
                Notify("Bodyguard spawned. Total: " + _bodyguards.Count);
            }
            catch (Exception ex)
            {
                LogException("SpawnBodyguard", ex);
                Notify("Bodyguard spawn failed. See log.");
            }
        }

        private void EnsureBodyguardGroup(Ped player)
        {
            if (!IsEntityUsable(player)) return;
            if (_bodyguardGroupId < 0)
            {
                _bodyguardGroupId = Function.Call<int>((Hash)0x90370EBE0FEE1A3DUL, 0); // CREATE_GROUP
                _bodyguardGroupHasDeadMember = false;
            }
            if (_bodyguardGroupId >= 0)
                Function.Call((Hash)0x2A7819605465FBCEUL, player.Handle, _bodyguardGroupId); // SET_PED_AS_GROUP_LEADER
        }

        private void EnsureBodyguardRelationshipGroup(Ped player)
        {
            if (!IsEntityUsable(player)) return;
            try
            {
                if (_bodyguardRelationshipGroupHash == 0)
                {
                    OutputArgument groupOutput = new OutputArgument();
                    try
                    {
                        Function.Call((Hash)0xF372BC22FCB88606UL, "AVERNES_BODYGUARDS", groupOutput);
                        _bodyguardRelationshipGroupHash = groupOutput.GetResult<uint>();
                    }
                    finally { groupOutput.Dispose(); }
                }
                if (_bodyguardRelationshipGroupHash == 0) return;
                uint playerGroup = Function.Call<uint>((Hash)0x7DBDD04862D95F04UL, player.Handle);
                Function.Call((Hash)0xBF25EB89375A37ADUL, 3, _bodyguardRelationshipGroupHash, playerGroup);
                Function.Call((Hash)0xBF25EB89375A37ADUL, 3, playerGroup, _bodyguardRelationshipGroupHash);
            }
            catch (Exception ex) { LogException("EnsureBodyguardRelationshipGroup", ex); }
        }

        private string GetSelectedBodyguardWeaponLabel()
        {
            if (BodyguardWeaponLabels.Length == 0) return "Unavailable";
            if (_bodyguardWeaponIndex < 0 || _bodyguardWeaponIndex >= BodyguardWeaponLabels.Length)
                _bodyguardWeaponIndex = 0;
            return BodyguardWeaponLabels[_bodyguardWeaponIndex];
        }

        private void CycleBodyguardWeapon()
        {
            if (BodyguardWeaponModels.Length == 0) return;
            _bodyguardWeaponIndex = (_bodyguardWeaponIndex + 1) % BodyguardWeaponModels.Length;
            Notify("Bodyguard weapon: " + GetSelectedBodyguardWeaponLabel());
        }

        private void GiveSelectedWeaponToBodyguard(int handle)
        {
            GiveBodyguardWeapon(handle, _bodyguardWeaponIndex);
        }

        private void GiveBodyguardWeapon(int handle, int weaponIndex)
        {
            if (!IsLivingBodyguardHandle(handle) || BodyguardWeaponModels.Length == 0) return;
            weaponIndex = ClampInt(weaponIndex, 0, BodyguardWeaponModels.Length - 1);
            uint weapon = Function.Call<uint>(Hash.GET_HASH_KEY, BodyguardWeaponModels[weaponIndex]);
            if (weapon == 0) throw new InvalidOperationException("Selected bodyguard weapon hash is invalid.");
            Function.Call(Hash.GIVE_WEAPON_TO_PED, handle, weapon, AmmoPerWeapon, false, true);
            Function.Call(Hash.SET_CURRENT_PED_WEAPON, handle, weapon, true);
        }

        private void GiveSelectedWeaponToBodyguards()
        {
            if (_bodyguards.Count == 0)
            {
                Notify("No active bodyguards.");
                return;
            }
            int applied = 0;
            for (int i = _bodyguards.Count - 1; i >= 0; i--)
            {
                int handle = _bodyguards[i];
                try
                {
                    if (!IsLivingBodyguardHandle(handle))
                    {
                        _bodyguards.RemoveAt(i);
                        QueueBodyguardDismissal(handle);
                        continue;
                    }
                    GiveSelectedWeaponToBodyguard(handle);
                    applied++;
                }
                catch (Exception ex) { LogException("Give bodyguard weapon " + handle, ex); }
            }
            Notify("Weapon " + GetSelectedBodyguardWeaponLabel() + " given to " + applied + " bodyguards.");
        }

        private int GetPlayerAimedNpcHandle()
        {
            try
            {
                OutputArgument targetOutput = new OutputArgument();
                bool hasTarget = false;
                int target = 0;
                try
                {
                    hasTarget = Function.Call<bool>((Hash)0x2975C866E6713290UL, Game.Player.Handle, targetOutput);
                    if (hasTarget) target = targetOutput.GetResult<int>();
                }
                finally { targetOutput.Dispose(); }

                if (!hasTarget || target == 0 || !IsPedHandleUsable(target)) return 0;
                if (Function.Call<bool>((Hash)0x12534C348C6CB68BUL, target)) return 0; // IS_PED_A_PLAYER
                if (Function.Call<bool>((Hash)0x3317DEDB88C95038UL, target, true)) return 0; // IS_PED_DEAD_OR_DYING
                if (_bodyguards.Contains(target)) return 0;
                return target;
            }
            catch (Exception ex)
            {
                LogException("GetPlayerAimedNpcHandle", ex);
                return 0;
            }
        }

        private void ProcessBodyguardAim(Ped player, int now)
        {
            if (unchecked(now - _lastBodyguardAimTick) < 250) return;
            _lastBodyguardAimTick = now;
            int target = GetPlayerAimedNpcHandle();
            Ped playerPed = Game.Player.Character;
            bool playerInVehicle = IsEntityUsable(playerPed) && playerPed.IsInVehicle();
            bool useMicroSmg = playerInVehicle && target != 0;
            if (_bodyguardUseMicroSmg != useMicroSmg)
            {
                _bodyguardUseMicroSmg = useMicroSmg;
                int weaponIndex = _bodyguardUseMicroSmg ? 3 : _bodyguardWeaponIndex;
                for (int i = 0; i < _bodyguards.Count; i++)
                {
                    int guard = _bodyguards[i];
                    try { if (IsLivingBodyguardHandle(guard)) GiveBodyguardWeapon(guard, weaponIndex); }
                    catch (Exception ex) { LogException("Switch bodyguard vehicle weapon " + guard, ex); }
                }
            }
            if (target == _lastBodyguardAimTargetHandle) return;
            _lastBodyguardAimTargetHandle = target;
            if (target == 0) return;

            for (int i = 0; i < _bodyguards.Count; i++)
            {
                int guard = _bodyguards[i];
                try
                {
                    if (!IsLivingBodyguardHandle(guard) || _bodyguardBoardingVehicleHandles.ContainsKey(guard)) continue;
                    Function.Call((Hash)0xF166E48407BAC484UL, guard, target, 0, 16); // TASK_COMBAT_PED
                    Function.Call((Hash)0x971D38760FBC02EFUL, guard, true); // SET_PED_KEEP_TASK
                }
                catch (Exception ex) { LogException("Bodyguard aim attack " + guard, ex); }
            }
            Log("Bodyguards assigned aimed NPC target " + target + ".");
        }

        private void RemoveBodyguardBlip(int handle)
        {
            int bodyguardBlip;
            if (!_bodyguardBlips.TryGetValue(handle, out bodyguardBlip)) return;
            try
            {
                if (bodyguardBlip != 0 && Function.Call<bool>((Hash)0xA6DB27D19ECBB7DAUL, bodyguardBlip))
                    Function.Call((Hash)0x86A652570E5F25DDUL, bodyguardBlip); // REMOVE_BLIP
            }
            catch (Exception ex) { LogException("Remove bodyguard map marker " + handle, ex); }
            _bodyguardBlips.Remove(handle);
        }

        private void ChangeBodyguardHealth(int delta)
        {
            _settings.BodyguardHealth = ClampInt(_settings.BodyguardHealth + delta, 100, 1000);
            ApplyBodyguardVitalsToAll();
            SaveSettings();
            Notify("Bodyguard HP: " + _settings.BodyguardHealth);
        }

        private void ChangeBodyguardArmour(int delta)
        {
            _settings.BodyguardArmour = ClampInt(_settings.BodyguardArmour + delta, 0, 100);
            ApplyBodyguardVitalsToAll();
            SaveSettings();
            Notify("Bodyguard armour: " + _settings.BodyguardArmour);
        }

        private void ApplyBodyguardVitals(int handle, bool resetHealth)
        {
            if (!IsLivingBodyguardHandle(handle)) return;
            int maxHealth = ClampInt(_settings.BodyguardHealth, 100, 1000);
            Function.Call(Hash.SET_ENTITY_INVINCIBLE, handle, _settings.BodyguardInvincible);
            Function.Call((Hash)0xFAEE099C6F890BB8UL, handle,
                false, false, false, false, false, false, false, false); // SET_ENTITY_PROOFS: no damage immunity flags
            Function.Call((Hash)0x1760FFA8AB074D66UL, handle, true); // SET_ENTITY_CAN_BE_DAMAGED
            Function.Call((Hash)0x166E7CF68597D8B5UL, handle, maxHealth); // SET_ENTITY_MAX_HEALTH
            if (resetHealth) Function.Call(Hash.SET_ENTITY_HEALTH, handle, maxHealth);
            Function.Call(Hash.SET_PED_ARMOUR, handle, ClampInt(_settings.BodyguardArmour, 0, 100));
            Function.Call((Hash)0xEBD76F2359F190ACUL, handle, true); // SET_PED_SUFFERS_CRITICAL_HITS
            Function.Call((Hash)0x63F58F7C80513AADUL, handle, true); // SET_PED_CAN_BE_TARGETTED
            Function.Call((Hash)0x66B57B72E0836A76UL, handle, Game.Player.Handle, true); // SET_PED_CAN_BE_TARGETTED_BY_PLAYER
            Function.Call((Hash)0x061CB768363D6424UL, handle, true); // SET_ALLOW_LOCKON_TO_PED_IF_FRIENDLY
            Function.Call((Hash)0x1913FE4CBF41C463UL, handle, 6, false); // SET_PED_CONFIG_FLAG: HasBulletProofVest=false
            Function.Call((Hash)0x1913FE4CBF41C463UL, handle, 2, false); // SET_PED_CONFIG_FLAG: NoCriticalHits=false
        }

        private void ApplyBodyguardVitalsToAll()
        {
            for (int i = 0; i < _bodyguards.Count; i++)
            {
                int handle = _bodyguards[i];
                try { ApplyBodyguardVitals(handle, true); }
                catch (Exception ex) { LogException("Apply bodyguard vitals " + handle, ex); }
            }
        }

        private void EnsureBodyguardWeaponAndVulnerability(int handle)
        {
            if (!IsLivingBodyguardHandle(handle)) return;
            ApplyBodyguardCombatTactics(handle);
            Function.Call(Hash.SET_ENTITY_INVINCIBLE, handle, _settings.BodyguardInvincible);
            Function.Call((Hash)0xFAEE099C6F890BB8UL, handle,
                false, false, false, false, false, false, false, false); // SET_ENTITY_PROOFS: bullet/fire/explosion/etc all false
            Function.Call((Hash)0x1760FFA8AB074D66UL, handle, true); // SET_ENTITY_CAN_BE_DAMAGED
            Function.Call((Hash)0x166E7CF68597D8B5UL, handle, ClampInt(_settings.BodyguardHealth, 100, 1000)); // SET_ENTITY_MAX_HEALTH
            Function.Call((Hash)0xEBD76F2359F190ACUL, handle, true); // SET_PED_SUFFERS_CRITICAL_HITS
            Function.Call((Hash)0x63F58F7C80513AADUL, handle, true); // SET_PED_CAN_BE_TARGETTED
            Function.Call((Hash)0x66B57B72E0836A76UL, handle, Game.Player.Handle, true); // SET_PED_CAN_BE_TARGETTED_BY_PLAYER
            Function.Call((Hash)0x061CB768363D6424UL, handle, true); // SET_ALLOW_LOCKON_TO_PED_IF_FRIENDLY
            Function.Call((Hash)0x1913FE4CBF41C463UL, handle, 6, false); // SET_PED_CONFIG_FLAG: HasBulletProofVest=false
            Function.Call((Hash)0x1913FE4CBF41C463UL, handle, 2, false); // SET_PED_CONFIG_FLAG: NoCriticalHits=false
            Function.Call(Hash.SET_PED_ARMOUR, handle, ClampInt(_settings.BodyguardArmour, 0, 100));
            // The custom companion group stays neutral to the player in both directions.
            if (_bodyguardRelationshipGroupHash != 0)
            {
                Ped player = Game.Player.Character;
                if (IsEntityUsable(player))
                {
                    uint playerGroup = Function.Call<uint>((Hash)0x7DBDD04862D95F04UL, player.Handle);
                    if (playerGroup != 0)
                    {
                        Function.Call((Hash)0xBF25EB89375A37ADUL, 3, _bodyguardRelationshipGroupHash, playerGroup);
                        Function.Call((Hash)0xBF25EB89375A37ADUL, 3, playerGroup, _bodyguardRelationshipGroupHash);
                    }
                }
            }

            if (_bodyguardWeaponIndex < 0 || _bodyguardWeaponIndex >= BodyguardWeaponModels.Length)
                _bodyguardWeaponIndex = 0;
            int desiredWeaponIndex = _bodyguardUseMicroSmg ? 3 : _bodyguardWeaponIndex;
            uint desiredWeapon = GetHash(BodyguardWeaponModels[desiredWeaponIndex]);
            uint currentWeapon = Function.Call<uint>(Hash.GET_SELECTED_PED_WEAPON, handle);
            int ammo = currentWeapon == desiredWeapon
                ? Function.Call<int>((Hash)0x015A522136D7F951UL, handle, desiredWeapon) : 0;
            if (currentWeapon != desiredWeapon || ammo <= 0)
                GiveBodyguardWeapon(handle, desiredWeaponIndex);
        }

        private void ApplyBodyguardCombatTactics(int handle)
        {
            if (!IsLivingBodyguardHandle(handle)) return;

            // Keep the companion in an offensive combat posture instead of repeatedly
            // diving away or cowering while the player is engaging nearby NPCs.
            Function.Call((Hash)0x6B7A646C242A7059UL, handle, false); // SET_PED_CAN_EVASIVE_DIVE
            Function.Call((Hash)0xCB7553CDCEF4A735UL, handle, false); // SET_PED_CAN_COWER_IN_COVER
            Function.Call((Hash)0x4D9CA1009AFBD057UL, handle, 2); // SET_PED_COMBAT_MOVEMENT: offensive
            Function.Call((Hash)0x3C606747B23E497BUL, handle, 2); // SET_PED_COMBAT_RANGE: far
            Function.Call((Hash)0xC7622C0D36B2FDA8UL, handle, 2); // SET_PED_COMBAT_ABILITY: professional
            Function.Call((Hash)0x9F7794730795E019UL, handle, 5, true); // Always fight
            Function.Call((Hash)0x9F7794730795E019UL, handle, 12, true); // Blind fire while in cover
            Function.Call((Hash)0x9F7794730795E019UL, handle, 13, true); // Aggressive
            Function.Call((Hash)0x9F7794730795E019UL, handle, 21, true); // Chase target on foot
            Function.Call((Hash)0x9F7794730795E019UL, handle, 43, true); // Advance when unable to find cover
            Function.Call((Hash)0x9F7794730795E019UL, handle, 58, true); // Disable fleeing from combat
        }

        private bool IsLivingBodyguardHandle(int handle)
        {
            // Do not wrap tracked bodyguard handles in Ped.Exists here. GTA may still
            // retain a dead ped handle during its deferred corpse cleanup; direct
            // existence/type/death natives avoid Entity.Exists on that invalid wrapper.
            if (!DoesEntityHandleExist(handle)) return false;
            try
            {
                if (Function.Call<int>((Hash)0x8ACD366038D14505UL, handle) != 1) return false; // GET_ENTITY_TYPE == PED
                return !Function.Call<bool>((Hash)0x3317DEDB88C95038UL, handle, true); // IS_PED_DEAD_OR_DYING
            }
            catch (Exception ex)
            {
                LogStaleEntityOnce(handle, "Direct bodyguard liveness check failed: " + ex.GetType().Name);
                return false;
            }
        }

        private void ProcessBodyguardFollowing(Ped player)
        {
            if (_bodyguards.Count == 0 || !IsEntityUsable(player)) return;
            int now = Environment.TickCount;
            ProcessBodyguardAim(player, now);
            if (unchecked(now - _lastBodyguardFollowTick) < 1250) return;
            _lastBodyguardFollowTick = now;
            EnsureBodyguardRelationshipGroup(player);

            bool playerInVehicle = false;
            int playerVehicleHandle = 0;
            try
            {
                playerInVehicle = player.IsInVehicle() && IsEntityUsable(player.CurrentVehicle);
                if (playerInVehicle) playerVehicleHandle = player.CurrentVehicle.Handle;
            }
            catch (Exception ex) { LogException("Read bodyguard player vehicle", ex); }

            if (playerInVehicle) _bodyguardPlayerVehicleHandle = playerVehicleHandle;

            for (int i = _bodyguards.Count - 1; i >= 0; i--)
            {
                int handle = _bodyguards[i];
                try
                {
                    if (!IsLivingBodyguardHandle(handle))
                    {
                        // Direct native check only; never call Ped.Exists on a corpse.
                        _bodyguards.RemoveAt(i);
                        RetireDeadBodyguardHandle(handle, "ped is dead or no longer exists");
                        continue;
                    }

                    // Reassert the selected weapon and ordinary damage state periodically.
                    // This counters ambient AI weapon switching and model-level protection flags.
                    EnsureBodyguardWeaponAndVulnerability(handle);

                    // A companion hit by the player must not retaliate. Keep bullet
                    // damage enabled; only cancel combat when this exact player is
                    // the target, then restore the normal follow task.
                    if (Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, handle, player.Handle))
                    {
                        Function.Call(Hash.CLEAR_PED_TASKS, handle);
                        Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                            -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true); // TASK_FOLLOW_TO_OFFSET_OF_ENTITY
                        Function.Call((Hash)0x971D38760FBC02EFUL, handle, true); // SET_PED_KEEP_TASK
                        continue;
                    }

                    bool inCombat = Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, handle, 0);
                    if (inCombat) continue;

                    bool guardInVehicle = Function.Call<bool>((Hash)0x997ABD671D25CA0BUL, handle, false);
                    int guardVehicleHandle = guardInVehicle
                        ? Function.Call<int>((Hash)0x9A9112A0FE9A4713UL, handle, false) : 0;

                    if (playerInVehicle)
                    {
                        if (guardVehicleHandle == playerVehicleHandle)
                        {
                            _bodyguardBoardingVehicleHandles.Remove(handle);
                            _bodyguardBoardingSeats.Remove(handle);
                            _bodyguardBoardingDeadlinesUtc.Remove(handle);
                            _bodyguardBoardingEnterAtUtc.Remove(handle);
                            continue;
                        }
                        if (guardInVehicle)
                        {
                            int previouslyRequestedVehicle;
                            if (_bodyguardBoardingVehicleHandles.TryGetValue(handle, out previouslyRequestedVehicle) &&
                                previouslyRequestedVehicle != playerVehicleHandle && guardVehicleHandle == previouslyRequestedVehicle)
                            {
                                // The player switched vehicles without leaving the old one;
                                // let this guard leave the previously assigned vehicle and
                                // board the new player vehicle on a subsequent tick.
                                Function.Call((Hash)0xD3DBCE61A490BE02UL, handle, guardVehicleHandle, 16); // TASK_LEAVE_VEHICLE
                                Function.Call((Hash)0x971D38760FBC02EFUL, handle, true); // SET_PED_KEEP_TASK
                                _bodyguardBoardingVehicleHandles.Remove(handle);
                                _bodyguardBoardingSeats.Remove(handle);
                                _bodyguardBoardingDeadlinesUtc.Remove(handle);
                                _bodyguardBoardingEnterAtUtc.Remove(handle);
                            }
                            continue; // Never hijack an unrelated ambient vehicle.
                        }

                        Vector3 guardPosition = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, handle, true);
                        float distance = Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                            guardPosition.X, guardPosition.Y, guardPosition.Z,
                            player.Position.X, player.Position.Y, player.Position.Z, true);
                        if (distance > 28.0f)
                        {
                            Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                                -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true);
                            continue;
                        }

                        int requestedVehicle;
                        DateTime retryDeadline;
                        DateTime enterAt;
                        if (_bodyguardBoardingVehicleHandles.TryGetValue(handle, out requestedVehicle) &&
                            requestedVehicle == playerVehicleHandle &&
                            _bodyguardBoardingEnterAtUtc.TryGetValue(handle, out enterAt))
                        {
                            if (DateTime.UtcNow < enterAt) continue;
                            int stagedSeat;
                            if (_bodyguardBoardingSeats.TryGetValue(handle, out stagedSeat))
                            {
                                // The door has been opened by a dedicated task. Start the normal
                                // entry task only after that animation had time to complete.
                                Function.Call((Hash)0xC20E50AA46D09CA8UL, handle, playerVehicleHandle,
                                    10000, stagedSeat, 1.0f, 1, 0); // TASK_ENTER_VEHICLE: normal animated entry.
                                Function.Call((Hash)0x971D38760FBC02EFUL, handle, true); // SET_PED_KEEP_TASK
                                _bodyguardBoardingEnterAtUtc.Remove(handle);
                                _bodyguardBoardingDeadlinesUtc[handle] = DateTime.UtcNow.AddSeconds(12);
                            }
                            continue;
                        }
                        if (_bodyguardBoardingVehicleHandles.TryGetValue(handle, out requestedVehicle) &&
                            requestedVehicle == playerVehicleHandle &&
                            _bodyguardBoardingDeadlinesUtc.TryGetValue(handle, out retryDeadline) &&
                            DateTime.UtcNow < retryDeadline)
                            continue;

                        _bodyguardBoardingVehicleHandles.Remove(handle);
                        _bodyguardBoardingSeats.Remove(handle);
                        _bodyguardBoardingDeadlinesUtc.Remove(handle);
                        _bodyguardBoardingEnterAtUtc.Remove(handle);
                        int passengerCount = Function.Call<int>((Hash)0xA7C4F2C6E744A550UL, playerVehicleHandle);
                        if (passengerCount < 0 || passengerCount > 16) passengerCount = 0;
                        int freeSeat = -1;
                        for (int seat = 0; seat < passengerCount; seat++)
                        {
                            bool reserved = false;
                            foreach (KeyValuePair<int, int> reservation in _bodyguardBoardingSeats)
                            {
                                if (reservation.Key != handle && reservation.Value == seat &&
                                    _bodyguardBoardingVehicleHandles.ContainsKey(reservation.Key) &&
                                    _bodyguardBoardingVehicleHandles[reservation.Key] == playerVehicleHandle)
                                { reserved = true; break; }
                            }
                            if (!reserved && Function.Call<bool>((Hash)0x22AC59A870E6A669UL, playerVehicleHandle, seat, false))
                            {
                                freeSeat = seat;
                                break;
                            }
                        }
                        if (freeSeat >= 0)
                        {
                            // Open the selected door as a separate ped task, then start the
                            // ordinary entry task on a later tick. Flag 3 teleports the ped
                            // to the vehicle in GTA; flag 1 is the normal animated entry.
                            Function.Call((Hash)0x965791A9A488A062UL, handle, playerVehicleHandle,
                                10000, freeSeat, 1.0f); // TASK_OPEN_VEHICLE_DOOR
                            Function.Call((Hash)0x971D38760FBC02EFUL, handle, true); // SET_PED_KEEP_TASK
                            _bodyguardBoardingVehicleHandles[handle] = playerVehicleHandle;
                            _bodyguardBoardingSeats[handle] = freeSeat;
                            _bodyguardBoardingEnterAtUtc[handle] = DateTime.UtcNow.AddMilliseconds(500);
                            _bodyguardBoardingDeadlinesUtc[handle] = DateTime.UtcNow.AddSeconds(15);
                        }
                        else
                        {
                            Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                                -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true);
                        }
                        continue;
                    }

                    if (guardInVehicle && _bodyguardPlayerVehicleHandle != 0 && guardVehicleHandle == _bodyguardPlayerVehicleHandle)
                    {
                        Function.Call((Hash)0xD3DBCE61A490BE02UL, handle, guardVehicleHandle, 16); // TASK_LEAVE_VEHICLE
                        Function.Call((Hash)0x971D38760FBC02EFUL, handle, true);
                        continue;
                    }
                    _bodyguardBoardingVehicleHandles.Remove(handle);
                    _bodyguardBoardingSeats.Remove(handle);
                    _bodyguardBoardingDeadlinesUtc.Remove(handle);
                    _bodyguardBoardingEnterAtUtc.Remove(handle);
                    if (guardInVehicle) continue;

                    bool playerInCombat = Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, player.Handle, 0);
                    if (playerInCombat)
                    {
                        Function.Call((Hash)0x7CF1C3B65E9C67B1UL, handle, 45.0f, 0); // TASK_COMBAT_HATED_TARGETS_AROUND_PED
                        continue;
                    }

                    Vector3 position = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, handle, true);
                    float playerDistance = Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                        position.X, position.Y, position.Z,
                        player.Position.X, player.Position.Y, player.Position.Z, true);
                    if (playerDistance > 5.0f)
                    {
                        Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                            -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true);
                    }
                }
                catch (Exception ex) { LogException("Bodyguard follow " + handle, ex); }
            }

            if (!playerInVehicle) _bodyguardPlayerVehicleHandle = 0;
        }

        private void DismissAllBodyguards()
        {
            int queued = RemoveTrackedBodyguardsSilently();
            if (queued > 0)
                Notify("Dismissal queued for " + queued + " bodyguards. Cleanup runs safely in the background.");
            else if (_pendingBodyguardDismissals.Count > 0)
                Notify("Bodyguard dismissal is already in progress.");
            else
                Notify("No active bodyguards.");
        }

        private void ClearBodyguardBoardingState(int handle)
        {
            _bodyguardBoardingVehicleHandles.Remove(handle);
            _bodyguardBoardingSeats.Remove(handle);
            _bodyguardBoardingDeadlinesUtc.Remove(handle);
            _bodyguardBoardingEnterAtUtc.Remove(handle);
        }

        private void RetireDeadBodyguardHandle(int handle, string reason)
        {
            // Config flag 72 asks GTA to clear the entity blip on death. Forget the
            // managed mapping instead of issuing REMOVE_BLIP while corpse cleanup runs.
            _bodyguardBlips.Remove(handle);
            ClearBodyguardBoardingState(handle);
            _queuedBodyguardDismissalHandles.Remove(handle);
            _bodyguardDismissalDetachedHandles.Remove(handle);
            _bodyguardGroupHasDeadMember = true;
            _bodyguardGroupCleanupPending = true;
            Log("Bodyguard removed from trainer tracking without further ped natives: handle=" + handle + "; reason=" + reason + ".");
        }

        private void QueueBodyguardDismissal(int handle)
        {
            if (handle == 0) return;
            if (_queuedBodyguardDismissalHandles.Add(handle))
                _pendingBodyguardDismissals.Enqueue(handle);
            ClearBodyguardBoardingState(handle);
            _bodyguardGroupCleanupPending = true;
        }

        private int RemoveTrackedBodyguardsSilently()
        {
            int queued = 0;
            for (int i = _bodyguards.Count - 1; i >= 0; i--)
            {
                int handle = _bodyguards[i];
                if (handle != 0)
                {
                    bool wasQueued = _queuedBodyguardDismissalHandles.Contains(handle);
                    QueueBodyguardDismissal(handle);
                    if (!wasQueued) queued++;
                }
            }

            _bodyguards.Clear();
            _bodyguardPlayerVehicleHandle = 0;
            _lastBodyguardAimTargetHandle = 0;
            _lastBodyguardAimTick = 0;
            if (_bodyguardGroupId >= 0) _bodyguardGroupCleanupPending = true;
            return queued;
        }

        private void ProcessPendingBodyguardDismissals()
        {
            if (_pendingBodyguardDismissals.Count > 0)
            {
                DateTime now = DateTime.UtcNow;
                if (now < _nextBodyguardDismissalUtc) return;

                int handle = _pendingBodyguardDismissals.Dequeue();
                try
                {
                    if (!IsLivingBodyguardHandle(handle))
                    {
                        // Dead/unavailable ped: GTA clears its blip through config flag 72.
                        _bodyguardBlips.Remove(handle);
                        // Stop all further entity calls and leave the corpse to GTA.
                        _bodyguardGroupHasDeadMember = true;
                        _bodyguardDismissalDetachedHandles.Remove(handle);
                        _queuedBodyguardDismissalHandles.Remove(handle);
                        _bodyguardGroupCleanupNotBeforeUtc = DateTime.UtcNow.AddSeconds(2);
                        _nextBodyguardDismissalUtc = DateTime.UtcNow.AddMilliseconds(650);
                        return;
                    }

                    RemoveBodyguardBlip(handle);
                    if (!_bodyguardDismissalDetachedHandles.Contains(handle))
                    {
                        // Phase 1: detach the living ped from GTA's group/task manager.
                        // Delete is intentionally deferred to a later game tick.
                        Function.Call(Hash.CLEAR_PED_TASKS, handle);
                        Function.Call((Hash)0x971D38760FBC02EFUL, handle, false); // SET_PED_KEEP_TASK
                        Function.Call((Hash)0x3DBFC55D5C9BB447UL, handle, false); // SET_PED_NEVER_LEAVES_GROUP
                        Function.Call((Hash)0xED74007FFB146BC2UL, handle); // REMOVE_PED_FROM_GROUP
                        _bodyguardDismissalDetachedHandles.Add(handle);
                        _pendingBodyguardDismissals.Enqueue(handle);
                        _nextBodyguardDismissalUtc = DateTime.UtcNow.AddMilliseconds(650);
                        return;
                    }

                    // Phase 2: after the quiet interval, release the detached ped to GTA.
                    // DELETE_ENTITY on a recently detached companion was associated with
                    // native game crashes during batch dismissal; mark it no longer needed.
                    if (IsLivingBodyguardHandle(handle))
                    {
                        Ped guard = new Ped(handle);
                        if (IsEntityUsable(guard)) guard.MarkAsNoLongerNeeded();
                    }
                    else
                    {
                        _bodyguardGroupHasDeadMember = true;
                    }
                    _bodyguardDismissalDetachedHandles.Remove(handle);
                    _queuedBodyguardDismissalHandles.Remove(handle);
                }
                catch (Exception ex)
                {
                    _bodyguardGroupHasDeadMember = true;
                    _bodyguardDismissalDetachedHandles.Remove(handle);
                    _queuedBodyguardDismissalHandles.Remove(handle);
                    LogException("Process bodyguard dismissal " + handle, ex);
                }

                _bodyguardGroupCleanupNotBeforeUtc = DateTime.UtcNow.AddSeconds(2);
                _nextBodyguardDismissalUtc = DateTime.UtcNow.AddMilliseconds(650);
                return;
            }

            if (!_bodyguardGroupCleanupPending || DateTime.UtcNow < _bodyguardGroupCleanupNotBeforeUtc) return;
            _bodyguardGroupCleanupPending = false;

            if (_bodyguardGroupId >= 0)
            {
                // Keep the empty group alive and reuse it on the next spawn. Removing a
                // group immediately after releasing mission peds is unsafe in some game
                // sessions and was the remaining crash path in bulk dismissal.
                _bodyguardGroupCleanupPending = false;
                Log("Bodyguard group retained empty for safe reuse; deadMember=" + _bodyguardGroupHasDeadMember + ".");
            }
        }

        private void ReleaseBodyguardsOnScriptAbort()
        {
            // Script shutdown stops Tick callbacks, so queued deletes would never run.
            // Release living peds only; GTA retains ownership of dead companions.
            HashSet<int> handles = new HashSet<int>(_bodyguards);
            foreach (int pendingHandle in _pendingBodyguardDismissals) handles.Add(pendingHandle);
            foreach (int handle in handles)
            {
                try
                {
                    if (IsLivingBodyguardHandle(handle))
                    {
                        RemoveBodyguardBlip(handle);
                        Function.Call(Hash.CLEAR_PED_TASKS, handle);
                        Function.Call((Hash)0x971D38760FBC02EFUL, handle, false);
                        Function.Call((Hash)0x3DBFC55D5C9BB447UL, handle, false);
                        Function.Call((Hash)0xED74007FFB146BC2UL, handle);
                    }
                    else
                    {
                        // The game removes this blip through config flag 72. Avoid a
                        // REMOVE_BLIP call while GTA is processing the dead ped.
                        _bodyguardBlips.Remove(handle);
                        _bodyguardGroupHasDeadMember = true;
                    }
                }
                catch (Exception ex) { LogException("Release bodyguard during script abort " + handle, ex); }
            }
            _bodyguards.Clear();
            _pendingBodyguardDismissals.Clear();
            _queuedBodyguardDismissalHandles.Clear();
            _bodyguardDismissalDetachedHandles.Clear();
            _bodyguardBoardingVehicleHandles.Clear();
            _bodyguardBoardingSeats.Clear();
            _bodyguardBoardingDeadlinesUtc.Clear();
            _bodyguardBoardingEnterAtUtc.Clear();
            _bodyguardBlips.Clear();
            _bodyguardPlayerVehicleHandle = 0;
            _lastBodyguardAimTargetHandle = 0;
            _bodyguardGroupCleanupPending = false;
            // Keep the now-empty group allocated. It belongs to the game session and
            // is reused safely if this script is reloaded before the player respawns.
        }

        private void ProcessAutomaticParachuteDeployment(Ped ped)
        {
            if (!_settings.AutoDeployParachute || !IsEntityUsable(ped))
            {
                _parachuteAutoDeploymentRequested = false;
                return;
            }

            try
            {
                bool freeFall = Function.Call<bool>((Hash)0x7DCE8BDA0F1C1200UL, ped.Handle); // IS_PED_IN_PARACHUTE_FREE_FALL
                if (!freeFall)
                {
                    _parachuteAutoDeploymentRequested = false;
                    return;
                }

                int parachuteState = Function.Call<int>((Hash)0x79CFD9827CC979B6UL, ped.Handle); // GET_PED_PARACHUTE_STATE
                if (parachuteState == 0 && !_parachuteAutoDeploymentRequested)
                {
                    Function.Call((Hash)0x16E42E800B472221UL, ped.Handle); // FORCE_PED_TO_OPEN_PARACHUTE
                    _parachuteAutoDeploymentRequested = true;
                    Log("Automatic parachute deployment requested during free fall.");
                }
                else if (parachuteState != 0)
                {
                    _parachuteAutoDeploymentRequested = true;
                }
            }
            catch (Exception ex)
            {
                LogException("Automatic parachute deployment", ex);
            }
        }

        private void StartPlayerScenario(string scenario)
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            Function.Call((Hash)0xAAA34F8A7CB32098UL, ped.Handle); // CLEAR_PED_TASKS_IMMEDIATELY
            Function.Call((Hash)0x142A02425FF02BD9UL, ped.Handle, scenario, 0, true);
        }

        private void StopPlayerAnimation()
        {
            Ped ped = Game.Player.Character;
            if (IsEntityUsable(ped))
                Function.Call((Hash)0xAAA34F8A7CB32098UL, ped.Handle); // CLEAR_PED_TASKS_IMMEDIATELY
        }

        private void BeginGroundTeleport(float x, float y, float zHint)
        {
            BeginGroundTeleportInternal(x, y, zHint, null);
        }

        private void BeginHistoryRollbackTeleport(TeleportPoint point)
        {
            if (point == null) return;
            BeginGroundTeleportInternal(point.X, point.Y, point.Z, point);
        }

        private void BeginGroundTeleportInternal(float x, float y, float zHint, TeleportPoint historyRollbackTarget)
        {
            if (_groundTeleportPending)
            {
                Notify("Another ground-loading teleport is already in progress.");
                return;
            }
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped))
            {
                Notify("Player is not ready; teleport was cancelled.");
                return;
            }
            int entityHandle = ped.Handle;
            if (ped.IsInVehicle() && IsEntityUsable(ped.CurrentVehicle))
                entityHandle = ped.CurrentVehicle.Handle;

            _pendingHistoryRollbackTarget = historyRollbackTarget;
            _pendingGroundTeleportHistoryOrigin = historyRollbackTarget == null ? CaptureTeleportOrigin() : null;
            _groundTeleportX = x;
            _groundTeleportY = y;
            _groundTeleportZHint = zHint;
            _groundTeleportEntityHandle = entityHandle;
            _groundTeleportDeadlineUtc = DateTime.UtcNow.AddSeconds(60);
            _groundTeleportPending = true;
            _groundLoadSceneStarted = false;

            try
            {
                // Request the remote scene without moving or freezing the player/entity.
                Function.Call((Hash)0xBB7454BAFF08FE25UL, x, y, zHint, 0.0f, 0.0f, 0.0f); // SET_FOCUS_POS_AND_VEL
                Function.Call((Hash)0x07503F7948F491A7UL, x, y, zHint); // REQUEST_COLLISION_AT_COORD
                Function.Call((Hash)0xC9156DC11411A9EAUL, x, y, zHint); // REQUEST_ADDITIONAL_COLLISION_AT_COORD
                _groundLoadSceneStarted = Function.Call<bool>((Hash)0xACCFB4ACF53551B0UL, x, y, zHint, 250.0f, 0); // NEW_LOAD_SCENE_START_SPHERE
                if (!_groundLoadSceneStarted)
                {
                    CancelPendingGroundTeleport();
                    Notify("GTA could not start destination streaming. No teleport was performed.");
                    LogFailure("Ground teleport", "NEW_LOAD_SCENE_START_SPHERE did not start; player position was preserved.");
                    return;
                }
                Notify("Loading destination terrain first; your current position is preserved.");
                Log("Ground teleport streaming started. X=" + x.ToString("0.0", CultureInfo.InvariantCulture) +
                    "; Y=" + y.ToString("0.0", CultureInfo.InvariantCulture) +
                    "; hintZ=" + zHint.ToString("0.0", CultureInfo.InvariantCulture) +
                    "; sceneStarted=" + _groundLoadSceneStarted + ".");
            }
            catch (Exception ex)
            {
                CancelPendingGroundTeleport();
                LogException("BeginGroundTeleport", ex);
                Notify("Ground streaming could not start; no teleport was performed.");
            }
        }

        private void ProcessPendingGroundTeleport(Ped ped)
        {
            if (!_groundTeleportPending) return;
            int handle = _groundTeleportEntityHandle;
            try
            {
                if (!IsEntityUsable(ped) || !DoesEntityHandleExist(handle))
                {
                    CancelPendingGroundTeleport();
                    Notify("Ground teleport cancelled: player or target entity is unavailable.");
                    return;
                }
                if (handle != ped.Handle &&
                    (!ped.IsInVehicle() || !IsEntityUsable(ped.CurrentVehicle) || ped.CurrentVehicle.Handle != handle))
                {
                    CancelPendingGroundTeleport();
                    Notify("Ground teleport cancelled because you left the original vehicle.");
                    return;
                }

                // Re-request collision while the streaming focus remains at the target.
                Function.Call((Hash)0xBB7454BAFF08FE25UL, _groundTeleportX, _groundTeleportY,
                    _groundTeleportZHint, 0.0f, 0.0f, 0.0f); // SET_FOCUS_POS_AND_VEL
                Function.Call((Hash)0x07503F7948F491A7UL, _groundTeleportX, _groundTeleportY, _groundTeleportZHint);
                Function.Call((Hash)0xC9156DC11411A9EAUL, _groundTeleportX, _groundTeleportY, _groundTeleportZHint);

                bool sceneReady = _groundLoadSceneStarted &&
                    Function.Call<bool>((Hash)0x01B8247A7A8B9AD1UL); // IS_NEW_LOAD_SCENE_LOADED
                float probeZ = Math.Max(1000.0f, _groundTeleportZHint + 300.0f);
                OutputArgument groundOutput = new OutputArgument();
                bool foundGround = false;
                float groundZ = 0.0f;
                try
                {
                    foundGround = Function.Call<bool>((Hash)0xC906A7DAB05C8D2BUL,
                        _groundTeleportX, _groundTeleportY, probeZ, groundOutput, false, false);
                    if (foundGround) groundZ = groundOutput.GetResult<float>();
                }
                finally { groundOutput.Dispose(); }

                TeleportPoint rollbackTarget = _pendingHistoryRollbackTarget;
                bool rollbackCoordinatesSafe = rollbackTarget != null &&
                    IsSafeWorldPosition(new Vector3(rollbackTarget.X, rollbackTarget.Y, rollbackTarget.Z)) &&
                    !Single.IsNaN(rollbackTarget.Heading) && !Single.IsInfinity(rollbackTarget.Heading);
                bool destinationReady = sceneReady && (rollbackTarget != null
                    ? rollbackCoordinatesSafe
                    : foundGround && !Single.IsNaN(groundZ) && !Single.IsInfinity(groundZ) && groundZ >= -100.0f && groundZ <= 1500.0f);
                if (destinationReady)
                {
                    // For waypoint teleports use confirmed ground. History rollback restores
                    // the exact saved height/heading after the same destination scene loads.
                    float destinationZ = rollbackTarget == null ? groundZ + 2.0f : rollbackTarget.Z;
                    Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, handle,
                        _groundTeleportX, _groundTeleportY, destinationZ, false, false, false);
                    Function.Call((Hash)0x1C99BB7B6E96D16FUL, handle, 0.0f, 0.0f, 0.0f);
                    if (rollbackTarget != null)
                        Function.Call((Hash)0x8E2530AA8ADA980EUL, handle, rollbackTarget.Heading); // SET_ENTITY_HEADING
                    else if (ped.IsInVehicle() && IsEntityUsable(ped.CurrentVehicle) && ped.CurrentVehicle.Handle == handle)
                    {
                        int vehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, handle);
                        if (vehicleClass != 15 && vehicleClass != 16)
                            Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, handle);
                    }
                    if (rollbackTarget != null) CompleteHistoryRollbackTeleport(rollbackTarget);
                    else RecordTeleportOrigin(_pendingGroundTeleportHistoryOrigin);
                    _pendingGroundTeleportHistoryOrigin = null;
                    CancelPendingGroundTeleport();
                    if (rollbackTarget == null) Notify("Teleported after destination terrain loaded.");
                    return;
                }

                if (DateTime.UtcNow >= _groundTeleportDeadlineUtc)
                {
                    LogFailure("Ground teleport", "Destination terrain/collision was not confirmed within 60 seconds; source entity was not moved.");
                    CancelPendingGroundTeleport();
                    Notify("Teleport cancelled: destination terrain did not finish loading. You remain at the original position.");
                }
            }
            catch (Exception ex)
            {
                CancelPendingGroundTeleport();
                LogException("ProcessPendingGroundTeleport", ex);
                Notify("Teleport cancelled safely; current position was not changed.");
            }
        }

        private void CancelPendingGroundTeleport()
        {
            bool hadStreamingFocus = _groundTeleportPending || _groundLoadSceneStarted;
            bool stopOwnedScene = _groundLoadSceneStarted;
            _groundTeleportPending = false;
            _groundLoadSceneStarted = false;
            _pendingGroundTeleportHistoryOrigin = null;
            _pendingHistoryRollbackTarget = null;
            if (!hadStreamingFocus) return;
            if (stopOwnedScene)
            {
                try
                {
                    if (Function.Call<bool>((Hash)0xA41A05B6CB741B85UL)) // IS_NEW_LOAD_SCENE_ACTIVE
                        Function.Call((Hash)0xC197616D221FF4A4UL); // NEW_LOAD_SCENE_STOP
                }
                catch (Exception ex) { LogException("Stop ground teleport load scene", ex); }
            }
            try { Function.Call((Hash)0x31B73D1EA9F01DA2UL); } // CLEAR_FOCUS
            catch (Exception ex) { LogException("Clear ground teleport streaming focus", ex); }
        }

        private void TeleportToGround(float x, float y, float z)
        {
            BeginGroundTeleport(x, y, z);
        }

        private void TeleportTo(float x, float y, float z)
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            Entity target = ped;
            if (ped.IsInVehicle() && IsEntityUsable(ped.CurrentVehicle)) target = ped.CurrentVehicle;
            if (!IsEntityUsable(target)) return;
            TeleportPoint origin = CaptureTeleportOrigin();
            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, target.Handle, x, y, z, false, false, false);
            Function.Call((Hash)0x1C99BB7B6E96D16FUL, target.Handle, 0.0f, 0.0f, 0.0f);
            RecordTeleportOrigin(origin);
            Notify("Teleported.");
        }

        private void SetWeather(string weather)
        {
            _settings.Weather = NormalizeWeather(weather);
            _settings.LockWeather = true;
            Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, _settings.Weather);
            SaveSettings();
            Notify("Weather: " + _settings.Weather);
        }

        private void SetClockTime(int hour, int minute)
        {
            Function.Call((Hash)0x47C3B5848C3E45D8UL, hour, minute, 0);
            Notify("Game time changed.");
        }

        private void ClearNearbyArea()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            Vector3 position = ped.Position;
            if (ped.IsInVehicle())
            {
                Notify("Exit your vehicle before clearing nearby traffic and pedestrians.");
                return;
            }

            // CLEAR_AREA_OF_VEHICLES requires all 11 parameters; the previous call
            // omitted trailing arguments and used an incorrect peds native hash.
            Function.Call((Hash)0x01C7B9B38428AEB6UL, position.X, position.Y, position.Z,
                35.0f, false, false, false, false, false, false, 0);
            Function.Call((Hash)0xBE31FD6CE464AC59UL, position.X, position.Y, position.Z, 25.0f, 0);
            Notify("Nearby traffic and pedestrians cleared.");
        }

        private bool IsLooseLitterModel(uint modelHash)
        {
            if (_looseLitterModelHashes == null)
            {
                string[] models = new string[] {
                    "prop_rub_binbag_01", "prop_rub_binbag_02", "prop_rub_binbag_03", "prop_rub_binbag_04", "prop_rub_binbag_05",
                    "prop_rub_litter_01", "prop_rub_litter_03", "prop_rub_litter_04", "prop_rub_pile_01", "prop_rub_pile_02",
                    "prop_rub_pile_03", "prop_rub_pile_04", "prop_rub_pile_05", "prop_rub_pile_06", "prop_rub_pile_07",
                    "prop_rub_cup_01", "prop_rub_bottle_01", "prop_rub_cardpile_01", "prop_rub_cardpile_02", "prop_rub_scrap_02"
                };
                _looseLitterModelHashes = new HashSet<uint>();
                for (int i = 0; i < models.Length; i++)
                    _looseLitterModelHashes.Add(Function.Call<uint>(Hash.GET_HASH_KEY, models[i]));
            }
            return _looseLitterModelHashes.Contains(modelHash);
        }

        private void ClearTrainerSpawnedWorldEntities()
        {
            Ped player = Game.Player.Character;
            if (!IsEntityUsable(player)) { Notify("Player is not available."); return; }
            int playerVehicle = 0;
            try { if (player.IsInVehicle() && IsEntityUsable(player.CurrentVehicle)) playerVehicle = player.CurrentVehicle.Handle; } catch { }
            int removedVehicles = 0;
            int skippedVehicles = 0;
            List<int> vehicleHandles = _trainerSpawnedVehicleHashes.Keys.ToList();
            for (int i = 0; i < vehicleHandles.Count; i++)
            {
                int handle = vehicleHandles[i];
                try
                {
                    if (!IsVehicleHandleUsable(handle))
                    {
                        _trainerSpawnedVehicleHashes.Remove(handle);
                        _trainerSpawnedVehicleCreatedUtc.Remove(handle);
                        _trainerVehicleDeleteRetryUtc.Remove(handle);
                        continue;
                    }
                    if (handle == playerVehicle || IsVehicleOccupied(handle)) { skippedVehicles++; continue; }
                    if (TryDeleteTrackedVehicle(handle, "manual cleanup"))
                    {
                        _trainerSpawnedVehicleHashes.Remove(handle);
                        _trainerSpawnedVehicleCreatedUtc.Remove(handle);
                        _trainerVehicleDeleteRetryUtc.Remove(handle);
                        removedVehicles++;
                    }
                    else
                    {
                        skippedVehicles++;
                    }
                }
                catch (Exception ex) { skippedVehicles++; LogException("Clear trainer vehicle " + handle, ex); }
            }

            int queuedBodyguards = RemoveTrackedBodyguardsSilently();
            int removedObjects = 0;
            for (int i = _trainerSpawnedObjectHandles.Count - 1; i >= 0; i--)
            {
                int handle = _trainerSpawnedObjectHandles[i];
                try
                {
                    if (IsPropHandleUsable(handle))
                    {
                        if (Function.Call<bool>((Hash)0x0A7B270912999B3CUL, handle))
                        {
                            // Trainer-owned object handles are trusted, but keep mission objects protected.
                            _trainerSpawnedObjectHandles.RemoveAt(i);
                            continue;
                        }
                        Prop trackedProp = new Prop(handle);
                        if (IsEntityUsable(trackedProp))
                        {
                            Function.Call((Hash)0xAD738C3085FE7E11UL, handle, true, true);
                            trackedProp.Delete();
                            removedObjects++;
                        }
                    }
                    _trainerSpawnedObjectHandles.RemoveAt(i);
                }
                catch (Exception ex) { LogException("Clear trainer object " + handle, ex); }
            }

            int removedLitter = 0;
            try
            {
                Prop[] props = World.GetAllProps();
                if (props != null)
                {
                    for (int i = 0; i < props.Length; i++)
                    {
                        Prop prop = props[i];
                        if (!IsEntityUsable(prop)) continue;
                        try
                        {
                            if (Function.Call<bool>((Hash)0x0A7B270912999B3CUL, prop.Handle)) continue; // Keep mission entities.
                            if (Function.Call<bool>((Hash)0xB346476EF1A64897UL, prop.Handle)) continue; // Keep attached props.
                            uint modelHash = Function.Call<uint>(Hash.GET_ENTITY_MODEL, prop.Handle);
                            if (!IsLooseLitterModel(modelHash)) continue;
                            Vector3 position = prop.Position;
                            float distance = Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                                position.X, position.Y, position.Z, player.Position.X, player.Position.Y, player.Position.Z, true);
                            if (distance > 45.0f) continue;
                            prop.Delete();
                            removedLitter++;
                        }
                        catch (Exception ex) { LogException("Clear loose litter prop", ex); }
                    }
                }
            }
            catch (Exception ex) { LogException("Enumerate loose litter", ex); }

            Log("World cleanup completed. trainerVehicles=" + removedVehicles + "; bodyguardsQueued=" + queuedBodyguards +
                "; trainerObjects=" + removedObjects + "; litter=" + removedLitter + "; occupiedVehiclesSkipped=" + skippedVehicles + ".");
            Notify("Cleanup: " + removedVehicles + " vehicles, " + queuedBodyguards + " bodyguards queued for safe removal, " + removedObjects +
                " objects, " + removedLitter + " litter items. Occupied vehicles skipped: " + skippedVehicles + ".");
        }

        private void HealPlayer()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            int maxHealth = Function.Call<int>(Hash.GET_ENTITY_MAX_HEALTH, ped.Handle);
            Function.Call(Hash.SET_ENTITY_HEALTH, ped.Handle, maxHealth > 0 ? maxHealth : 200);
            Function.Call(Hash.SET_PED_ARMOUR, ped.Handle, 100);
            Function.Call(Hash.CLEAR_PED_BLOOD_DAMAGE, ped.Handle);
            Notify("Health and armour restored.");
        }

        private void ClearWantedLevel()
        {
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player, 0, false);
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
            Notify("Wanted level cleared.");
        }

        private void TeleportToWaypoint()
        {
            try
            {
                const int waypointBlipSprite = 8;
                int blip = Function.Call<int>((Hash)0x1BEDE233E6CD2A1FUL, waypointBlipSprite); // GET_FIRST_BLIP_INFO_ID
                if (blip == 0 || !Function.Call<bool>((Hash)0xA6DB27D19ECBB7DAUL, blip)) // DOES_BLIP_EXIST
                {
                    Notify("Place a waypoint on the map first.");
                    return;
                }

                Vector3 destination = Function.Call<Vector3>((Hash)0xFA7C7F0AADF25D09UL, blip); // GET_BLIP_INFO_ID_COORD
                if (Single.IsNaN(destination.X) || Single.IsInfinity(destination.X) ||
                    Single.IsNaN(destination.Y) || Single.IsInfinity(destination.Y))
                {
                    Notify("Waypoint coordinates are invalid; teleport cancelled.");
                    return;
                }

                Log("Waypoint teleport requested. X=" + destination.X.ToString("0.0", CultureInfo.InvariantCulture) +
                    "; Y=" + destination.Y.ToString("0.0", CultureInfo.InvariantCulture) + ".");
                BeginGroundTeleport(destination.X, destination.Y, destination.Z);
            }
            catch (Exception ex)
            {
                LogException("TeleportToWaypoint", ex);
                Notify("Waypoint teleport failed safely. See log.");
            }
        }

        private void TeleportForward()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            Vector3 target = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, ped.Handle, 0.0f, 3.0f, 0.0f);
            TeleportTo(target.X, target.Y, target.Z);
            Notify("Teleported 3 metres forward.");
        }

        private void RepairCurrentVehicle()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped) || !ped.IsInVehicle())
            {
                Notify("Enter a vehicle first.");
                return;
            }
            Vehicle vehicle = ped.CurrentVehicle;
            if (!IsEntityUsable(vehicle)) return;
            Function.Call(Hash.SET_VEHICLE_FIXED, vehicle.Handle);
            Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicle.Handle);
            Function.Call(Hash.SET_VEHICLE_DIRT_LEVEL, vehicle.Handle, 0.0f);
            Notify("Vehicle repaired.");
        }

        private void AddStoryModeMoney(int amount)
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            uint modelHash = Function.Call<uint>(Hash.GET_ENTITY_MODEL, ped.Handle);
            bool protagonist = modelHash == GetHash("player_zero") ||
                modelHash == GetHash("player_one") || modelHash == GetHash("player_two");
            if (!protagonist)
            {
                Notify("Cash can only be added as Michael, Franklin, or Trevor. Restore a protagonist skin first.");
                return;
            }

            try
            {
                int currentCash = Game.Player.Money;
                long total = (long)currentCash + amount;
                if (total > StoryCashLimit) total = StoryCashLimit;
                Game.Player.Money = (int)total;
                _hideCashChangeUntilUtc = DateTime.UtcNow.AddSeconds(8);
                Notify("Added $" + amount.ToString("N0") + ". New cash: $" + ((int)total).ToString("N0"));
                Log("Story Mode cash changed: " + currentCash + " -> " + (int)total);
            }
            catch (Exception ex)
            {
                LogException("AddStoryModeMoney", ex);
                Notify("Cash update failed. See log.");
            }
        }

        private void SetStoryModeMoney(int amount)
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            uint modelHash = Function.Call<uint>(Hash.GET_ENTITY_MODEL, ped.Handle);
            bool protagonist = modelHash == GetHash("player_zero") ||
                modelHash == GetHash("player_one") || modelHash == GetHash("player_two");
            if (!protagonist)
            {
                Notify("Cash can only be set as Michael, Franklin, or Trevor. Restore a protagonist skin first.");
                return;
            }

            try
            {
                int targetCash = Math.Max(0, Math.Min(StoryCashLimit, amount));
                int previousCash = Game.Player.Money;
                Game.Player.Money = targetCash;
                _hideCashChangeUntilUtc = DateTime.UtcNow.AddSeconds(8);
                Notify("Cash set to $" + targetCash.ToString("N0"));
                Log("Story Mode cash set: " + previousCash + " -> " + targetCash);
            }
            catch (Exception ex)
            {
                LogException("SetStoryModeMoney", ex);
                Notify("Cash update failed. See log.");
            }
        }

        private void GiveFullWeaponLoadoutToPlayer()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            GiveFullWeaponLoadout(ped);
            _autoLoadoutGranted = true;
            Notify("Weapon arsenal delivered.");
        }

        private void GiveFullWeaponLoadout(Ped ped)
        {
            if (!IsEntityUsable(ped)) return;
            int granted = 0;
            for (int i = 0; i < _weaponNames.Length; i++)
            {
                uint weaponHash = GetHash(_weaponNames[i]);
                if (weaponHash == 0 || !Function.Call<bool>(Hash.IS_WEAPON_VALID, weaponHash)) continue;

                Function.Call(Hash.GIVE_WEAPON_TO_PED, ped.Handle, weaponHash, AmmoPerWeapon, false, false);
                ApplyPremiumToWeapon(ped, weaponHash);
                granted++;
            }

            uint preferredWeapon = GetHash("WEAPON_CARBINERIFLE");
            if (preferredWeapon != 0 && Function.Call<bool>(Hash.IS_WEAPON_VALID, preferredWeapon))
                Function.Call(Hash.SET_CURRENT_PED_WEAPON, ped.Handle, preferredWeapon, true);

            Log("Weapon loadout applied. Valid weapons granted: " + granted);
        }

        private void ApplyWeaponFinishToCurrentLoadout()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return;
            int owned = 0;
            int finished = 0;
            for (int i = 0; i < _weaponNames.Length; i++)
            {
                uint weaponHash = GetHash(_weaponNames[i]);
                if (weaponHash == 0 || !Function.Call<bool>(Hash.IS_WEAPON_VALID, weaponHash)) continue;
                if (!Function.Call<bool>((Hash)0x8DECB02F88F428BCUL, ped.Handle, weaponHash, false)) continue;
                owned++;
                if (ApplyPremiumToWeapon(ped, weaponHash)) finished++;
            }
            if (owned == 0) Notify("No owned weapons found. Give the arsenal first.");
            else if (finished == 0) Notify("No premium tint or finish is available for the owned weapons in this game build.");
            else Notify("Premium finish applied to " + finished + " of " + owned + " owned weapons.");
        }

        private bool ApplyPremiumToWeapon(Ped ped, uint weaponHash)
        {
            if (!IsEntityUsable(ped)) return false;
            bool premiumApplied = false;
            int tintCount = Function.Call<int>((Hash)0x5DCF6C5CAB2E9BF7UL, weaponHash);
            if (tintCount > 1)
            {
                int tintIndex = Math.Min(PremiumWeaponTint, tintCount - 1);
                Function.Call(Hash.SET_PED_WEAPON_TINT_INDEX, ped.Handle, weaponHash, tintIndex);
                premiumApplied = tintIndex > 0;
            }

            for (int i = 0; i < _weaponComponentNames.Length; i++)
            {
                string componentName = _weaponComponentNames[i];
                uint componentHash = GetHash(componentName);
                if (componentHash == 0) continue;
                if (!Function.Call<bool>(Hash.DOES_WEAPON_TAKE_WEAPON_COMPONENT, weaponHash, componentHash)) continue;
                Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED, ped.Handle, weaponHash, componentHash);
                if (componentName.IndexOf("VARMOD_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    componentName.IndexOf("_CAMO_", StringComparison.OrdinalIgnoreCase) >= 0)
                    premiumApplied = true;
            }

            // Heavy Sniper Mk II has mutually exclusive scope components. Apply the
            // thermal optic last so an earlier maximum/NV scope cannot remain selected.
            if (weaponHash == GetHash("WEAPON_HEAVYSNIPER_MK2"))
                premiumApplied = EnsureHeavySniperMkIIThermalScope(ped, weaponHash) || premiumApplied;
            return premiumApplied;
        }


        private bool EnsureHeavySniperMkIIThermalScope(Ped ped, uint weaponHash)
        {
            uint thermalScope = GetHash("COMPONENT_AT_SCOPE_THERMAL");
            if (thermalScope == 0 || !Function.Call<bool>(Hash.DOES_WEAPON_TAKE_WEAPON_COMPONENT, weaponHash, thermalScope))
            {
                Log("Heavy Sniper Mk II thermal scope is not supported by this game build.");
                return false;
            }

            string[] alternativeScopes = new string[]
            {
                "COMPONENT_AT_SCOPE_MACRO", "COMPONENT_AT_SCOPE_SMALL", "COMPONENT_AT_SCOPE_MEDIUM",
                "COMPONENT_AT_SCOPE_LARGE", "COMPONENT_AT_SCOPE_MAX", "COMPONENT_AT_SCOPE_NV"
            };
            for (int i = 0; i < alternativeScopes.Length; i++)
            {
                uint alternative = GetHash(alternativeScopes[i]);
                if (alternative == 0 || alternative == thermalScope) continue;
                if (Function.Call<bool>(Hash.DOES_WEAPON_TAKE_WEAPON_COMPONENT, weaponHash, alternative))
                    Function.Call((Hash)0x1E8BE90C74FB4C09UL, ped.Handle, weaponHash, alternative); // REMOVE_WEAPON_COMPONENT_FROM_PED
            }

            Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED, ped.Handle, weaponHash, thermalScope);
            bool installed = Function.Call<bool>((Hash)0xC593212475FAE340UL, ped.Handle, weaponHash, thermalScope); // HAS_PED_GOT_WEAPON_COMPONENT
            Log("Heavy Sniper Mk II thermal scope install: " + installed + ".");
            return installed;
        }

        private uint GetHash(string name)
        {
            return Function.Call<uint>(Hash.GET_HASH_KEY, name);
        }
















        private void SpawnVehicleModel(string modelName)
        {
            BeginVehicleSpawn(modelName, modelName);
        }









        private string GetSelectedAddonLabel()
        {
            if (_addonVehicles.Count == 0) return "No entries";
            AddonVehicle item = _addonVehicles[_addonIndex % _addonVehicles.Count];
            return item.name + " (" + item.model + ")";
        }

        private void NextAddon()
        {
            if (_addonVehicles.Count == 0)
            {
                Notify("No vehicles in addons.json.");
                return;
            }
            _addonIndex = (_addonIndex + 1) % _addonVehicles.Count;
            Notify("Selected: " + GetSelectedAddonLabel());
        }

        private void PreviousAddon()
        {
            if (_addonVehicles.Count == 0)
            {
                Notify("No vehicles in addons.json.");
                return;
            }
            _addonIndex = (_addonIndex - 1 + _addonVehicles.Count) % _addonVehicles.Count;
            Notify("Selected: " + GetSelectedAddonLabel());
        }

        private void SpawnSelectedAddon()
        {
            if (_addonVehicles.Count == 0)
            {
                Notify("No vehicles in addons.json.");
                return;
            }

            AddonVehicle item = _addonVehicles[_addonIndex % _addonVehicles.Count];
            BeginVehicleSpawn(item.model, item.name);
        }









        private void Log(string message)
        {
            // The main log is intentionally quiet: startup, errors, fatal exceptions
            // and crash-session markers only. Per-tick/action chatter is not persisted.
            if (String.IsNullOrEmpty(message) ||
                !message.StartsWith("Startup reached script constructor.", StringComparison.Ordinal)) return;
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [STARTUP] " + message + Environment.NewLine;
            AppendLogFile(_logPath, line);
        }

        private void LogException(string context, Exception ex)
        {
            string details = ex == null ? "<no exception object>" : ex.ToString();
            LogErrorRecord(context, details, ex == null ? "null" : ex.GetType().FullName + "|" + ex.Message);
        }

        private void LogFailure(string context, string details)
        {
            LogErrorRecord(context, details, details);
        }

        private void LogErrorRecord(string context, string details, string signatureDetails)
        {
            string signature = context + "|" + (signatureDetails ?? String.Empty);
            DateTime now = DateTime.UtcNow;
            if (String.Equals(signature, _lastLoggedError, StringComparison.Ordinal) &&
                (now - _lastLoggedErrorUtc).TotalSeconds < 5.0)
                return;

            _lastLoggedError = signature;
            _lastLoggedErrorUtc = now;
            string runtimeContext = String.Empty;
            try
            {
                Ped ped = Game.Player.Character;
                if (IsEntityUsable(ped))
                {
                    runtimeContext = "Player model: 0x" + Function.Call<uint>(Hash.GET_ENTITY_MODEL, ped.Handle).ToString("X8") + Environment.NewLine;
                    if (ped.IsInVehicle() && IsEntityUsable(ped.CurrentVehicle))
                        runtimeContext += "Vehicle model: 0x" + Function.Call<uint>(Hash.GET_ENTITY_MODEL, ped.CurrentVehicle.Handle).ToString("X8") + Environment.NewLine;
                }
            }
            catch { }

            string record = Environment.NewLine + "============================================================" + Environment.NewLine +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [ERROR] " + context + Environment.NewLine +
                "Product: " + ProductName + " " + ProductVersion + Environment.NewLine +
                "Menu page: " + (_currentPage ?? "<none>") + "; menu open: " + _menuOpen + Environment.NewLine +
                runtimeContext + (details ?? "<no details>") + Environment.NewLine;
            AppendLogFile(_errorLogPath, record);
        }

        private void NotifyError(string context)
        {
            if (_aborted) return;
            DateTime now = DateTime.UtcNow;
            if ((now - _lastErrorNotificationUtc).TotalSeconds < 5.0) return;
            _lastErrorNotificationUtc = now;
            Notify("AvernesSowtware error in " + context + ". Report: " + _errorLogPath);
        }

        private static int EncodingSize(string content)
        {
            return content == null ? 0 : System.Text.Encoding.UTF8.GetByteCount(content);
        }

        private void AppendLogFile(string path, string content)
        {
            try
            {
                Directory.CreateDirectory(_logDirectory);
                lock (_logSync)
                {
                    if (File.Exists(path) && new FileInfo(path).Length + EncodingSize(content) > 1024 * 1024)
                    {
                        string rotated = path + ".1";
                        if (File.Exists(rotated)) File.Delete(rotated);
                        File.Move(path, rotated);
                    }
                    using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
                    {
                        writer.Write(content);
                        writer.Flush();
                        stream.Flush(true);
                    }
                }
            }
            catch
            {
                // Logging never falls back into the legacy scripts root or creates YnixTrainer artifacts.
                try
                {
                    string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    if (String.IsNullOrEmpty(local)) return;
                    string fallbackDir = Path.Combine(local, "AvernesSowtware", "GTA V Legacy", "Logs");
                    Directory.CreateDirectory(fallbackDir);
                    string fallbackPath = Path.Combine(fallbackDir, Path.GetFileName(path));
                    using (FileStream stream = new FileStream(fallbackPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8)) { writer.Write(content); writer.Flush(); stream.Flush(true); }
                }
                catch { }
            }
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                string details = e.ExceptionObject == null ? "<null>" : e.ExceptionObject.ToString();
                string record = Environment.NewLine + "!!!!!!!!!!!!!!!! FATAL UNHANDLED EXCEPTION !!!!!!!!!!!!!!!!" + Environment.NewLine +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + Environment.NewLine +
                    "IsTerminating: " + e.IsTerminating + Environment.NewLine +
                    BuildCrashTelemetryContext() + details + Environment.NewLine;
                AppendLogFile(_crashReportPath, record);
            }
            catch { }
        }

        private void StartDiagnosticSession()
        {
            try
            {
                if (File.Exists(_sessionMarkerPath))
                {
                    string previous = String.Empty;
                    try { previous = File.ReadAllText(_sessionMarkerPath); } catch { }
                    string unexpected = Environment.NewLine + "!!!!!!!!!!!!!!!! POSSIBLE GTA/SHVDN CRASH !!!!!!!!!!!!!!!!" + Environment.NewLine +
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + Environment.NewLine +
                        "The previous trainer session marker was not cleared by a normal shutdown. A native GTA crash, forced process termination, or power loss is suspected." + Environment.NewLine +
                        "Previous session: " + previous + Environment.NewLine +
                        "A managed ScriptHookVDotNet script cannot capture a native GTA crash stack after the game process has terminated." + Environment.NewLine;
                    AppendLogFile(_crashReportPath, unexpected);
                }

                string marker = "PID=" + ProcessIdText() + Environment.NewLine +
                    "STARTED_UTC=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + Environment.NewLine;
                using (FileStream stream = new FileStream(_sessionMarkerPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(marker);
                    writer.Flush();
                    stream.Flush(true);
                }
            }
            catch (Exception ex) { LogException("StartDiagnosticSession", ex); }
        }

        private static string ProcessIdText()
        {
            try { return System.Diagnostics.Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture); }
            catch { return "unknown"; }
        }

        private void DeleteSessionMarker()
        {
            try { if (!String.IsNullOrEmpty(_sessionMarkerPath) && File.Exists(_sessionMarkerPath)) File.Delete(_sessionMarkerPath); }
            catch (Exception ex) { AppendLogFile(_errorLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [WARN] Could not remove session marker: " + ex + Environment.NewLine); }
        }

        private void OnDomainProcessExit(object sender, EventArgs e)
        {
            AppendLogFile(_logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [SHUTDOWN] Game process is exiting; trainer session is being closed normally." + Environment.NewLine);
            DeleteSessionMarker();
        }

        private static string[] BuildWeaponCatalog()
        {
            return new string[]
            {
                "WEAPON_KNIFE", "WEAPON_NIGHTSTICK", "WEAPON_HAMMER", "WEAPON_BAT", "WEAPON_GOLFCLUB", "WEAPON_CROWBAR", "WEAPON_BOTTLE", "WEAPON_DAGGER", "WEAPON_HATCHET", "WEAPON_KNUCKLE", "WEAPON_MACHETE", "WEAPON_FLASHLIGHT", "WEAPON_SWITCHBLADE", "WEAPON_BATTLEAXE", "WEAPON_POOLCUE", "WEAPON_WRENCH", "WEAPON_STONE_HATCHET",
                "WEAPON_PISTOL", "WEAPON_COMBATPISTOL", "WEAPON_APPISTOL", "WEAPON_PISTOL50", "WEAPON_SNSPISTOL", "WEAPON_HEAVYPISTOL", "WEAPON_VINTAGEPISTOL", "WEAPON_MARKSMANPISTOL", "WEAPON_REVOLVER", "WEAPON_DOUBLEACTION", "WEAPON_NAVYREVOLVER", "WEAPON_CERAMICPISTOL", "WEAPON_GADGETPISTOL", "WEAPON_FLAREGUN", "WEAPON_STUNGUN", "WEAPON_PISTOL_MK2", "WEAPON_SNSPISTOL_MK2", "WEAPON_REVOLVER_MK2",
                "WEAPON_MICROSMG", "WEAPON_SMG", "WEAPON_ASSAULTSMG", "WEAPON_COMBATPDW", "WEAPON_MACHINEPISTOL", "WEAPON_MINISMG", "WEAPON_SMG_MK2", "WEAPON_TECPISTOL",
                "WEAPON_PUMPSHOTGUN", "WEAPON_SAWNOFFSHOTGUN", "WEAPON_ASSAULTSHOTGUN", "WEAPON_BULLPUPSHOTGUN", "WEAPON_MUSKET", "WEAPON_HEAVYSHOTGUN", "WEAPON_DBSHOTGUN", "WEAPON_AUTOSHOTGUN", "WEAPON_PUMPSHOTGUN_MK2", "WEAPON_COMBATSHOTGUN",
                "WEAPON_ASSAULTRIFLE", "WEAPON_CARBINERIFLE", "WEAPON_ADVANCEDRIFLE", "WEAPON_SPECIALCARBINE", "WEAPON_BULLPUPRIFLE", "WEAPON_COMPACTRIFLE", "WEAPON_MILITARYRIFLE", "WEAPON_HEAVYRIFLE", "WEAPON_TACTICALRIFLE", "WEAPON_ASSAULTRIFLE_MK2", "WEAPON_CARBINERIFLE_MK2", "WEAPON_SPECIALCARBINE_MK2", "WEAPON_BULLPUPRIFLE_MK2",
                "WEAPON_MG", "WEAPON_COMBATMG", "WEAPON_GUSENBERG", "WEAPON_COMBATMG_MK2",
                "WEAPON_SNIPERRIFLE", "WEAPON_HEAVYSNIPER", "WEAPON_MARKSMANRIFLE", "WEAPON_HEAVYSNIPER_MK2", "WEAPON_MARKSMANRIFLE_MK2",
                "WEAPON_RPG", "WEAPON_GRENADELAUNCHER", "WEAPON_GRENADELAUNCHER_SMOKE", "WEAPON_MINIGUN", "WEAPON_FIREWORK", "WEAPON_RAILGUN", "WEAPON_HOMINGLAUNCHER", "WEAPON_COMPACTLAUNCHER", "WEAPON_RAYMINIGUN", "WEAPON_RAYCARBINE", "WEAPON_RAYPISTOL", "WEAPON_WIDOWMAKER", "WEAPON_UNHOLYHELLBRINGER", "WEAPON_UP_N_ATOMIZER", "WEAPON_COMPACTEMP_LAUNCHER", "WEAPON_RAILGUNXM3",
                "WEAPON_GRENADE", "WEAPON_BZGAS", "WEAPON_MOLOTOV", "WEAPON_STICKYBOMB", "WEAPON_PROXMINE", "WEAPON_SNOWBALL", "WEAPON_PIPEBOMB", "WEAPON_BALL", "WEAPON_SMOKEGRENADE", "WEAPON_FLARE",
                "WEAPON_FIREEXTINGUISHER", "WEAPON_PETROLCAN", "WEAPON_HAZARDCAN", "WEAPON_FERTILIZERCAN", "WEAPON_PARACHUTE"
            };
        }

        private static string[] BuildWeaponComponentCatalog()
        {
            return new string[]
            {
                "COMPONENT_AT_PI_FLSH", "COMPONENT_AT_PI_SUPP_02", "COMPONENT_AT_PI_SUPP", "COMPONENT_AT_PI_COMP",
                "COMPONENT_AT_AR_FLSH", "COMPONENT_AT_AR_SUPP", "COMPONENT_AT_AR_SUPP_02", "COMPONENT_AT_AR_AFGRIP",
                "COMPONENT_AT_SCOPE_MACRO", "COMPONENT_AT_SCOPE_SMALL", "COMPONENT_AT_SCOPE_MEDIUM", "COMPONENT_AT_SCOPE_LARGE", "COMPONENT_AT_SCOPE_MAX", "COMPONENT_AT_SCOPE_NV", "COMPONENT_AT_SCOPE_THERMAL",
                "COMPONENT_AT_MUZZLE_01", "COMPONENT_AT_MUZZLE_02", "COMPONENT_AT_MUZZLE_03", "COMPONENT_AT_MUZZLE_04", "COMPONENT_AT_MUZZLE_05", "COMPONENT_AT_MUZZLE_06", "COMPONENT_AT_MUZZLE_07", "COMPONENT_AT_MUZZLE_08", "COMPONENT_AT_MUZZLE_09", "COMPONENT_AT_MUZZLE_10", "COMPONENT_AT_MUZZLE_11", "COMPONENT_AT_MUZZLE_12", "COMPONENT_AT_MUZZLE_13", "COMPONENT_AT_MUZZLE_14", "COMPONENT_AT_MUZZLE_15", "COMPONENT_AT_MUZZLE_16", "COMPONENT_AT_MUZZLE_17",
                "COMPONENT_PISTOL_MK2_CLIP_02", "COMPONENT_SNSPISTOL_MK2_CLIP_02",
                "COMPONENT_SMG_MK2_CLIP_02", "COMPONENT_ASSAULTRIFLE_MK2_CLIP_02",
                "COMPONENT_CARBINERIFLE_MK2_CLIP_02", "COMPONENT_SPECIALCARBINE_MK2_CLIP_02", "COMPONENT_BULLPUPRIFLE_MK2_CLIP_02", "COMPONENT_COMBATMG_MK2_CLIP_02", "COMPONENT_MARKSMANRIFLE_MK2_CLIP_02", "COMPONENT_HEAVYSNIPER_MK2_CLIP_02",
                "COMPONENT_PISTOL_VARMOD_LUXE", "COMPONENT_APPISTOL_VARMOD_LUXE", "COMPONENT_PISTOL50_VARMOD_LUXE", "COMPONENT_HEAVYPISTOL_VARMOD_LUXE", "COMPONENT_MICROSMG_VARMOD_LUXE", "COMPONENT_SMG_VARMOD_LUXE", "COMPONENT_ASSAULTSMG_VARMOD_LOWRIDER", "COMPONENT_ASSAULTRIFLE_VARMOD_LUXE", "COMPONENT_CARBINERIFLE_VARMOD_LUXE", "COMPONENT_ADVANCEDRIFLE_VARMOD_LUXE", "COMPONENT_SPECIALCARBINE_VARMOD_LOWRIDER", "COMPONENT_BULLPUPRIFLE_VARMOD_LOW", "COMPONENT_MG_VARMOD_LOWRIDER", "COMPONENT_COMBATMG_VARMOD_LOWRIDER", "COMPONENT_PUMPSHOTGUN_VARMOD_LOWRIDER", "COMPONENT_SNIPERRIFLE_VARMOD_LUXE", "COMPONENT_MARKSMANRIFLE_VARMOD_LUXE",
                "COMPONENT_PISTOL_MK2_VARMOD_LUXE", "COMPONENT_SNSPISTOL_MK2_VARMOD_LUXE", "COMPONENT_REVOLVER_MK2_VARMOD_BOSS", "COMPONENT_REVOLVER_MK2_VARMOD_GOON", "COMPONENT_SMG_MK2_VARMOD_LUXE", "COMPONENT_PUMPSHOTGUN_MK2_VARMOD_LUXE", "COMPONENT_ASSAULTRIFLE_MK2_VARMOD_LUXE", "COMPONENT_CARBINERIFLE_MK2_VARMOD_LUXE", "COMPONENT_SPECIALCARBINE_MK2_VARMOD_LOWRIDER", "COMPONENT_BULLPUPRIFLE_MK2_VARMOD_LUXE", "COMPONENT_COMBATMG_MK2_VARMOD_LUXE", "COMPONENT_MARKSMANRIFLE_MK2_VARMOD_LUXE", "COMPONENT_HEAVYSNIPER_MK2_VARMOD_LUXE",
                "COMPONENT_PISTOL_CLIP_02", "COMPONENT_COMBATPISTOL_CLIP_02", "COMPONENT_APPISTOL_CLIP_02", "COMPONENT_PISTOL50_CLIP_02", "COMPONENT_SNSPISTOL_CLIP_02", "COMPONENT_HEAVYPISTOL_CLIP_02", "COMPONENT_VINTAGEPISTOL_CLIP_02",
                "COMPONENT_MICROSMG_CLIP_02", "COMPONENT_SMG_CLIP_02", "COMPONENT_ASSAULTSMG_CLIP_02", "COMPONENT_COMBATPDW_CLIP_02", "COMPONENT_MACHINEPISTOL_CLIP_02", "COMPONENT_MINISMG_CLIP_02",
                "COMPONENT_ASSAULTRIFLE_CLIP_02", "COMPONENT_CARBINERIFLE_CLIP_02", "COMPONENT_ADVANCEDRIFLE_CLIP_02", "COMPONENT_SPECIALCARBINE_CLIP_02", "COMPONENT_BULLPUPRIFLE_CLIP_02", "COMPONENT_COMPACTRIFLE_CLIP_02",
                "COMPONENT_ASSAULTRIFLE_CLIP_03", "COMPONENT_CARBINERIFLE_CLIP_03",
                "COMPONENT_SPECIALCARBINE_CLIP_03", "COMPONENT_COMPACTRIFLE_CLIP_03",
                "COMPONENT_MG_CLIP_02", "COMPONENT_COMBATMG_CLIP_02", "COMPONENT_GUSENBERG_CLIP_02", "COMPONENT_PUMPSHOTGUN_CLIP_02", "COMPONENT_ASSAULTSHOTGUN_CLIP_02", "COMPONENT_BULLPUPSHOTGUN_CLIP_02", "COMPONENT_HEAVYSHOTGUN_CLIP_02",
                "COMPONENT_SNIPERRIFLE_CLIP_02", "COMPONENT_HEAVYSNIPER_CLIP_02", "COMPONENT_MARKSMANRIFLE_CLIP_02", "COMPONENT_GRENADELAUNCHER_CLIP_02"
            };
        }

        private sealed class WeaponSnapshot
        {
            public readonly uint Hash;
            public readonly int Ammo;

            public WeaponSnapshot(uint hash, int ammo)
            {
                Hash = hash;
                Ammo = ammo;
            }
        }

        private sealed class SavedVehicle
        {
            public int Slot { get; set; }
            public int ModelHash { get; set; }
            public string Name { get; set; }
            public float SavedX { get; set; }
            public float SavedY { get; set; }
            public float SavedZ { get; set; }
            public float SavedHeading { get; set; }
            public int[] Mods { get; set; }
            public bool[] ModVariations { get; set; }
            public bool[] ToggleMods { get; set; }
            public bool[] Extras { get; set; }
            public int WheelType { get; set; }
            public int PrimaryColor { get; set; }
            public int SecondaryColor { get; set; }
            public int PearlescentColor { get; set; }
            public int WheelColor { get; set; }
            public int ModColor1Type { get; set; }
            public int ModColor1Index { get; set; }
            public int ModColor1Pearlescent { get; set; }
            public int ModColor2Type { get; set; }
            public int ModColor2Index { get; set; }
            public bool PrimaryCustom { get; set; }
            public int PrimaryRed { get; set; }
            public int PrimaryGreen { get; set; }
            public int PrimaryBlue { get; set; }
            public bool SecondaryCustom { get; set; }
            public int SecondaryRed { get; set; }
            public int SecondaryGreen { get; set; }
            public int SecondaryBlue { get; set; }
            public int WindowTint { get; set; }
            public int XenonColor { get; set; }
            public int InteriorColor { get; set; }
            public int DashboardColor { get; set; }
            public string PlateText { get; set; }
            public int PlateStyle { get; set; }
            public int Livery { get; set; }
            public int NeonRed { get; set; }
            public int NeonGreen { get; set; }
            public int NeonBlue { get; set; }
            public int TyreSmokeRed { get; set; }
            public int TyreSmokeGreen { get; set; }
            public int TyreSmokeBlue { get; set; }
            public bool[] NeonEnabled { get; set; }
        }

        public sealed class FavoriteLocation
        {
            public string name { get; set; }
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }

        private sealed class TrainerSettings
        {
            public bool GodMode = true;
            public bool AutoGiveWeapons = true;
            public bool InfiniteAmmo = true;
            public bool NoReload = true;
            public bool NeverWanted = true;
            public bool BodyguardInvincible = false;
            public int BodyguardHealth = 250;
            public int BodyguardArmour = 0;
            public bool InfiniteStamina = true;
            public bool InfiniteSpecialAbility = false;
            public bool UnlimitedOxygen = true;
            public bool AutoDeployParachute = true;
            public bool FastRun = true;
            public bool FastSwim = true;
            public bool SuperJump = false;
            public bool CopsIgnore = false;
            public bool Invisible = false;
            public bool NoRagdoll = false;
            public bool FranklinHairProtection = true;
            public bool ExplosiveAmmo = false;
            public bool FireAmmo = false;
            public bool SuperDamage = false;
            public bool VehicleGodMode = true;
            public bool VehicleRockets = false;
            public bool AntiEject = true;
            public bool Nitro = false;
            public float NitroPower = 1.50f;
            public bool DriftMode = false;
            public bool Speedometer = false;
            public bool BulletproofTires = false;
            public bool HornJump = false;
            public bool SpeedUnitMph = false;
            public bool NoClip = false;
            public bool NightVision = false;
            public bool ThermalVision = false;
            public bool FreezeTime = false;
            public bool LockWeather = true;
            public string Weather = "EXTRASUNNY";
            public float MenuX = 0.685f;
            public float MenuY = 0.006f;
            public float MenuWidth = 0.380f;
            public float MenuScale = 1.0f;
            public float MenuFontScale = 1.0f;
            public int MenuFontStyle = 0;
            public int MenuTheme = 0;
            public int MenuOpacity = 225;
            public int MenuRows = 9;
        }

        private sealed class MenuEntry
        {
            public readonly string Title;
            public readonly string Description;
            public readonly Func<string> Value;
            public readonly string ChildPage;
            public readonly Action Action;

            public MenuEntry(string title, string description, Func<string> value, string childPage, Action action)
            {
                Title = title;
                Description = description;
                Value = value;
                ChildPage = childPage;
                Action = action;
            }
        }

        public sealed class AddonVehicle
        {
            public string name { get; set; }
            public string model { get; set; }
            public string id { get; set; }
            public string category { get; set; }
            public string source { get; set; }
        }
    }
}
