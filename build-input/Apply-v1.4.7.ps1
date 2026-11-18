$ErrorActionPreference = 'Stop'
$root = $env:SOURCE_ROOT
$nl = [string][char]10
function Read-Text([string]$relative) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing source file: $relative" }
    return ([IO.File]::ReadAllText($path) -replace '\r','')
}
function Write-Text([string]$relative, [string]$text) {
    [IO.File]::WriteAllText((Join-Path $root $relative), $text, (New-Object Text.UTF8Encoding($true)))
}
function Replace-Exact([string]$text, [string]$old, [string]$new, [string]$label) {
    $count = ([regex]::Matches($text, [regex]::Escape($old))).Count
    if ($count -ne 1) { throw "Expected exactly one match for $label; found $count" }
    return $text.Replace($old, $new)
}

$corePath = 'src/Core/AvernesSowtwareScript.cs'
$core = Read-Text $corePath
$core = Replace-Exact $core 'private const string ProductVersion = "1.4.6";' 'private const string ProductVersion = "1.4.7";' 'product version'
$core = Replace-Exact $core '        private DateTime _lastLoggedErrorUtc;' ('        private DateTime _lastLoggedErrorUtc;' + $nl + '        private string _lastLogWriteFailure;') 'log failure field'
$core = Replace-Exact $core '            _activeLogDirectory = _logDirectory;' ('            _activeLogDirectory = _logDirectory;' + $nl + '            try { Directory.CreateDirectory(_logDirectory); } catch { }') 'create log directory'
$core = Replace-Exact $core '_logPath = Path.Combine(_logDirectory, "AvernesSowtware.log");' '_logPath = Path.Combine(_logDirectory, "session.log");' 'session path'
$core = Replace-Exact $core '_errorLogPath = Path.Combine(_logDirectory, "AvernesSowtware_errors.log");' '_errorLogPath = Path.Combine(_logDirectory, "error.log");' 'error path'
$core = Replace-Exact $core '_crashReportPath = Path.Combine(_logDirectory, "AvernesSowtware_crashes.log");' '_crashReportPath = Path.Combine(_logDirectory, "crash.log");' 'crash path'
$core = Replace-Exact $core '_sessionMarkerPath = Path.Combine(_logDirectory, "AvernesSowtware.session");' '_sessionMarkerPath = Path.Combine(_logDirectory, "session.marker");' 'marker path'
$core = Replace-Exact $core '            // AppendLogFile creates the target directory itself and falls back to LocalAppData.' '            // All runtime diagnostics are pinned to scripts/AvernesSowtware_Logs; no AppData/Documents fallback is permitted.' 'log comment'
$core = Replace-Exact $core '            transport.Add(ToggleEntry("Drift Mode", "DriftMode", delegate { return _settings.DriftMode; }, delegate(bool v) { _settings.DriftMode = v; }));' ('            transport.Add(ToggleEntry("Drift Mode", "DriftMode", delegate { return _settings.DriftMode; }, delegate(bool v) { _settings.DriftMode = v; }));' + $nl + '            transport.Add(ToggleEntry("High-Speed Stability", "VehicleStability", delegate { return _settings.VehicleStability; }, delegate(bool v) { _settings.VehicleStability = v; }));') 'stability menu'
$core = Replace-Exact $core '            transport.Add(ActionEntry("Repair & wash vehicle", "Repair", "Manual action only; no automatic repair.", RepairCurrentVehicle));' ('            transport.Add(ActionEntry("Wash vehicle", "WashVehicle", "Remove road dirt and decals without repairing damage.", WashCurrentVehicle));' + $nl + '            transport.Add(ActionEntry("Repair & wash vehicle", "Repair", "Manual action only; repairs deformation and cleans the vehicle.", RepairCurrentVehicle));') 'wash menu'
$core = Replace-Exact $core '        private void RepairCurrentVehicle()' @'
        private void WashCurrentVehicle()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped) || !ped.IsInVehicle())
            {
                Notify("Enter a vehicle first.");
                return;
            }
            Vehicle vehicle = ped.CurrentVehicle;
            if (!IsEntityUsable(vehicle)) return;
            try
            {
                Function.Call(Hash.SET_VEHICLE_DIRT_LEVEL, vehicle.Handle, 0.0f);
                Function.Call((Hash)0x5B712761429DBC14UL, vehicle.Handle, 1.0f); // WASH_DECALS_FROM_VEHICLE
                Notify("Vehicle washed. Damage was not repaired.");
                Log("Vehicle washed: model=" + vehicle.Model.Hash + "; handle=" + vehicle.Handle + ".");
            }
            catch (Exception ex)
            {
                LogException("WashCurrentVehicle", ex);
                NotifyError("Wash vehicle");
            }
        }

        private void RepairCurrentVehicle()
