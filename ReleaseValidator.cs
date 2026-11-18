using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

internal static class ReleaseValidator
{
    private static int _errors;
    private static int _warnings;

    private static void Error(string message)
    {
        _errors++;
        Console.WriteLine("[ERROR] " + message);
    }

    private static void Warn(string message)
    {
        _warnings++;
        Console.WriteLine("[WARN] " + message);
    }

    private static void CheckFile(string root, string relative)
    {
        if (!File.Exists(Path.Combine(root, relative))) Error("Required file is missing: " + relative);
        else Console.WriteLine("[OK] " + relative);
    }

    private static object ReadJson(string path)
    {
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        serializer.MaxJsonLength = Int32.MaxValue;
        serializer.RecursionLimit = 128;
        return serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
    }

    private static string GetString(IDictionary<string, object> item, string key)
    {
        object value;
        return item.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : String.Empty;
    }

    private static void ValidateVehicleJson(string path)
    {
        ValidateVehicleJson(path, true, -1);
    }

    private static void ValidateVehicleJson(string path, bool requireSuperDiamond, int expectedCount)
    {
        if (!File.Exists(path)) return;
        try
        {
            object root = ReadJson(path);
            object[] entries = root as object[];
            if (entries == null) throw new InvalidDataException("Root JSON value must be an array.");
            HashSet<string> models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasT20 = false;
            bool hasFbiSuv = false;
            bool hasSuperDiamond = false;
            for (int i = 0; i < entries.Length; i++)
            {
                IDictionary<string, object> item = entries[i] as IDictionary<string, object>;
                if (item == null) { Error(Path.GetFileName(path) + ": entry " + i + " is not an object."); continue; }
                string model = GetString(item, "model").Trim();
                if (model.Length == 0) { Error(Path.GetFileName(path) + ": entry " + i + " has no model."); continue; }
                if (!models.Add(model)) Error(Path.GetFileName(path) + ": duplicate model " + model + ".");
                if (String.Equals(model, "arwing", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(model, "tornadob", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(model, "proto", StringComparison.OrdinalIgnoreCase))
                    Error(Path.GetFileName(path) + ": retired vehicle model must not be present: " + model + ".");
                string category = GetString(item, "category");
                if (String.Equals(model, "t20", StringComparison.OrdinalIgnoreCase))
                {
                    hasT20 = true;
                    if (!String.Equals(category, "Super", StringComparison.OrdinalIgnoreCase)) Error("T20 must be in Super category in " + Path.GetFileName(path) + ".");
                }
                if (String.Equals(model, "fbi2", StringComparison.OrdinalIgnoreCase))
                {
                    hasFbiSuv = true;
                    if (!String.Equals(category, "Sedans / Coupes / SUVs", StringComparison.OrdinalIgnoreCase)) Error("FBI SUV model fbi2 must be in Sedans / Coupes / SUVs in " + Path.GetFileName(path) + ".");
                }
                if (String.Equals(model, "superd", StringComparison.OrdinalIgnoreCase))
                {
                    hasSuperDiamond = true;
                    if (GetString(item, "name").IndexOf("Super Diamond", StringComparison.OrdinalIgnoreCase) < 0)
                        Error("Model superd must be labelled Enus Super Diamond in " + Path.GetFileName(path) + ".");
                }
            }
            if (!hasT20) Warn("T20 is absent from " + Path.GetFileName(path) + ".");
            if (!hasFbiSuv) Warn("FBI SUV (model fbi2) is absent from " + Path.GetFileName(path) + ".");
            if (requireSuperDiamond && !hasSuperDiamond) Error("Enus Super Diamond (model superd) is absent from " + Path.GetFileName(path) + ".");
            if (expectedCount >= 0 && entries.Length != expectedCount)
                Error(Path.GetFileName(path) + " must contain exactly " + expectedCount + " records; found " + entries.Length + ".");
            Console.WriteLine("[OK] JSON " + Path.GetFileName(path) + ": " + entries.Length + " entries, " + models.Count + " unique models.");
        }
        catch (Exception ex) { Error("Invalid JSON " + path + ": " + ex.Message); }
    }

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: ReleaseValidator.exe <project-root>");
            return 2;
        }
        string root = Path.GetFullPath(args[0]);
        string[] required = new string[] {
            "AvernesSowtware.csproj", "AvernesSowtware.sln", "BUILD_RELEASE.bat", "START_HERE_RU.txt", "Install_AvernesSowtware.bat",
            "Uninstall_AvernesSowtware.bat", "build/Build.bat", "build/ReleaseValidator.cs", "build/ReleasePackager.cs",
            "src/Core/AvernesSowtwareScript.cs", "src/Core/SettingsPersistence.cs", "src/Core/RuntimeDiagnostics.cs",
            "src/Core/HotkeyManager.cs", "src/Core/TeleportHistory.cs", "src/Managers/VehicleSystems.cs",
            "src/Managers/VehicleCatalog.cs", "src/UI/MenuRenderer.cs",
            "package/AvernesSowtware/AvernesSowtware.ini", "package/AvernesSowtware/vehicle_catalog.json",
            "package/AvernesSowtware/Vehicles/vehicles.json", "package/AvernesSowtware/Vehicles/README.txt",
            "package/AvernesSowtware/addons.json", "package/AvernesSowtware/hotkeys.json", "README_RU.md", "assets/AvernesSowtware.png", "assets/AvernesSowtware.ico",
            "build/Test-InstallerIdempotence.ps1", "build/Migrate-LegacyData.ps1", "build/Finalize-InstallStorage.ps1",
            "docs/CHANGELOG_1_3_3_RU.md", "docs/TEST_REPORT_1_3_3_RU.md",
            "docs/CHANGELOG_1_3_4_RU.md", "docs/TEST_REPORT_1_3_4_RU.md",
            "docs/CHANGELOG_1_3_5_RU.md", "docs/TEST_REPORT_1_3_5_RU.md",
            "docs/CHANGELOG_1_3_6_RU.md", "docs/TEST_REPORT_1_3_6_RU.md",
            "docs/CHANGELOG_1_3_7_RU.md", "docs/TEST_REPORT_1_3_7_RU.md",
            "docs/CHANGELOG_1_3_8_RU.md", "docs/TEST_REPORT_1_3_8_RU.md",
            "docs/CHANGELOG_1_3_9_RU.md", "docs/TEST_REPORT_1_3_9_RU.md",
            "docs/CHANGELOG_1_4_3_RU.md", "docs/TEST_REPORT_1_4_3_RU.md", "tests/test_1_4_3_regressions.py",
            "docs/CHANGELOG_1_3_1_RU.md", "docs/TEST_REPORT_1_3_1_RU.md", "docs/REGRESSION_TEST_PLAN_RU.md",
            "docs/CHANGELOG_1_2_8_RU.md", "docs/TEST_REPORT_1_2_8_RU.md", "docs/AUDIT_1_2_8_PROGRESS_RU.md", "docs/CHANGELOG_1_2_9_RU.md", "docs/TEST_REPORT_1_2_9_RU.md", "docs/CHANGELOG_1_3_0_RU.md", "docs/TEST_REPORT_1_3_0_RU.md", "docs/NATIVE_REFERENCES_RU.md"
        };
        for (int i = 0; i < required.Length; i++) CheckFile(root, required[i].Replace('/', Path.DirectorySeparatorChar));

        string project = Path.Combine(root, "AvernesSowtware.csproj");
        if (File.Exists(project))
        {
            string xml = File.ReadAllText(project);
            if (xml.IndexOf("<TargetFrameworkVersion>v4.0</TargetFrameworkVersion>", StringComparison.OrdinalIgnoreCase) < 0)
                Error("TargetFrameworkVersion changed; this release must remain compatible with .NET Framework 4.0 API.");
            if (xml.IndexOf("<LangVersion>4</LangVersion>", StringComparison.OrdinalIgnoreCase) < 0)
                Error("LangVersion must remain 4 for the supported build environment.");
            string[] modules = new string[] { "src\\Core\\AvernesSowtwareScript.cs", "src\\Core\\SettingsPersistence.cs", "src\\Core\\RuntimeDiagnostics.cs", "src\\Core\\HotkeyManager.cs", "src\\Core\\TeleportHistory.cs", "src\\Managers\\VehicleSystems.cs", "src\\Managers\\VehicleCatalog.cs", "src\\UI\\MenuRenderer.cs" };
            for (int i = 0; i < modules.Length; i++)
                if (xml.IndexOf(modules[i], StringComparison.OrdinalIgnoreCase) < 0)
                    Error("Project does not compile source module " + modules[i] + ".");
        }

        string[] systemUsingModules = new string[] { "src/Managers/VehicleCatalog.cs", "src/Managers/VehicleSystems.cs" };
        for (int i = 0; i < systemUsingModules.Length; i++)
        {
            string modulePath = Path.Combine(root, systemUsingModules[i].Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(modulePath))
            {
                string moduleText = File.ReadAllText(modulePath);
                if (moduleText.IndexOf("using System;", StringComparison.Ordinal) < 0)
                    Error(systemUsingModules[i] + " is missing using System; for framework types used by this module.");
            }
        }

        string buildScript = Path.Combine(root, "build", "Build.bat");
        if (File.Exists(buildScript))
        {
            string buildText = File.ReadAllText(buildScript);
            string[] modules = new string[] { "src\\Core\\AvernesSowtwareScript.cs", "src\\Core\\SettingsPersistence.cs", "src\\Core\\RuntimeDiagnostics.cs", "src\\Core\\HotkeyManager.cs", "src\\Core\\TeleportHistory.cs", "src\\Managers\\VehicleSystems.cs", "src\\Managers\\VehicleCatalog.cs", "src\\UI\\MenuRenderer.cs" };
            for (int i = 0; i < modules.Length; i++)
                if (buildText.IndexOf(modules[i], StringComparison.OrdinalIgnoreCase) < 0)
                    Error("Build.bat does not compile source module " + modules[i] + ".");
        }
        string releaseScript = Path.Combine(root, "BUILD_RELEASE.bat");
        if (File.Exists(releaseScript))
        {
            string releaseText = File.ReadAllText(releaseScript);
            if (releaseText.IndexOf("--build-only --no-pause", StringComparison.OrdinalIgnoreCase) < 0)
                Error("BUILD_RELEASE.bat must compile without automatically installing into the user's GTA V folder.");
            if (releaseText.IndexOf("installation was not executed", StringComparison.OrdinalIgnoreCase) < 0)
                Error("Build report must clearly state that GTA V installation was not performed.");
        }

        string installer = Path.Combine(root, "Install_AvernesSowtware.bat");
        if (File.Exists(installer))
        {
            string installerText = File.ReadAllText(installer);
            if (installerText.IndexOf("copy /b /y", StringComparison.OrdinalIgnoreCase) < 0 ||
                installerText.IndexOf("vehicles.json", StringComparison.OrdinalIgnoreCase) < 0)
                Error("Installer must overwrite the shipped vehicle database on repeat installs.");
        }

        string vehicleCatalogSource = File.ReadAllText(Path.Combine(root, "src/Managers/VehicleCatalog.cs"));
        if (vehicleCatalogSource.IndexOf("String.Equals(model, \"fbi\", StringComparison.OrdinalIgnoreCase)) category = \"Sedans / Coupes / SUVs\"", StringComparison.Ordinal) >= 0)
            Error("FBI SUV category override must use model fbi2, not fbi.");

        ValidateVehicleJson(Path.Combine(root, "package/AvernesSowtware/vehicle_catalog.json"));
        ValidateVehicleJson(Path.Combine(root, "package/AvernesSowtware/Vehicles/vehicles.json"));
        // The bundled add-on list is a separate source. Validate its schema and exact
        // packaged count without requiring main-catalog aliases such as superd.
        ValidateVehicleJson(Path.Combine(root, "package/AvernesSowtware/addons.json"), false, 85);
        try
        {
            JavaScriptSerializer hotkeySerializer = new JavaScriptSerializer();
            Dictionary<string, object> hotkeyRoot = hotkeySerializer.Deserialize<Dictionary<string, object>>(
                File.ReadAllText(Path.Combine(root, "package/AvernesSowtware/hotkeys.json")));
            if (hotkeyRoot == null || !hotkeyRoot.ContainsKey("schema_version") || Convert.ToInt32(hotkeyRoot["schema_version"]) != 1 || !hotkeyRoot.ContainsKey("bindings"))
                Error("hotkeys.json does not match schema_version 1.");
        }
        catch (Exception ex) { Error("hotkeys.json validation failed: " + ex.Message); }
        string vehiclesJson = File.ReadAllText(Path.Combine(root, "package/AvernesSowtware/Vehicles/vehicles.json"));
        if (vehiclesJson.IndexOf("\"name\": \"Enus Super Diamond\"", StringComparison.Ordinal) < 0 ||
            vehiclesJson.IndexOf("\"model\": \"superd\"", StringComparison.Ordinal) < 0)
            Error("Enus Super Diamond must use the real GTA model code superd in the built-in catalog.");

        string[] sourceFiles = new string[] {
            "src/Core/AvernesSowtwareScript.cs", "src/Core/SettingsPersistence.cs", "src/Core/RuntimeDiagnostics.cs",
            "src/Core/HotkeyManager.cs", "src/Core/TeleportHistory.cs",
            "src/Managers/VehicleSystems.cs", "src/Managers/VehicleCatalog.cs",
            "src/UI/MenuRenderer.cs"
        };
        StringBuilder allSource = new StringBuilder();
        for (int i = 0; i < sourceFiles.Length; i++)
        {
            string path = Path.Combine(root, sourceFiles[i].Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) allSource.AppendLine(File.ReadAllText(path));
        }
        string source = allSource.ToString();
        string[] requiredMarkers = new string[] {
            "ApplyAntiEject", "ProcessFranklinHairProtection", "TryDeleteTrackedVehicle",
            "IsEntityUsable", "RunEntityWatchdog", "RecordManagerTick", "BuildCrashTelemetryContext",
            "DeserializeVehicleEntriesWithSchema", "RebuildCombinedVehicleCatalogCache", "FindExistingTrainerVehicle",
            "ProcessHotkeyCapture", "ReloadJsonConfigurations", "UndoLastTeleport",
            "NitroPower", "ProcessVehicleRocketFire", "SET_VEHICLE_WINDOW_TINT",
            "GET_VEHICLE_PED_IS_IN", "FranklinHairProtection", "GET_ENTITY_SPEED", "fbi2",
            "ProcessBodyguardAim", "GetPlayerAimedNpcHandle", "TASK_ENTER_VEHICLE",
            "ApplyAllVehicleBodyKits", "CycleVehicleWheelType", "CycleVehicleMod", "superDiamondAlias"
        };
        for (int i = 0; i < requiredMarkers.Length; i++)
            if (source.IndexOf(requiredMarkers[i], StringComparison.Ordinal) < 0) Error("Expected feature marker is missing: " + requiredMarkers[i]);
        if (source.IndexOf("Math.Min(95.0f", StringComparison.Ordinal) >= 0) Error("The old fixed 95 m/s nitro cap is still present.");
        string mainSource = File.ReadAllText(Path.Combine(root, "src/Core/AvernesSowtwareScript.cs"));
        string vehicleSource = File.ReadAllText(Path.Combine(root, "src/Managers/VehicleSystems.cs"));
        string catalogSource = File.ReadAllText(Path.Combine(root, "src/Managers/VehicleCatalog.cs"));
        string uiSource = File.ReadAllText(Path.Combine(root, "src/UI/MenuRenderer.cs"));
        if (vehicleSource.IndexOf("0x18FF00FC7EFF559EUL", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("Function.Call((Hash)0xAB54A438726D25D5UL", StringComparison.Ordinal) >= 0)
            Error("1.3.7 Nitro must use physics force instead of rewriting absolute vehicle speed.");
        if (vehicleSource.IndexOf("CalculateNitroMaximumSpeedMetersPerSecond", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("150.0f / 3.6f", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("maximumNitroSpeed", StringComparison.Ordinal) < 0)
            Error("1.3.8 Nitro compatibility must scale the target speed linearly: x3=450 km/h and x8=1200 km/h.");
        int vehicleSettingsStart = vehicleSource.IndexOf("private void ApplyVehicleSettings", StringComparison.Ordinal);
        int vehicleSettingsEnd = vehicleSettingsStart >= 0
            ? vehicleSource.IndexOf("private void RestoreVehicleProtection", vehicleSettingsStart, StringComparison.Ordinal)
            : -1;
        string vehicleSettingsBody = vehicleSettingsStart >= 0 && vehicleSettingsEnd > vehicleSettingsStart
            ? vehicleSource.Substring(vehicleSettingsStart, vehicleSettingsEnd - vehicleSettingsStart)
            : String.Empty;
        if (vehicleSettingsBody.IndexOf("SET_VEHICLE_DEFORMATION_FIXED", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("0x772282EBEB95E682UL", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("if (godMode)", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("wasGodModeAlreadyApplied", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("if (!wasGodModeAlreadyApplied)", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("IS_PED_GETTING_INTO_A_VEHICLE", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("_pendingGodModeRepairVehicleHandle", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("private void RepairVehicleForGodMode", StringComparison.Ordinal) < 0)
            Error("1.4.5 vehicle God Mode must defer initial repair until the player's entry animation has completed.");
        if (mainSource.IndexOf("SuppressActiveMilitaryCombat", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0x4859F1FC66A6278EUL", StringComparison.Ordinal) < 0)
            Error("1.3.7 targeted Fort Zancudo combat suppression is missing.");
        if (mainSource.IndexOf("RestoreParachuteVisualDefaults", StringComparison.Ordinal) >= 0) Error("Unsafe parachute visual reset routine is still present.");
        if (mainSource.IndexOf("StartAutopilot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            mainSource.IndexOf("StopAutopilot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            vehicleSource.IndexOf("StartAutopilot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            vehicleSource.IndexOf("StopAutopilot", StringComparison.OrdinalIgnoreCase) >= 0)
            Error("Smart Autopilot code must be fully removed.");
        if (mainSource.IndexOf("TeleportToWaypoint", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("GET_FIRST_BLIP_INFO_ID", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("GET_BLIP_INFO_ID_COORD", StringComparison.Ordinal) < 0)
            Error("Independent waypoint teleport function is missing.");
        if (mainSource.IndexOf("BuildAtlasMenus", StringComparison.Ordinal) >= 0 ||
            mainSource.IndexOf("BuildWardrobeMenus", StringComparison.Ordinal) >= 0 ||
            File.Exists(Path.Combine(root, "src/Managers/Atlas.cs")) ||
            File.Exists(Path.Combine(root, "src/Managers/Wardrobe.cs")))
            Error("Atlas / Wardrobe feature modules must be removed from the release.");
        if (mainSource.IndexOf("Максимальный тюнинг", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("Установить все обвесы", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("Оружие телохранителей", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("GiveSelectedWeaponToBodyguards", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("ApplyBodyguardVitals", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("BodyguardInvincible", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0x2975C866E6713290UL", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0xC20E50AA46D09CA8UL", StringComparison.Ordinal) < 0)
            Error("1.3.6 bodyguard or tuning features are incomplete.");
        if (vehicleSource.IndexOf("ApplyAllVehicleBodyKits", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("CycleVehicleWheelType", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("CycleVehicleWheelStyle", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("CycleVehicleMod", StringComparison.Ordinal) < 0)
            Error("1.3.6 vehicle customization features are incomplete.");
        if (vehicleSource.IndexOf("SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("RestoreAntiAircraftTrailerWheels", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("SET_VEHICLE_DEFORMATION_FIXED", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("SET_VEHICLE_ENGINE_CAN_DEGRADE", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("AddMilliseconds(250)", StringComparison.Ordinal) < 0)
            Error("1.3.6 plate, trailer wheel or vehicle god mode regression fix is missing.");
        if (mainSource.IndexOf("EnsureBodyguardWeaponAndVulnerability", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0x1913FE4CBF41C463UL, handle, 6, false", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0x1913FE4CBF41C463UL, handle, 2, false", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("MarkAsNoLongerNeeded()", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("TeleportToWaypoint", StringComparison.Ordinal) < 0)
            Error("1.4.5 bodyguard vulnerability and deferred cleanup safety are incomplete.");
        if (mainSource.IndexOf("ApplyPopulationDensity", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("_bodyguardBoardingSeats", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("SET_EVERYONE_IGNORE_PLAYER", StringComparison.Ordinal) >= 0)
            Error("1.3.6 world AI, population or bodyguard boarding fixes are missing.");
        int fortDispatchStart = mainSource.IndexOf("private void UpdateFortZancudoArmyDispatch", StringComparison.Ordinal);
        int fortDispatchEnd = fortDispatchStart >= 0
            ? mainSource.IndexOf("private void RestoreFortZancudoArmyDispatch", fortDispatchStart, StringComparison.Ordinal)
            : -1;
        string fortDispatchBody = fortDispatchStart >= 0 && fortDispatchEnd > fortDispatchStart
            ? mainSource.Substring(fortDispatchStart, fortDispatchEnd - fortDispatchStart)
            : String.Empty;
        if (fortDispatchBody.IndexOf("ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0xDC0F817884CDD856UL, 15, false", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0x9B2BD3773123EA2FUL, 15, true", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0xDC0F817884CDD856UL, 2, false", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0x9B2BD3773123EA2FUL, 2, true", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("AddMilliseconds(900)", StringComparison.Ordinal) < 0)
            Error("1.4.5 Fort Zancudo Army dispatch suppression and helicopter dispatch must be periodically disabled and blocked while inside the base.");
        if (catalogSource.IndexOf("superDiamondAlias", StringComparison.Ordinal) < 0 ||
            File.ReadAllText(Path.Combine(root, "src/Core/AvernesSowtwareScript.cs")).IndexOf("ProductVersion = \"1.4.5\"", StringComparison.Ordinal) < 0)
            Error("1.3.6 version marker or Super Diamond search alias is missing.");
        if (vehicleSource.IndexOf("0xBAA045B4E42F3C06UL", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("0x93A3996368C94158UL", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("0xB59E4BD37AE292DBUL", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("CalculateNitroMaximumSpeedMetersPerSecond", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("Reset vehicle maximum speed", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("0xBB40DD2270B65366UL, handle, -1", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("&& playerIsDriver", StringComparison.Ordinal) < 0)
            Error("1.4.5 Nitro must be restricted to the player driver seat and retain speed/transmission/torque reset.");
        if (mainSource.IndexOf("Custom plate: YNIX", StringComparison.OrdinalIgnoreCase) >= 0)
            Error("Legacy YNIX custom plate item is still exposed in the tuning menu.");
        if (mainSource.IndexOf("ProcessAutomaticParachuteDeployment", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0x16E42E800B472221UL", StringComparison.Ordinal) < 0)
            Error("1.4.1 automatic parachute deployment is missing.");
        if (mainSource.IndexOf("StartDiagnosticSession", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("POSSIBLE GTA/SHVDN CRASH", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("Path.Combine(_scriptsDirectory, \"AvernesSowtware\")", StringComparison.Ordinal) < 0)
            Error("1.4.5 crash-session diagnostics path or marker is missing.");
        if (mainSource.IndexOf("0x965791A9A488A062UL, handle, playerVehicleHandle", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("10000, stagedSeat, 1.0f, 1, 0)", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("_bodyguardBoardingEnterAtUtc", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("normal animated entry", StringComparison.Ordinal) < 0)
            Error("1.4.5 staged vehicle door opening and normal animated entry are missing.");
        if (mainSource.IndexOf("NEW_LOAD_SCENE_START_SPHERE", StringComparison.Ordinal) < 0 || mainSource.IndexOf("IS_NEW_LOAD_SCENE_LOADED", StringComparison.Ordinal) < 0) Error("Safe remote-scene loading is missing from ground teleports.");
        if (mainSource.IndexOf("bool sceneReady = _groundLoadSceneStarted &&", StringComparison.Ordinal) < 0) Error("Ground teleport does not require a successfully started/loaded scene.");
        if (mainSource.IndexOf("Mount Chiliad", StringComparison.Ordinal) < 0 || mainSource.IndexOf("TeleportToGround(501.2f, 5604.5f, 797.9f)", StringComparison.Ordinal) < 0) Error("Mount Chiliad does not use the ground-loading teleport path.");
        if (vehicleSource.IndexOf("aircraftTransfer", StringComparison.OrdinalIgnoreCase) >= 0) Error("Unreliable in-air transfer code must be disabled in this version.");
        if (vehicleSource.IndexOf("distance < 650.0f", StringComparison.Ordinal) < 0 || vehicleSource.IndexOf("TryDeleteTrackedVehicle(handle, \"distant\")", StringComparison.Ordinal) < 0) Error("Distant empty trainer-vehicle cleanup is missing.");
        if (catalogSource.IndexOf("IsRetiredOrUnsupportedVehicleModel", StringComparison.Ordinal) < 0 ||
            catalogSource.IndexOf("IsVehicleModelAvailableInCurrentBuild", StringComparison.Ordinal) < 0 ||
            catalogSource.IndexOf("String.Equals(model, \"lm87\"", StringComparison.Ordinal) < 0)
            Error("Unavailable vehicle-model filter or Legacy 1.0.2245 LM87 exclusion is missing.");
        if (mainSource.IndexOf("Keep Franklin hairstyle after cutscenes", StringComparison.Ordinal) >= 0) Error("Separate Franklin hairstyle toggle is still present.");
        if (mainSource.IndexOf("ProcessFranklinHairProtection", StringComparison.Ordinal) < 0) Error("Automatic Franklin hairstyle preservation is missing.");
        if (uiSource.IndexOf("AvernesSowtware", StringComparison.Ordinal) < 0 || uiSource.IndexOf("itemIndex + 1", StringComparison.Ordinal) < 0) Error("AvernesSowtware branded numbered menu is missing.");
        if (uiSource.IndexOf("logoCx", StringComparison.Ordinal) >= 0 || uiSource.IndexOf("DrawRect(logoCx", StringComparison.Ordinal) >= 0) Error("The prohibited crosshair/red-center mark is still present in the menu header.");
        if (uiSource.IndexOf("progressWidth", StringComparison.Ordinal) >= 0 || uiSource.IndexOf("LIVE", StringComparison.Ordinal) >= 0) Error("Obsolete scrollbar or rail decorations remain in the menu renderer.");
        if (mainSource.IndexOf("ApplyMilitaryIgnore", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("_militaryRelationshipOriginalPlayerToArmy", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0xBF25EB89375A37ADUL, 3, armyGroup, playerGroup", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0xBF25EB89375A37ADUL, 3, playerGroup, armyGroup", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0xDC0F817884CDD856UL", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0x9B2BD3773123EA2FUL", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("RestoreFortZancudoArmyDispatch", StringComparison.Ordinal) < 0)
            Error("1.4.5 Fort Zancudo Army/air dispatch suppression and restoration are incomplete.");
        if (mainSource.IndexOf("ApplyMilitaryIgnore(ped, false)", StringComparison.Ordinal) < 0) Error("Military ignore state is not restored when the trainer unloads.");

        string persistenceSource = File.ReadAllText(Path.Combine(root, "src/Core/SettingsPersistence.cs"));
        if (persistenceSource.IndexOf("TryAtomicWriteFile", StringComparison.Ordinal) < 0 ||
            persistenceSource.IndexOf("QueueSettingsSnapshot", StringComparison.Ordinal) < 0 ||
            persistenceSource.IndexOf("ThreadPool.QueueUserWorkItem", StringComparison.Ordinal) < 0 ||
            persistenceSource.IndexOf("GetSha256", StringComparison.Ordinal) < 0)
            Error("Atomic SHA-256 verified asynchronous settings persistence is incomplete.");
        if (mainSource.IndexOf("MigrateFileWithVerification", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("ResolveWritableDataDirectory", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("CleanupObsoleteFiles", StringComparison.Ordinal) < 0)
            Error("Runtime migration, writable data fallback, or safe cleanup is missing.");
        if (mainSource.IndexOf("if (Game.Player == null) return;", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("catch (ThreadAbortException)", StringComparison.Ordinal) < 0)
            Error("Notification null guard or explicit thread-abort handling is missing.");
        if (vehicleSource.IndexOf("const int maximumPureBlackTint = 1", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("StandardVehicleColorRgb", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("GetWheelTypeForVehicleClass", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("classPreference", StringComparison.Ordinal) < 0)
            Error("Max tuning does not explicitly preserve matte body color, maximum tint, and class-based wheels.");
        if (mainSource.IndexOf("thermalScopeOwnsVision", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0x2E397FD2ECD37C87UL", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0xC593212475FAE340UL", StringComparison.Ordinal) < 0)
            Error("Thermal optic protection against the global thermal-vision override is missing.");

        string migrationScript = File.ReadAllText(Path.Combine(root, "build/Migrate-LegacyData.ps1"));
        string finalizerScript = File.ReadAllText(Path.Combine(root, "build/Finalize-InstallStorage.ps1"));
        string installerScript = File.ReadAllText(Path.Combine(root, "Install_AvernesSowtware.bat"));
        if (migrationScript.IndexOf("Get-FileHash", StringComparison.Ordinal) < 0 ||
            migrationScript.IndexOf("YnixTrainerExtended.dll", StringComparison.Ordinal) < 0 ||
            migrationScript.IndexOf("YnixTrainerExtended.ini", StringComparison.Ordinal) < 0 ||
            migrationScript.IndexOf("Remove-Item", StringComparison.Ordinal) < 0)
            Error("Verified legacy migration and safe old-file removal are incomplete.");
        if (finalizerScript.IndexOf("SHA256", StringComparison.Ordinal) < 0 ||
            finalizerScript.IndexOf(".bak", StringComparison.Ordinal) < 0)
            Error("Installer does not create and verify critical-file backups/checksums.");
        if (installerScript.IndexOf("WindowsPrincipal", StringComparison.Ordinal) < 0 ||
            installerScript.IndexOf("Migrate-LegacyData.ps1", StringComparison.Ordinal) < 0 ||
            installerScript.IndexOf("Finalize-InstallStorage.ps1", StringComparison.Ordinal) < 0)
            Error("Installer permission check or safe migration/storage finalization is missing.");

        if (source.IndexOf("TODO", StringComparison.OrdinalIgnoreCase) >= 0 || source.IndexOf("FIXME", StringComparison.OrdinalIgnoreCase) >= 0)
            Warn("Source contains TODO/FIXME markers; review before release.");

        Console.WriteLine("Validation summary: errors=" + _errors + ", warnings=" + _warnings + ".");
        return _errors == 0 ? 0 : 1;
    }
}