'@ 'wash method'
$core = Replace-Exact $core 'public bool DriftMode = false;' ('public bool DriftMode = false;' + $nl + '            public bool VehicleStability = false;') 'stability default'
$core = Replace-Exact $core 'AppendLogFile(_logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [SHUTDOWN] Game process is exiting; trainer session is being closed normally." + Environment.NewLine);' 'AppendLogFile(_logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [SESSION END] Game process is exiting; trainer session is being closed normally." + Environment.NewLine);' 'session end'
$core = Replace-Exact $core '                string marker = "PID=" + ProcessIdText() + Environment.NewLine +' ('                AppendLogFile(_logPath, Environment.NewLine + "[SESSION START] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +' + $nl + '                    " | PID=" + ProcessIdText() + " | Version=" + ProductVersion + Environment.NewLine);' + $nl + '                string marker = "PID=" + ProcessIdText() + Environment.NewLine +') 'session start'
$start = $core.IndexOf('        private string GetEffectiveLogPath(string path)')
$end = $core.IndexOf('        private void OnDomainUnhandledException', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'Could not locate log writer methods' }
$logMethods = @'
        private string GetEffectiveLogPath(string path)
        {
            return Path.Combine(_logDirectory, Path.GetFileName(path));
        }

        private void AppendLogFile(string path, string content)
        {
            string effectivePath = GetEffectiveLogPath(path);
            Exception lastError = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    string effectiveDirectory = Path.GetDirectoryName(effectivePath);
                    if (String.IsNullOrEmpty(effectiveDirectory)) throw new IOException("Log directory is unavailable.");
                    Directory.CreateDirectory(effectiveDirectory);
                    lock (_logSync)
                    {
                        if (File.Exists(effectivePath) && new FileInfo(effectivePath).Length + EncodingSize(content) > 1024 * 1024)
                        {
                            string rotated = effectivePath + ".1";
                            if (File.Exists(rotated)) File.Delete(rotated);
                            File.Move(effectivePath, rotated);
                        }
                        using (FileStream stream = new FileStream(effectivePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                        {
                            writer.Write(content);
                            writer.Flush();
                            stream.Flush();
                        }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < 2) Thread.Sleep(40 * (attempt + 1));
                }
            }
            _lastLogWriteFailure = lastError == null ? "Unknown log write failure." : lastError.GetType().FullName + ": " + lastError.Message;
        }

'@
$core = $core.Substring(0,$start) + $logMethods + $core.Substring($end)

$core = Replace-Exact $core '        private void ProcessBodyguardFollowing(Ped player)' @'
        private void IssueBodyguardFollowTask(int pedHandle, int playerHandle)
        {
            if (!IsPedHandleUsable(pedHandle) || !IsPedHandleUsable(playerHandle)) return;
            int formationIndex = _bodyguards.IndexOf(pedHandle);
            if (formationIndex < 0) formationIndex = _bodyguards.Count;
            int row = formationIndex / 2;
            float offsetX = (formationIndex % 2 == 0) ? -1.35f : 1.35f;
            float offsetY = -1.8f - (row * 1.35f);
            Function.Call((Hash)0x304AE42E357B8C7EUL, pedHandle, playerHandle,
                offsetX, offsetY, 0.0f, 2.0f, -1, 1.75f, true); // TASK_FOLLOW_TO_OFFSET_OF_ENTITY
            Function.Call((Hash)0x971D38760FBC02EFUL, pedHandle, true); // SET_PED_KEEP_TASK
        }

        private void ProcessBodyguardFollowing(Ped player)
'@ 'bodyguard formation helper'

$core = Replace-Exact $core '                Function.Call((Hash)0x304AE42E357B8C7EUL, guard, player.Handle,
                    -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true); // TASK_FOLLOW_TO_OFFSET_OF_ENTITY
                Function.Call((Hash)0x971D38760FBC02EFUL, guard, true); // SET_PED_KEEP_TASK' '                IssueBodyguardFollowTask(guard, player.Handle);' 'spawn follow task'

$core = Replace-Exact $core '                            Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                                -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true); // TASK_FOLLOW_TO_OFFSET_OF_ENTITY
                            Function.Call((Hash)0x971D38760FBC02EFUL, handle, true); // SET_PED_KEEP_TASK' '                            IssueBodyguardFollowTask(handle, player.Handle);' 'player-target follow task'

$core = Replace-Exact $core '                                Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                                    -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true);' '                                IssueBodyguardFollowTask(handle, player.Handle);' 'long-distance follow task'

$core = Replace-Exact $core '                            Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                                -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true);' '                            IssueBodyguardFollowTask(handle, player.Handle);' 'boarding fallback follow task'

$core = Replace-Exact $core '                        Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                            -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true);' '                        IssueBodyguardFollowTask(handle, player.Handle);' 'normal follow task'

$core = Replace-Exact $core '                    Function.Call((Hash)0xBF0FD6E56C964FCBUL, ped.Handle, GetHash("WEAPON_PARACHUTE"), 1, false, false); // GIVE_WEAPON_TO_PED' ('                    uint parachuteGadget = GetHash("GADGET_PARACHUTE");' + $nl + '                    if (!Function.Call<bool>((Hash)0x8DECB02F88F428BCUL, ped.Handle, parachuteGadget, false))' + $nl + '                        Function.Call((Hash)0xBF0FD6E56C964FCBUL, ped.Handle, parachuteGadget, 1, false, false); // GIVE_WEAPON_TO_PED') 'parachute gadget hash'

$core = Replace-Exact $core '                // Keep Army vehicle and police helicopter dispatch enabled. Newer Wanted / 911
                // owns these response services; disabling or blocking them can leave spawned
                // emergency transports without a valid mission/driver task.
                Function.Call((Hash)0xDC0F817884CDD856UL, 15, true);  // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle
                Function.Call((Hash)0x9B2BD3773123EA2FUL, 15, false); // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION
                Function.Call((Hash)0xDC0F817884CDD856UL, 2, true);   // ENABLE_DISPATCH_SERVICE: DT_PoliceHelicopter
                Function.Call((Hash)0x9B2BD3773123EA2FUL, 2, false);  // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION
                _armyVehicleDispatchSuppressed = false;
                _nextFortZancudoDispatchRefreshUtc = now.AddMilliseconds(900);
                Log("Fort Zancudo Army and helicopter dispatch kept enabled for Newer Wanted / 911 traffic.");' @'
                // Block only Army vehicle response while the player is inside Fort Zancudo.
                // Police helicopter dispatch (service 2) remains untouched for Newer Wanted / 911.
                Function.Call((Hash)0xDC0F817884CDD856UL, 15, false); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle
                Function.Call((Hash)0x9B2BD3773123EA2FUL, 15, true);  // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION
                _armyVehicleDispatchSuppressed = true;
                _nextFortZancudoDispatchRefreshUtc = now.AddMilliseconds(900);
                Log("Fort Zancudo Army dispatch suppressed; police helicopter dispatch left unchanged.");
'@ 'Fort Zancudo Army dispatch suppression'

$core = Replace-Exact $core '                Function.Call((Hash)0xDC0F817884CDD856UL, 15, true); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle
                Function.Call((Hash)0xDC0F817884CDD856UL, 2, true);  // ENABLE_DISPATCH_SERVICE: DT_PoliceHelicopter
                Function.Call((Hash)0x9B2BD3773123EA2FUL, 15, false); // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION
                Function.Call((Hash)0x9B2BD3773123EA2FUL, 2, false);  // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION' @'
                Function.Call((Hash)0xDC0F817884CDD856UL, 15, true); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle
                Function.Call((Hash)0x9B2BD3773123EA2FUL, 15, false); // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION
'@ 'Fort Zancudo Army dispatch restore'

$core = Replace-Exact $core 'now < lastFleeTaskUtc.AddSeconds(5)' 'now < lastFleeTaskUtc.AddSeconds(1.5)' 'military aircraft flee interval'
$core = Replace-Exact $core '                    // Reissue at most every 5 seconds so the flight task can progress.' '                    // Reassert the escape mission if hostile aircraft AI replaces it; avoid a 5-second attack window.' 'military aircraft flee comment'

$oldDismiss = '_nextBodyguardDismissalUtc = DateTime.UtcNow.AddMilliseconds(200);'
$countDismiss = ([regex]::Matches($core, [regex]::Escape($oldDismiss))).Count
if ($countDismiss -ne 3) { throw "Expected 3 bodyguard dismissal throttle sites; found $countDismiss" }
$core = $core.Replace($oldDismiss, '_nextBodyguardDismissalUtc = DateTime.UtcNow.AddMilliseconds(650);')


Write-Text $corePath $core

$persistPath = 'src/Core/SettingsPersistence.cs'
$persist = Read-Text $persistPath
$start = $persist.IndexOf('        private static string ResolveWritableLogDirectory(')
$end = $persist.IndexOf('        private static bool CanWriteDirectory(', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'Could not locate log resolver' }
$resolver = @'
        private static string ResolveWritableLogDirectory(string scriptsDirectory, string dataDirectory)
        {
            return Path.Combine(scriptsDirectory, "AvernesSowtware_Logs");
        }

'@
$persist = $persist.Substring(0,$start) + $resolver + $persist.Substring($end)
$persist = Replace-Exact $persist '_settings.DriftMode = GetBool(values, "VEHICLE.DRIFTMODE", false);' ('_settings.DriftMode = GetBool(values, "VEHICLE.DRIFTMODE", false);' + $nl + '            _settings.VehicleStability = GetBool(values, "VEHICLE.STABILITY", false);') 'load stability'
$persist = Replace-Exact $persist '                lines.Add("DRIFTMODE=" + _settings.DriftMode.ToString().ToLowerInvariant());' ('                lines.Add("DRIFTMODE=" + _settings.DriftMode.ToString().ToLowerInvariant());' + $nl + '                lines.Add("STABILITY=" + _settings.VehicleStability.ToString().ToLowerInvariant());') 'save stability'
Write-Text $persistPath $persist

$vehiclePath = 'src/Managers/VehicleSystems.cs'
$vehicle = Read-Text $vehiclePath
$vehicle = Replace-Exact $vehicle ('            int handle = vehicle.Handle;' + $nl + '            if (_nitroVehicleHandle != 0 && _nitroVehicleHandle != handle) RestoreNitroSpeedLimit();') ('            int handle = vehicle.Handle;' + $nl + '            uint currentModel = Function.Call<uint>(Hash.GET_ENTITY_MODEL, handle);' + $nl + '            if (currentModel == GetHash("toreador"))' + $nl + '            {' + $nl + '                if (_nitroVehicleHandle == handle) RestoreNitroSpeedLimit();' + $nl + '                if (!_nitroTrailerWarningShown) Notify("Nitro is disabled for Toreador to prevent uncontrolled submarine-vehicle flips.");' + $nl + '                _nitroTrailerWarningShown = true;' + $nl + '                return;' + $nl + '            }' + $nl + '            if (_nitroVehicleHandle != 0 && _nitroVehicleHandle != handle) RestoreNitroSpeedLimit();') 'Toreador nitro'
$vehicle = Replace-Exact $vehicle '            bool nitroRequested = _settings.Nitro && IsControlPressed(21) && playerIsDriver;' ('            if (_settings.VehicleStability) ApplyVehicleStabilityAssist(handle);' + $nl + '            bool nitroRequested = _settings.Nitro && IsControlPressed(21) && playerIsDriver;') 'stability call'
$vehicle = Replace-Exact $vehicle '        // Preserve the original internal entry point for existing callers/reflection-based integrations.' @'
        private void ApplyVehicleStabilityAssist(int vehicleHandle)
        {
            if (vehicleHandle == 0 || !IsVehicleHandleUsable(vehicleHandle)) return;
            try
            {
                if (Function.Call<bool>((Hash)0x886E37EC497200B6UL, vehicleHandle)) return;
                Vector3 forward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, vehicleHandle);
                Vector3 velocity = Function.Call<Vector3>(Hash.GET_ENTITY_VELOCITY, vehicleHandle);
                float forwardLength = (float)Math.Sqrt(forward.X * forward.X + forward.Y * forward.Y);
                if (forwardLength < 0.001f) return;
                float fx = forward.X / forwardLength;
                float fy = forward.Y / forwardLength;
                float forwardSpeed = velocity.X * fx + velocity.Y * fy;
                if (Math.Abs(forwardSpeed) < 25.0f) return;
                float lateralX = velocity.X - fx * forwardSpeed;
                float lateralY = velocity.Y - fy * forwardSpeed;
                Function.Call(Hash.SET_ENTITY_VELOCITY, vehicleHandle,
                    fx * forwardSpeed + lateralX * 0.82f,
                    fy * forwardSpeed + lateralY * 0.82f,
                    velocity.Z);
            }
            catch (Exception ex) { LogException("ApplyVehicleStabilityAssist " + vehicleHandle, ex); }
        }

        // Preserve the original internal entry point for existing callers/reflection-based integrations.
'@ 'stability helper'
$vehicle = Replace-Exact $vehicle ('            Vector3 velocity = Function.Call<Vector3>(Hash.GET_ENTITY_VELOCITY, vehicleHandle);' + $nl + '            float forwardSpeed = velocity.X * forwardX + velocity.Y * forwardY + velocity.Z * forwardZ;') ('            Vector3 velocity = Function.Call<Vector3>(Hash.GET_ENTITY_VELOCITY, vehicleHandle);' + $nl + '            bool airborne = Function.Call<bool>((Hash)0x886E37EC497200B6UL, vehicleHandle);' + $nl + '            if (airborne) return;' + $nl + '            float forwardSpeed = velocity.X * forwardX + velocity.Y * forwardY + velocity.Z * forwardZ;') 'airborne nitro'
$vehicle = Replace-Exact $vehicle ('            int handle = vehicle.Handle;' + $nl + '            int supportedSlots = 0;') ('            int handle = vehicle.Handle;' + $nl + '            if (Function.Call<bool>((Hash)0x886E37EC497200B6UL, handle))' + $nl + '            {' + $nl + '                Notify("Land the vehicle before applying Max Tuning. No modifications were applied.");' + $nl + '                return;' + $nl + '            }' + $nl + '            int supportedSlots = 0;') 'airborne max tune'
$vehicle = Replace-Exact $vehicle 'Function.Call((Hash)0x81592BE4E4746D88UL, handle, type, color); // GET_VEHICLE_MOD_COLOR_2' 'Function.Call((Hash)0xA19435F193E081ACUL, handle, type, color); // GET_VEHICLE_COLOURS' 'secondary color native'
$vehicle = Replace-Exact $vehicle 'secondaryType = type.GetResult<int>();' 'secondaryType = 0;' 'secondary paint type'
Write-Text $vehiclePath $vehicle


$vehicle = Replace-Exact $vehicle '            Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicleHandle);
            for (int windowIndex = 0; windowIndex < 8; windowIndex++)
                Function.Call((Hash)0x772282EBEB95E682UL, vehicleHandle, windowIndex); // FIX_VEHICLE_WINDOW
            Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, vehicleHandle, false);' @'
            Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicleHandle);
            Function.Call(Hash.SET_VEHICLE_DIRT_LEVEL, vehicleHandle, 0.0f);
            Function.Call((Hash)0xE91F1B65F2B48D57UL, vehicleHandle); // REMOVE_DECALS_FROM_VEHICLE
            for (int windowIndex = 0; windowIndex < 8; windowIndex++)
                Function.Call((Hash)0x772282EBEB95E682UL, vehicleHandle, windowIndex); // FIX_VEHICLE_WINDOW
            Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, vehicleHandle, false);
'@ 'God Mode initial visual repair'

$vehicle = Replace-Exact $vehicle '                Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicleHandle);
                for (int windowIndex = 0; windowIndex < 8; windowIndex++)
                    Function.Call((Hash)0x772282EBEB95E682UL, vehicleHandle, windowIndex); // FIX_VEHICLE_WINDOW
                Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, vehicleHandle, false);' @'
                Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicleHandle);
                Function.Call(Hash.SET_VEHICLE_DIRT_LEVEL, vehicleHandle, 0.0f);
                Function.Call((Hash)0xE91F1B65F2B48D57UL, vehicleHandle); // REMOVE_DECALS_FROM_VEHICLE
                for (int windowIndex = 0; windowIndex < 8; windowIndex++)
                    Function.Call((Hash)0x772282EBEB95E682UL, vehicleHandle, windowIndex); // FIX_VEHICLE_WINDOW
                Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, vehicleHandle, false);
'@ 'God Mode recurring visual repair'


foreach ($relative in @('BUILD_RELEASE.bat','build/Build.bat')) {
    $text = Read-Text $relative
    Write-Text $relative ($text.Replace('1.4.6','1.4.7'))
}

$validatorPath = 'build/ReleaseValidator.cs'
$validator = Read-Text $validatorPath
$validator = Replace-Exact $validator 'ProductVersion = \"1.4.6\"' 'ProductVersion = \"1.4.7\"' 'validator version'
$oldLogStart = $validator.IndexOf('        if (File.ReadAllText(Path.Combine(root, "src/Core/SettingsPersistence.cs")).IndexOf("ResolveWritableLogDirectory"')
$oldLogEnd = $validator.IndexOf('        int repairStart = vehicleSource.IndexOf("private void RepairVehicleForGodMode"', $oldLogStart)
if ($oldLogStart -lt 0 -or $oldLogEnd -lt 0) { throw 'Could not locate legacy log validator assertion.' }
$newLogValidator = @'
        if (File.ReadAllText(Path.Combine(root, "src/Core/SettingsPersistence.cs")).IndexOf("ResolveWritableLogDirectory", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("\"session.log\"", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("\"error.log\"", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("\"crash.log\"", StringComparison.Ordinal) < 0)
            Error("1.4.7 fixed scripts log directory and session/error/crash logs are missing.");
'@
$validator = $validator.Substring(0,$oldLogStart) + $newLogValidator + $validator.Substring($oldLogEnd)
$oldStart = $validator.IndexOf('        if (mainSource.IndexOf("0x81592BE4E4746D88UL"')
$oldEnd = $validator.IndexOf('        if (persistenceSource.IndexOf("TryAtomicWriteFile"', $oldStart)
if ($oldStart -lt 0 -or $oldEnd -lt 0) { throw 'Could not locate old validator assertions.' }
$newChecks = @'
        string persistenceSource = File.ReadAllText(Path.Combine(root, "src/Core/SettingsPersistence.cs"));
        if (vehicleSource.IndexOf("0x81592BE4E4746D88UL", StringComparison.Ordinal) >= 0 ||
            vehicleSource.IndexOf("GET_VEHICLE_COLOURS", StringComparison.Ordinal) < 0)
            Error("1.4.7 Max Tuning must avoid the unsupported secondary mod-color native.");
        if (mainSource.IndexOf("AvernesSowtware error log initialized", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("AvernesSowtware crash log initialized", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("\"session.log\"", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("\"error.log\"", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("\"crash.log\"", StringComparison.Ordinal) < 0)
            Error("1.4.7 session/error/crash logs must be initialized in the required log directory.");
        if (mainSource.IndexOf("attempt < 3", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("fallbackDir", StringComparison.Ordinal) >= 0 ||
            persistenceSource.IndexOf("Path.Combine(scriptsDirectory, \"AvernesSowtware_Logs\")", StringComparison.Ordinal) < 0)
            Error("1.4.7 diagnostics must remain pinned to scripts/AvernesSowtware_Logs.");
        if (vehicleSource.IndexOf("GetHash(\"toreador\")", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("if (airborne) return", StringComparison.Ordinal) < 0 ||
            vehicleSource.IndexOf("ApplyVehicleStabilityAssist", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("WashCurrentVehicle", StringComparison.Ordinal) < 0)
            Error("1.4.7 vehicle safety, stability, or wash controls are incomplete.");
        if (vehicleSource.IndexOf("Land the vehicle before applying Max Tuning", StringComparison.Ordinal) < 0)
            Error("1.4.7 Max Tuning must reject airborne vehicles safely.");
        if (mainSource.IndexOf("AddMilliseconds(200)", StringComparison.Ordinal) < 0)
            Error("1.4.6 serialized bodyguard dismissal interval was not reduced.");

'@
$validator = $validator.Substring(0,$oldStart) + $newChecks + $validator.Substring($oldEnd)
Write-Text $validatorPath $validator

$readmePath = 'README_RU.md'
$readme = Read-Text $readmePath
$readme = $readme.Replace('AvernesSowtware 1.4.6 — GTA V Legacy Trainer','AvernesSowtware 1.4.7 — GTA V Legacy Trainer')
$readme = $readme.Replace('Версия 1.4.6 содержит адресные исправления по игровому отчёту 1.4.5.','Версия 1.4.7 содержит адресные исправления по игровому отчёту 1.4.6.')
$readme = $readme.Replace('## Изменения 1.4.6', '## Изменения 1.4.7' + $nl + $nl + '- Runtime-логи закреплены в scripts/AvernesSowtware_Logs: install.log, session.log, error.log, crash.log.' + $nl + '- Добавлена повторная попытка записи при временной блокировке.' + $nl + '- Max Tuning блокируется в воздухе; исключён неподдерживаемый native вторичного цвета.' + $nl + '- Добавлены отдельная мойка и переключатель High-Speed Stability.' + $nl + '- Nitro отключён для Toreador; импульс нитро не применяется в воздухе.' + $nl + $nl + 'Игровые тесты GTA V для 1.4.7 не выполнялись.' + $nl + $nl + '## Изменения 1.4.6')
$readme = $readme.Replace('## Установка 1.4.6','## Установка 1.4.7')
Write-Text $readmePath $readme
$startPath = 'START_HERE_RU.txt'
$start = Read-Text $startPath
Write-Text $startPath ($start.Replace('1.4.6','1.4.7'))

$changelog = @'
# AvernesSowtware 1.4.7

## Изменения
- Логи закреплены в scripts/AvernesSowtware_Logs: session.log, error.log, crash.log; install.log остаётся рядом.
- Запись логов повторяется при временной блокировке файла; перенаправление в AppData/Documents отключено.
- Убран вызов GET_VEHICLE_MOD_COLOR_2 из Max Tuning; вторичный цвет читается через GET_VEHICLE_COLOURS.
- Max Tuning отклоняется в воздухе.
- Nitro отключён для Toreador; импульс наземного нитро не применяется в воздухе.
- Добавлены отдельная мойка автомобиля и переключатель High-Speed Stability.

Игровые тесты GTA V для этой сборки не выполнялись.
'@
Write-Text 'docs/CHANGELOG_1_4_7_RU.md' $changelog
Write-Host 'Applied v1.4.7 source changes.'
