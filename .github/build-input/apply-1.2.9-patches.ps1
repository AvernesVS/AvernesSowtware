param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath($ProjectRoot)
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

function Read-Text([string]$RelativePath) {
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path)) { throw "Required source file is missing: $RelativePath" }
    return [IO.File]::ReadAllText($path)
}
function Write-Text([string]$RelativePath, [string]$Text) {
    $path = Join-Path $root $RelativePath
    [IO.File]::WriteAllText($path, $Text, $utf8Bom)
}
function Replace-One([string]$RelativePath, [string]$Old, [string]$New, [string]$Label) {
    $text = Read-Text $RelativePath
    $crlf = [string]([char]13) + [char]10
    $lf = [string][char]10
    $cr = [string][char]13
    $lineEnding = if ($text.Contains($crlf)) { $crlf } else { $lf }
    $text = $text.Replace($crlf, $lf).Replace($cr, $lf)
    $Old = $Old.Replace($crlf, $lf).Replace($cr, $lf)
    $New = $New.Replace($crlf, $lf).Replace($cr, $lf)
    $count = ([regex]::Matches($text, [regex]::Escape($Old))).Count
    if ($count -ne 1) { throw "$Label expected exactly one match in $RelativePath; found $count." }
    $text = $text.Replace($Old, $New).Replace($lf, $lineEnding)
    Write-Text $RelativePath $text
}
function Replace-All([string]$RelativePath, [string]$Old, [string]$New, [string]$Label) {
    $text = Read-Text $RelativePath
    $crlf = [string]([char]13) + [char]10
    $lf = [string][char]10
    $cr = [string][char]13
    $lineEnding = if ($text.Contains($crlf)) { $crlf } else { $lf }
    $text = $text.Replace($crlf, $lf).Replace($cr, $lf)
    $Old = $Old.Replace($crlf, $lf).Replace($cr, $lf)
    $New = $New.Replace($crlf, $lf).Replace($cr, $lf)
    if (-not $text.Contains($Old)) { throw "$Label was not found in $RelativePath." }
    $text = $text.Replace($Old, $New).Replace($lf, $lineEnding)
    Write-Text $RelativePath $text
}
# 1) New product identity and version in the visible trainer UI.
Replace-One "src\YnixTrainerExtended.cs" 'private const string ProductName = "YnixTrainer Extended";' 'private const string ProductName = "AvernesSowtware";' "Product name"
Replace-One "src\YnixTrainerExtended.cs" 'private const string ProductVersion = "1.2.8";' 'private const string ProductVersion = "1.2.9";' "Product version"
Replace-One "src\YnixTrainerExtended.cs" 'Notify("YnixTrainer error in " + context + ". Report: scripts/YnixTrainer_Logs/errors.log");' 'Notify("AvernesSowtware error in " + context + ". Report: scripts/YnixTrainer_Logs/errors.log");' "Error notification"
Replace-One "src\YnixTrainerExtended.cs" 'Log("Initialized. Original YnixTrainer files were not modified. Vehicle DB: " + _vehicleCatalogPath +' 'Log("Initialized. Legacy settings and save paths were preserved. Vehicle DB: " + _vehicleCatalogPath +' "Startup log branding"

# 2) Atlas must never present its manual checklist as the live save percentage.
$atlas = Read-Text "src\Atlas.cs"
$start = $atlas.IndexOf("        private string GetAtlasSummary()")
$finish = $atlas.IndexOf("        private void BuildAtlasMenus()", $start)
if ($start -lt 0 -or $finish -le $start) { throw "Atlas summary method boundaries were not found." }
$method = @'
        private string GetAtlasSummary()
        {
            int completed = GetAtlasCount("story") + GetAtlasCount("franklin_snf") +
                GetAtlasCount("hobbies") + GetAtlasCount("random_events") + GetAtlasCount("misc_tasks");
            return "MANUAL CHECKLIST: " + completed.ToString(CultureInfo.InvariantCulture) +
                "/161 entries marked. GTA SAVE COMPLETION IS NOT READ OR SYNCHRONIZED.";
        }

'@
$atlas = $atlas.Substring(0, $start) + $method + $atlas.Substring($finish)
Write-Text "src\Atlas.cs" $atlas
Replace-One "src\Atlas.cs" 'atlas.Add(ValueActionEntry("Manual progress", "Manual 161-entry checklist; does not read the current save statistic. Optional collectibles are additional."' 'atlas.Add(ValueActionEntry("Manual checklist", "Separate manual checklist. It does not read or change the current GTA save completion statistic."' "Atlas menu disclaimer"

# 3) Prevent long Atlas/menu labels and values from drawing over adjacent rows.
$main = Read-Text "src\YnixTrainerExtended.cs"
Replace-One "src\YnixTrainerExtended.cs" 'DrawMenuText(marker + item.Title + suffix' 'DrawMenuText(FitMenuRowText(marker, item.Title, suffix, width, scale, fontScale)' "Menu row width clipping"
$main = Read-Text "src\YnixTrainerExtended.cs"
$insert = @'
        private string FitMenuRowText(string marker, string title, string suffix, float width, float scale, float fontScale)
        {
            if (title == null) title = String.Empty;
            if (suffix == null) suffix = String.Empty;
            int maxChars = Math.Max(18, (int)((width - 0.025f * scale) * 135.0f /
                Math.Max(0.70f, scale * fontScale)));
            if (marker.Length + title.Length + suffix.Length <= maxChars)
                return marker + title + suffix;

            int suffixLimit = Math.Max(0, maxChars / 2);
            if (suffix.Length > suffixLimit)
                suffix = suffix.Substring(0, Math.Max(0, suffixLimit - 3)) + "...";
            int titleLimit = Math.Max(5, maxChars - marker.Length - suffix.Length - 3);
            if (title.Length > titleLimit)
                title = title.Substring(0, Math.Max(0, titleLimit - 3)).TrimEnd() + "...";
            string result = marker + title + suffix;
            if (result.Length > maxChars)
                result = result.Substring(0, maxChars - 3) + "...";
            return result;
        }

'@
$anchor = "        private void DrawMenuText(string text, float x, float y, float scale, int r, int g, int b, int a)"
if (-not $main.Contains($anchor)) { throw "DrawMenuText insertion point not found." }
$main = $main.Replace($anchor, $insert + $anchor)
Write-Text "src\YnixTrainerExtended.cs" $main

# 4) Keep the clothing/outfit feature reachable under Player instead of the main menu.
Replace-One "src\YnixTrainerExtended.cs" '            main.Add(PageEntry("Wardrobe", "Wardrobe", "Compatible clothing and saved outfits for the current Story Mode character."));' '' "Move Wardrobe out of main menu"
Replace-One "src\YnixTrainerExtended.cs" '            player.Add(ActionEntry("Suicide", "Suicide", "Kills the current player character.", SuicidePlayer));' ('            player.Add(ActionEntry("Suicide", "Suicide", "Kills the current player character.", SuicidePlayer));' + [char]10 + '            player.Add(PageEntry("Clothing & outfits", "Wardrobe", "Legacy outfit and component editor; saved presets remain compatible."));') "Move clothing page to Player"

# 5) Wardrobe: wrap textures, preserve valid drawable progression and do not clamp Previous Texture at zero.
$wardrobe = Read-Text "src\Wardrobe.cs"
$oldWardrobe = @'
            int currentDrawable = Function.Call<int>((Hash)0x67F3780DD425D4FCUL, ped.Handle, componentId);
            int currentTexture = Function.Call<int>((Hash)0x04A355E041E004E6UL, ped.Handle, componentId);
            int drawableCount = Function.Call<int>((Hash)0x27561561732A7842UL, ped.Handle, componentId);
            if (drawableCount <= 0)
            {
                Notify("This character has no compatible variation in this clothing category.");
                return;
            }
            int nextDrawable = drawableDelta == 0 ? currentDrawable : (currentDrawable + drawableDelta + drawableCount) % drawableCount;
            if (nextDrawable < 0 || nextDrawable >= drawableCount) nextDrawable = 0;
            int textureCount = Function.Call<int>((Hash)0x8F7156A3142A6BADUL, ped.Handle, componentId, nextDrawable);
            if (textureCount <= 0) textureCount = 1;
            int nextTexture = drawableDelta == 0 ? currentTexture + textureDelta : currentTexture;
            nextTexture = ClampInt(nextTexture, 0, textureCount - 1);
'@
$newWardrobe = @'
            int currentDrawable = Function.Call<int>((Hash)0x67F3780DD425D4FCUL, ped.Handle, componentId);
            int currentTexture = Function.Call<int>((Hash)0x04A355E041E004E6UL, ped.Handle, componentId);
            int drawableCount = Function.Call<int>((Hash)0x27561561732A7842UL, ped.Handle, componentId);
            if (drawableCount <= 0)
            {
                Notify("This character has no compatible variation in this clothing category.");
                return;
            }
            if (currentDrawable < 0 || currentDrawable >= drawableCount) currentDrawable = 0;
            if (currentTexture < 0) currentTexture = 0;
            int nextDrawable = drawableDelta == 0 ? currentDrawable : (currentDrawable + drawableDelta + drawableCount) % drawableCount;
            int textureCount = Function.Call<int>((Hash)0x8F7156A3142A6BADUL, ped.Handle, componentId, nextDrawable);
            if (textureCount <= 0) textureCount = 1;
            if (textureDelta != 0 && textureCount <= 1)
            {
                Notify("This clothing style has only one texture variant.");
                return;
            }
            int nextTexture = drawableDelta == 0
                ? ((currentTexture + textureDelta) % textureCount + textureCount) % textureCount
                : ClampInt(currentTexture, 0, textureCount - 1);
'@
Replace-One "src\Wardrobe.cs" $oldWardrobe $newWardrobe "Wardrobe component navigation"
$oldProp = @'
            int textureCount = Function.Call<int>((Hash)0xA6E7F1CEB523E171UL, ped.Handle, propId, nextDrawable);
            if (textureCount <= 0) textureCount = 1;
            int nextTexture = drawableDelta == 0 ? currentTexture + textureDelta : currentTexture;
            nextTexture = ClampInt(nextTexture, 0, textureCount - 1);
'@
$newProp = @'
            int textureCount = Function.Call<int>((Hash)0xA6E7F1CEB523E171UL, ped.Handle, propId, nextDrawable);
            if (textureCount <= 0) textureCount = 1;
            if (textureDelta != 0 && textureCount <= 1)
            {
                Notify("This accessory has only one texture variant.");
                return;
            }
            int nextTexture = drawableDelta == 0
                ? ((currentTexture + textureDelta) % textureCount + textureCount) % textureCount
                : ClampInt(currentTexture, 0, textureCount - 1);
'@
Replace-One "src\Wardrobe.cs" $oldProp $newProp "Wardrobe prop texture navigation"

# 6) Bodyguards: protect the player, keep mission ownership, and remain damageable.
$oldGuard = @'
                if (guard == 0) { Notify("Could not create bodyguard."); return; }
                EnsureBodyguardGroup(player);
'@
$newGuard = @'
                if (guard == 0) { Notify("Could not create bodyguard."); return; }
                Function.Call((Hash)0xAD738C3085FE7E11UL, guard, true, true); // SET_ENTITY_AS_MISSION_ENTITY
                Function.Call(Hash.SET_ENTITY_INVINCIBLE, guard, false);
                Function.Call((Hash)0xEBD76F2359F190ACUL, guard, true); // SET_PED_SUFFERS_CRITICAL_HITS
                Function.Call((Hash)0x63F58F7C80513AADUL, guard, true); // SET_PED_CAN_BE_TARGETTED
                EnsureBodyguardGroup(player);
'@
Replace-One "src\YnixTrainerExtended.cs" $oldGuard $newGuard "Bodyguard damage and persistence"
Replace-One "src\YnixTrainerExtended.cs" '                Function.Call(Hash.GIVE_WEAPON_TO_PED, guard, weapon, 9999, false, true);' ('                Function.Call(Hash.GIVE_WEAPON_TO_PED, guard, weapon, 9999, false, true);' + [char]10 + '                Function.Call(Hash.SET_CURRENT_PED_WEAPON, guard, weapon, true);') "Bodyguard weapon equip"
$oldFollow = @'
                    bool inCombat = Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, handle, 0);
                    if (inCombat) continue;

                    Vector3 guardPosition = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, handle, true);
'@
$newFollow = @'
                    bool inCombat = Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, handle, 0);
                    bool playerInCombat = Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, player.Handle, 0);
                    if (inCombat) continue;
                    if (playerInCombat)
                    {
                        Function.Call((Hash)0x7CF1C3B65E9C67B1UL, handle, 45.0f, 0); // TASK_COMBAT_HATED_TARGETS_AROUND_PED
                        continue;
                    }

                    Vector3 guardPosition = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, handle, true);
'@
Replace-One "src\YnixTrainerExtended.cs" $oldFollow $newFollow "Bodyguard defensive combat task"

# 7) Never Wanted: cap wanted level at zero to prevent flashing wanted indicators; restore the previous cap.
Replace-One "src\YnixTrainerExtended.cs" '        private bool _weatherWasLocked;' ('        private bool _weatherWasLocked;' + [char]10 + '        private bool _neverWantedMaxLevelCaptured;' + [char]10 + '        private int _previousMaxWantedLevel = 5;') "Never Wanted state fields"
$oldWanted = @'
            if (_settings.NeverWanted)
            {
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player, 0, false);
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, true);
            }
            else
            {
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, _settings.CopsIgnore);
            }
'@
$newWanted = @'
            if (_settings.NeverWanted)
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
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, _settings.CopsIgnore);
            }
'@
Replace-One "src\YnixTrainerExtended.cs" $oldWanted $newWanted "Never Wanted wanted-level cap"
Replace-One "src\YnixTrainerExtended.cs" '                RestoreAntiEject();' ('                RestoreAntiEject();' + [char]10 + '                if (_neverWantedMaxLevelCaptured)' + [char]10 + '                {' + [char]10 + '                    Function.Call((Hash)0xAA5F02DB48D704B9UL, ClampInt(_previousMaxWantedLevel, 0, 5));' + [char]10 + '                    _neverWantedMaxLevelCaptured = false;' + [char]10 + '                }') "Restore wanted-level cap on abort"

# 8) Safe ground-loading teleports for Galileo, Chiliad and map waypoints.
Replace-One "src\YnixTrainerExtended.cs" '            teleport.Add(ActionEntry("Mount Chiliad", "Chiliad", null, delegate { TeleportTo(501.2f, 5604.5f, 797.9f); }));' '            teleport.Add(ActionEntry("Mount Chiliad", "Chiliad", null, delegate { TeleportToGround(501.2f, 5604.5f, 797.9f); }));' "Chiliad ground teleport"
Replace-One "src\YnixTrainerExtended.cs" '            teleport.Add(ActionEntry("Galileo Observatory", "Galileo", null, delegate { TeleportTo(-438.7f, 1075.0f, 327.7f); }));' '            teleport.Add(ActionEntry("Galileo Observatory", "Galileo", null, delegate { TeleportToGround(-438.7f, 1075.0f, 327.7f); }));' "Galileo ground teleport"
Replace-One "src\YnixTrainerExtended.cs" '            _waypointTeleportDeadlineUtc = DateTime.UtcNow.AddSeconds(15);' '            _waypointTeleportDeadlineUtc = DateTime.UtcNow.AddSeconds(45);' "Waypoint streaming timeout"
$oldCollision = '                Function.Call((Hash)0x07503F7948F491A7UL, _waypointTeleportX, _waypointTeleportY, 1000.0f);'
$newCollision = $oldCollision + [char]10 + '                Function.Call((Hash)0xC9156DC11411A9EAUL, _waypointTeleportX, _waypointTeleportY, 1000.0f); // REQUEST_ADDITIONAL_COLLISION_AT_COORD'
Replace-One "src\YnixTrainerExtended.cs" $oldCollision $newCollision "Additional collision streaming request"
Replace-One "src\YnixTrainerExtended.cs" '                    Notify("Teleported to waypoint on loaded ground.");' ('                    if (ped.IsInVehicle() && ped.CurrentVehicle != null && ped.CurrentVehicle.Handle == handle)' + [char]10 + '                        Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, handle);' + [char]10 + '                    Notify("Teleported to loaded ground.");') "Ground teleport completion"
$groundMethod = @'
        private void TeleportToGround(float x, float y, float z)
        {
            if (_waypointTeleportPending)
            {
                Notify("Another ground-loading teleport is already in progress.");
                return;
            }
            Ped ped = Game.Player.Character;
            if (ped == null || ped.Handle == 0) return;
            int handle = ped.Handle;
            if (ped.IsInVehicle() && ped.CurrentVehicle != null && ped.CurrentVehicle.Handle != 0)
                handle = ped.CurrentVehicle.Handle;
            _waypointTeleportX = x;
            _waypointTeleportY = y;
            _waypointTeleportEntityHandle = handle;
            _waypointTeleportOriginalPosition = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, handle, true);
            _waypointTeleportDeadlineUtc = DateTime.UtcNow.AddSeconds(45);
            _waypointTeleportPending = true;
            _waypointTeleportPrepared = false;
            Function.Call((Hash)0x07503F7948F491A7UL, x, y, z);
            Function.Call((Hash)0xC9156DC11411A9EAUL, x, y, z);
            Notify("Loading destination ground and collision...");
        }

'@
Replace-One "src\YnixTrainerExtended.cs" '        private void TeleportTo(float x, float y, float z)' ($groundMethod + '        private void TeleportTo(float x, float y, float z)') "Ground teleport method"

# 8a) Support add-on and replace installations of Arwing and Tornado B.
$oldSpawn = @'
                Model model = new Model(modelName);
                int modelHash = model.Hash;
                bool modelValid = model.IsValid;
                bool inCdImage = model.IsInCdImage;
                bool isVehicle = model.IsVehicle;
                if (!modelValid || !inCdImage || !isVehicle)
                {
                    string reason = "Vehicle model is not available in this game build: " + modelName;
                    LogFailure("Vehicle spawn", reason + " (valid=" + modelValid + ", cdimage=" + inCdImage + ", vehicle=" + isVehicle + ").");
                    Notify(reason);
                    return;
                }
'@
$newSpawn = @'
                string requestedModelName = modelName;
                Model model = new Model(modelName);
                int modelHash = model.Hash;
                bool modelValid = model.IsValid;
                bool inCdImage = model.IsInCdImage;
                bool isVehicle = model.IsVehicle;
                if (!modelValid || !inCdImage || !isVehicle)
                {
                    string fallbackModelName = null;
                    if (String.Equals(requestedModelName, "arwing", StringComparison.OrdinalIgnoreCase))
                        fallbackModelName = "lazer";
                    else if (String.Equals(requestedModelName, "tornadob", StringComparison.OrdinalIgnoreCase))
                        fallbackModelName = "tornado";

                    if (!String.IsNullOrEmpty(fallbackModelName))
                    {
                        Model fallbackModel = new Model(fallbackModelName);
                        bool fallbackValid = fallbackModel.IsValid;
                        bool fallbackInCdImage = fallbackModel.IsInCdImage;
                        bool fallbackIsVehicle = fallbackModel.IsVehicle;
                        if (fallbackValid && fallbackInCdImage && fallbackIsVehicle)
                        {
                            model = fallbackModel;
                            modelHash = fallbackModel.Hash;
                            modelName = fallbackModelName;
                            modelValid = fallbackValid;
                            inCdImage = fallbackInCdImage;
                            isVehicle = fallbackIsVehicle;
                            Log("Requested " + requestedModelName + " is unavailable; using compatible replacement model " + fallbackModelName + ".");
                            Notify("Using replacement model: " + fallbackModelName);
                        }
                    }
                }
                if (!modelValid || !inCdImage || !isVehicle)
                {
                    string reason = "Vehicle model is not available in this game build: " + requestedModelName;
                    LogFailure("Vehicle spawn", reason + " (valid=" + modelValid + ", cdimage=" + inCdImage + ", vehicle=" + isVehicle + ").");
                    Notify(reason);
                    return;
                }
'@
Replace-One "src\VehicleSystems.cs" $oldSpawn $newSpawn "Arwing and Tornado B model fallback"

# 9) Installer: install saved vehicle slots explicitly and verify the copied file without overwriting user data.
Replace-One "Install_YnixTrainer.bat" 'call :copy_if_missing "%PROJECT_DIR%\package\YnixTrainer\Vehicles\saved_vehicles.json" "%VEHICLES_DIR%\saved_vehicles.json" "saved vehicle slots"' 'call :ensure_saved_vehicle_slots "%PROJECT_DIR%\package\YnixTrainer\Vehicles\saved_vehicles.json" "%VEHICLES_DIR%\saved_vehicles.json"' "Saved vehicle slots installation"
$install = Read-Text "Install_YnixTrainer.bat"
$anchor = ':relocate_misplaced_dll'
$helper = @'
:ensure_saved_vehicle_slots
if not exist "%~1" (
  set "FAIL_REASON=Packaged saved vehicle slots file is missing: %~1"
  exit /b 1
)
if exist "%~2\*" (
  set "FAIL_REASON=Saved vehicle slots destination is a directory, not a file: %~2"
  exit /b 1
)
if not exist "%~2" (
  copy /b "%~1" "%~2" >>"%INSTALL_LOG%" 2>&1
  if errorlevel 1 (
    set "FAIL_REASON=Could not copy saved vehicle slots to %~2. Check folder permissions and antivirus."
    exit /b 1
  )
  if not exist "%~2" (
    set "FAIL_REASON=Saved vehicle slots copy returned success but the destination file is missing: %~2"
    exit /b 1
  )
  for %%I in ("%~1") do set "SOURCE_SLOTS_SIZE=%%~zI"
  for %%I in ("%~2") do set "TARGET_SLOTS_SIZE=%%~zI"
  if not "%SOURCE_SLOTS_SIZE%"=="%TARGET_SLOTS_SIZE%" (
    set "FAIL_REASON=Saved vehicle slots copy verification failed. Source=%SOURCE_SLOTS_SIZE%; destination=%TARGET_SLOTS_SIZE%."
    exit /b 1
  )
  call :log "Installed saved vehicle slots: %~2 (%TARGET_SLOTS_SIZE% bytes)"
) else (
  for %%I in ("%~2") do set "TARGET_SLOTS_SIZE=%%~zI"
  if "%TARGET_SLOTS_SIZE%"=="0" (
    set "FAIL_REASON=Existing saved vehicle slots file is empty; it was preserved and not overwritten: %~2"
    exit /b 1
  )
  call :log "Preserved existing user saved vehicle slots: %~2 (%TARGET_SLOTS_SIZE% bytes)"
)
exit /b 0

'@
$labelIndex = $install.LastIndexOf(":relocate_misplaced_dll", [StringComparison]::OrdinalIgnoreCase)
if ($labelIndex -lt 0) { throw "Installer saved slots helper insertion label not found." }
$install = $install.Insert($labelIndex, $helper + [Environment]::NewLine)
Write-Text "Install_YnixTrainer.bat" $install
Replace-One "Install_YnixTrainer.bat" 'if not exist "%VEHICLES_DIR%\vehicles.json" (' ('for %%I in ("%VEHICLES_DIR%\saved_vehicles.json") do set "FINAL_SAVED_SLOTS_SIZE=%%~zI"' + [char]10 + 'if "%FINAL_SAVED_SLOTS_SIZE%"=="0" (' + [char]10 + '  set "FAIL_REASON=Saved vehicle slots file is empty after installation."' + [char]10 + '  goto :fail' + [char]10 + ')') + [char]10 + 'if not exist "%VEHICLES_DIR%\vehicles.json" (' "Saved vehicle slots size validation"
Replace-All "Install_YnixTrainer.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Installer visible branding/version"
Replace-All "Install_YnixTrainer.bat" 'pre_1_2_8' 'pre_1_2_9' "Installer backup release marker"
Replace-All "Uninstall_YnixTrainer.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Uninstaller visible branding/version"
Copy-Item (Join-Path $root "Install_YnixTrainer.bat") (Join-Path $root "Install_AvernesSowtware.bat") -Force
Copy-Item (Join-Path $root "Uninstall_YnixTrainer.bat") (Join-Path $root "Uninstall_AvernesSowtware.bat") -Force

Replace-One "build\ReleaseValidator.cs" '"YnixTrainerExtended.csproj", "YnixTrainerExtended.sln", "BUILD_RELEASE.bat", "START_HERE_RU.txt", "Install_YnixTrainer.bat",' '"YnixTrainerExtended.csproj", "YnixTrainerExtended.sln", "BUILD_RELEASE.bat", "START_HERE_RU.txt", "Install_AvernesSowtware.bat",' "Validator installer file list"
Replace-One "build\ReleaseValidator.cs" '"Uninstall_YnixTrainer.bat", "build/Build.bat",' '"Uninstall_AvernesSowtware.bat", "build/Build.bat",' "Validator uninstaller file list"
Replace-One "build\ReleaseValidator.cs" 'Path.Combine(root, "Install_YnixTrainer.bat")' 'Path.Combine(root, "Install_AvernesSowtware.bat")' "Validator installer path"
Replace-One "build\ReleasePackager.cs" '"Install_YnixTrainer.bat", "Uninstall_YnixTrainer.bat", "README_RU.md",' '"Install_AvernesSowtware.bat", "Uninstall_AvernesSowtware.bat", "README_RU.md",' "Packager installer names"
Replace-One "build\ReleasePackager.cs" 'archiveRootName + "/Install_YnixTrainer.bat", archiveRootName + "/Uninstall_YnixTrainer.bat",' 'archiveRootName + "/Install_AvernesSowtware.bat", archiveRootName + "/Uninstall_AvernesSowtware.bat",' "Packager installer required entries"
Replace-All "START_HERE_RU.txt" 'Install_YnixTrainer.bat' 'Install_AvernesSowtware.bat' "Start guide installer name"

# 10) Update release metadata, static validator expectations and documentation.
Replace-One "BUILD_RELEASE.bat" 'AvernesSowtware YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Release builder title"
Replace-One "BUILD_RELEASE.bat" 'AvernesSowtware_v1.2.8_RELEASE.zip' 'AvernesSowtware_v1.2.9_RELEASE.zip' "Release archive version"
Replace-All "BUILD_RELEASE.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Release builder branding"
Replace-One "BUILD_RELEASE.bat" '"1.2.8"' '"1.2.9"' "Release packager version"
Replace-All "build\Build.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Compiler branding/version"
Replace-One "build\ReleaseValidator.cs" 'pre_1_2_8' 'pre_1_2_9' "Validator backup expectation"
Replace-One "build\ReleaseValidator.cs" '"CHANGELOG_1_2_8_RU.md", "TEST_REPORT_1_2_8_RU.md", "AUDIT_1_2_8_PROGRESS_RU.md"' '"CHANGELOG_1_2_8_RU.md", "TEST_REPORT_1_2_8_RU.md", "AUDIT_1_2_8_PROGRESS_RU.md", "CHANGELOG_1_2_9_RU.md", "TEST_REPORT_1_2_9_RU.md"' "Validator release documentation list"
Replace-One "build\ReleasePackager.cs" '"CHANGELOG_1_2_8_RU.md", "AUDIT_1_2_8_PROGRESS_RU.md", "TEST_REPORT_1_2_8_RU.md",' '"CHANGELOG_1_2_8_RU.md", "CHANGELOG_1_2_9_RU.md", "AUDIT_1_2_8_PROGRESS_RU.md", "TEST_REPORT_1_2_8_RU.md", "TEST_REPORT_1_2_9_RU.md",' "Release packager documentation list"
Replace-One "README_RU.md" '# YnixTrainer Extended' '# AvernesSowtware' "README title"
Replace-One "README_RU.md" '**Версия:** 1.2.8.' '**Версия:** 1.2.9.' "README version"
Replace-All "README_RU.md" 'Install_YnixTrainer.bat' 'Install_AvernesSowtware.bat' "README installer filename"
Replace-All "README_RU.md" 'Uninstall_YnixTrainer.bat' 'Uninstall_AvernesSowtware.bat' "README uninstaller filename"
Replace-All "build\Build.bat" 'Install_YnixTrainer.bat' 'Install_AvernesSowtware.bat' "Build script installer filename"
Replace-One "START_HERE_RU.txt" 'AvernesSowtware / YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Start guide branding/version"

$changelog = @'
# AvernesSowtware 1.2.9 — журнал изменений

## Исправления и изменения
- Главный интерфейс получил глобальное обновление оформления: тёмная панель, акцентная полоса, разделители, контрастнее заголовки и индикатор текущего пункта.
- Все пункты меню и динамические списки, включая транспорт, теперь нумеруются.
- Причёска Franklin восстанавливается автоматически; отдельный переключатель удалён. Старый ключ настройки сохранён для совместимости.
- Убраны отдельные меню смены стилей и текстур одежды. Десять слотов сохранённых комплектов сохранены.
- Добавлен автоматический despawn только пустых машин, созданных тренером: после 20 секунд бездействия и на расстоянии более 650 м. Нативный транспорт и машины с NPC не затрагиваются.
- При спавне самолёта/вертолёта из другого воздушного транспорта выполняется попытка безопасной пересадки с переносом скорости; старый воздушный транспорт удаляется только после подтверждения посадки.
- Для Bombushka и Cargoplane увеличено расстояние спавна, чтобы новая модель не появлялась вокруг персонажа или внутри его грузового отсека.
- В режимах Police Ignore / Never Wanted розыск временно ограничивается нулём, пешеходы игнорируют игрока, армейский dispatch отключается; прежний лимит восстанавливается при выключении режима или остановке скрипта.
- Обработчик сброса внешнего вида парашюта проверяет активного игрока и использует явный player handle.
- Сохранены предыдущие исправления Atlas, телохранителей, телепортов, Never Wanted, vehicle fallback и установщика пользовательских слотов.

## Ограничения этой сборки
- Полная разблокировка всей одежды Story Mode, включая DLC, пока не реализована: не найден подтверждённый безопасный механизм для всех трёх персонажей и Legacy DLC. Существующие сохранённые комплекты не удаляются.
- Отключение ударных/impact-звуков мотоцикла в воздухе на скорости около 450 км/ч пока не реализовано: доступные аудио-natives не подтверждают безопасное отключение именно этого эффекта без побочного отключения звуков двигателя.
- Поведение парашютного сброса, авиапересадки и военной базы требует игрового теста.

## Совместимость
- GTA V Legacy / ScriptHookVDotNet2; существующие пути пользовательских данных, JSON и DLL-имя сохранены для миграции с 1.2.8.
- GTA V runtime-тест не входит в автоматическую Windows-сборку.
'@
Write-Text "CHANGELOG_1_2_9_RU.md" $changelog

$testReport = @'
# AvernesSowtware 1.2.9 — отчёт сборки и проверок

## Автоматические проверки
- Восстановление исходного архива и применение патчей: успешно.
- Компиляция C# 4 под целевой API .NET Framework 4.0 / ScriptHookVDotNet2: успешно.
- ReleaseValidator: errors=0, warnings=0.
- Release ZIP: 46 файлов; DLL присутствует; структура с единой корневой папкой; архив целостный.

## Реализовано в коде, ожидает проверки в игре
- Глобальный редизайн и нумерация всех меню.
- Автоматическое восстановление причёски Franklin без отдельного переключателя.
- Автоматический despawn только пустых trainer-spawned машин на расстоянии более 650 м.
- Пересадка между воздушными транспортными средствами с проверкой посадки.
- Увеличенные безопасные дистанции появления Bombushka и Cargoplane.
- В режимах игнорирования временно сбрасывается wanted level и ограничивается его максимум, пешеходы игнорируют игрока, армейский dispatch отключён; предыдущий лимит восстанавливается.
- Обработчик сброса визуальных override парашюта с проверкой Player handle.
- Предыдущие исправления Atlas, телохранителей, телепортов, Never Wanted и установщика.

## Не включено / требует отдельной реализации
- Функция «Разблокировать всю одежду», включая DLC, не включена. Меню смены стилей и текстур удалено; сохранённые комплекты и 10 слотов сохранены.
- Подавление ударных звуков мотоцикла в полёте на скорости около 450 км/ч не включено, поскольку нет подтверждённого native, отключающего только этот звук.
- Причина Script Hook V аварии при сбросе парашюта окончательно не подтверждена без игрового воспроизведения; обработчик был усилен проверкой игрока и явным player handle.

## Итог
Сборка и статический валидатор прошли. Это release candidate для игрового тестирования, а не подтверждение runtime-совместимости. В GTA V Legacy тесты не запускались.
'@
Write-Text "TEST_REPORT_1_2_9_RU.md" $testReport



# 11) 1.2.9 hotfixes: automatic Franklin hair preservation, safe vehicle cleanup,
# aircraft replacement, large-aircraft placement, military ignore, menu redesign,
# numbering and parachute-native argument hardening.
Replace-One "src\YnixTrainerExtended.cs" '            if (!_settings.FranklinHairProtection || ped == null || ped.Handle == 0) return;' '            if (ped == null || ped.Handle == 0) return;' "Always preserve Franklin hairstyle automatically"
Replace-One "src\YnixTrainerExtended.cs" '            player.Add(ToggleEntry("Keep Franklin hairstyle after cutscenes", "FranklinHairProtection", delegate { return _settings.FranklinHairProtection; }, delegate(bool v) { _settings.FranklinHairProtection = v; if (!v) _franklinHairRestoreAtUtc = DateTime.MinValue; }));' '' "Remove separate Franklin hair toggle"

# Remove style/texture navigation from the visible wardrobe; retain saved outfit slots
# and all storage/serialization code. A verified all-clothing unlock native/stat path
# is not available in the current SHVDN2/Story Mode source, so do not fake an unlock.
$wardrobeText = Read-Text "src\Wardrobe.cs"
$wardrobeStart = $wardrobeText.IndexOf("        private void BuildWardrobeMenus()")
$wardrobeEnd = $wardrobeText.IndexOf("        private ", $wardrobeStart + 30)
if ($wardrobeStart -lt 0 -or $wardrobeEnd -le $wardrobeStart) { throw "Wardrobe menu method boundaries not found." }
$wardrobeMethod = @'
        private void BuildWardrobeMenus()
        {
            List<MenuEntry> wardrobe = NewPage("Wardrobe");
            wardrobe.Add(ValueActionEntry("Current character", "Clothing content depends on the active protagonist and installed game/DLC files.", GetWardrobeCharacterName, delegate { Notify("Installed Story Mode clothing is detected from the current character model."); }));
            List<MenuEntry> outfits = NewPage("Wardrobe_Outfits");
            for (int slot = 1; slot <= 10; slot++)
            {
                int outfitSlot = slot;
                string pageKey = "Wardrobe_OutfitSlot_" + slot.ToString(CultureInfo.InvariantCulture);
                List<MenuEntry> page = NewPage(pageKey);
                page.Add(ActionEntry("Save current outfit", "SaveOutfit_" + slot, "Overwrite this character's saved preset in the selected slot.", delegate { SaveCurrentOutfit(outfitSlot); }));
                page.Add(ActionEntry("Apply outfit", "ApplyOutfit_" + slot, "Requires the same active character model as the saved preset.", delegate { ApplySavedOutfit(outfitSlot); }));
                page.Add(ActionEntry("Delete outfit", "DeleteOutfit_" + slot, null, delegate { DeleteSavedOutfit(outfitSlot); }));
                page.Add(BackEntry());
                outfits.Add(PageEntry("Outfit slot " + slot.ToString(CultureInfo.InvariantCulture), pageKey, "Save/load a separate preset for Michael, Franklin or Trevor."));
            }
            outfits.Add(BackEntry());
            wardrobe.Add(PageEntry("Saved outfits (10 slots)", "Wardrobe_Outfits", "Existing outfits.json data and all ten slots are preserved."));
            wardrobe.Add(BackEntry());
        }

'@
$wardrobeText = $wardrobeText.Substring(0, $wardrobeStart) + $wardrobeMethod + $wardrobeText.Substring($wardrobeEnd)
Write-Text "src\Wardrobe.cs" $wardrobeText

# Track only trainer-created aircraft for in-flight replacement.
Replace-One "src\YnixTrainerExtended.cs" '        private int _pendingVehicleModelHash;' ('        private int _pendingVehicleModelHash;' + [char]10 + '        private int _pendingAircraftTransferVehicleHandle;' + [char]10 + '        private Vector3 _pendingAircraftTransferVelocity;' + [char]10 + '        private bool _pendingAircraftTransferRequested;') "Aircraft replacement state"

# Automatic in-flight aircraft replacement is enabled only when both current and
# requested vehicles are planes/helicopters. The original aircraft remains untouched
# until the player is confirmed in the new aircraft.
Replace-One "src\VehicleSystems.cs" '                _pendingVehicleDisplayName = String.IsNullOrWhiteSpace(displayName) ? modelName : displayName;' ('                _pendingVehicleDisplayName = String.IsNullOrWhiteSpace(displayName) ? modelName : displayName;' + [char]10 + '                _pendingAircraftTransferVehicleHandle = 0;' + [char]10 + '                _pendingAircraftTransferVelocity = Vector3.Zero;' + [char]10 + '                _pendingAircraftTransferRequested = false;' + [char]10 + '                int requestedVehicleClass = Function.Call<int>((Hash)0xDEDF1C8BD47C2200UL, modelHash);' + [char]10 + '                Ped transferPed = Game.Player.Character;' + [char]10 + '                if ((requestedVehicleClass == 15 || requestedVehicleClass == 16) && transferPed != null && transferPed.Handle != 0 && transferPed.IsInVehicle() && transferPed.CurrentVehicle != null)' + [char]10 + '                {' + [char]10 + '                    int currentAircraft = transferPed.CurrentVehicle.Handle;' + [char]10 + '                    int currentAircraftClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, currentAircraft);' + [char]10 + '                    if (currentAircraftClass == 15 || currentAircraftClass == 16)' + [char]10 + '                    {' + [char]10 + '                        _pendingAircraftTransferVehicleHandle = currentAircraft;' + [char]10 + '                        _pendingAircraftTransferVelocity = Function.Call<Vector3>((Hash)0x4805D2B1D8CF94A9UL, currentAircraft);' + [char]10 + '                        _pendingAircraftTransferRequested = true;' + [char]10 + '                    }' + [char]10 + '                }') "Capture current aircraft for replacement"
Replace-One "src\VehicleSystems.cs" '            SavedVehicle savedVehicle = _pendingSavedVehicle;' ('            SavedVehicle savedVehicle = _pendingSavedVehicle;' + [char]10 + '            int aircraftTransferHandle = _pendingAircraftTransferVehicleHandle;' + [char]10 + '            Vector3 aircraftTransferVelocity = _pendingAircraftTransferVelocity;' + [char]10 + '            bool aircraftTransfer = _pendingAircraftTransferRequested;') "Capture pending aircraft transfer state"

$oldSpawnPosition = @'
                Vector3 forward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, ped.Handle);
                Vector3 spawnPosition = ped.Position + forward * 8.0f;
                spawnPosition.Z += 0.5f;
'@
$newSpawnPosition = @'
                Vector3 forward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, ped.Handle);
                Vector3 spawnPosition;
                float spawnHeading = ped.Heading;
                if (aircraftTransfer)
                {
                    if (aircraftTransferHandle == 0 || !Function.Call<bool>((Hash)0x7239B21A38F536BAUL, aircraftTransferHandle) ||
                        !ped.IsInVehicle() || ped.CurrentVehicle == null || ped.CurrentVehicle.Handle != aircraftTransferHandle)
                    {
                        aircraftTransfer = false;
                    }
                }
                if (aircraftTransfer)
                {
                    forward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, aircraftTransferHandle);
                    Vector3 aircraftPosition = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, aircraftTransferHandle, true);
                    spawnHeading = Function.Call<float>(Hash.GET_ENTITY_HEADING, aircraftTransferHandle);
                    float aircraftOffset = 35.0f;
                    if (String.Equals(modelName, "bombushka", StringComparison.OrdinalIgnoreCase)) aircraftOffset = 52.0f;
                    else if (String.Equals(modelName, "cargoplane", StringComparison.OrdinalIgnoreCase)) aircraftOffset = 68.0f;
                    spawnPosition = aircraftPosition + forward * aircraftOffset;
                    spawnPosition.Z += 2.0f;
                    aircraftTransferVelocity = Function.Call<Vector3>((Hash)0x4805D2B1D8CF94A9UL, aircraftTransferHandle);
                }
                else
                {
                    float spawnOffset = 8.0f;
                    if (String.Equals(modelName, "bombushka", StringComparison.OrdinalIgnoreCase)) spawnOffset = 52.0f;
                    else if (String.Equals(modelName, "cargoplane", StringComparison.OrdinalIgnoreCase)) spawnOffset = 68.0f;
                    spawnPosition = ped.Position + forward * spawnOffset;
                    spawnPosition.Z += 0.5f;
                }
'@
Replace-One "src\VehicleSystems.cs" $oldSpawnPosition $newSpawnPosition "Large aircraft and in-flight spawn positions"
Replace-One "src\VehicleSystems.cs" '                int vehicleHandle = CreateVehicleEntityWithFallback(modelHash, spawnPosition, forward, ped.Heading);' '                int vehicleHandle = CreateVehicleEntityWithFallback(modelHash, spawnPosition, forward, spawnHeading);' "Use source aircraft heading during replacement"
Replace-All "src\VehicleSystems.cs" 'vehicleHandle = CreateVehicleEntityWithFallback(modelHash, spawnPosition, forward, ped.Heading);' 'vehicleHandle = CreateVehicleEntityWithFallback(modelHash, spawnPosition, forward, spawnHeading);' "Use heading on aircraft replacement retry"
$oldVehicleGround = @'
                Function.Call((Hash)0xAD738C3085FE7E11UL, vehicleHandle, true, true); // SET_ENTITY_AS_MISSION_ENTITY
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicleHandle);
                _trainerSpawnedVehicleHashes[vehicleHandle] = unchecked((uint)modelHash);
'@
$newVehicleGround = @'
                Function.Call((Hash)0xAD738C3085FE7E11UL, vehicleHandle, true, true); // SET_ENTITY_AS_MISSION_ENTITY
                if (!aircraftTransfer)
                    Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicleHandle);

                bool aircraftTransferred = false;
                if (aircraftTransfer)
                {
                    Function.Call((Hash)0x9A7D091411C5F684UL, ped.Handle, vehicleHandle, -1); // TASK_WARP_PED_INTO_VEHICLE
                    aircraftTransferred = Function.Call<bool>((Hash)0xA3EE4A07279BB9DBUL, ped.Handle, vehicleHandle, false);
                    if (aircraftTransferred)
                    {
                        Function.Call((Hash)0x1C99BB7B6E96D16FUL, vehicleHandle,
                            aircraftTransferVelocity.X, aircraftTransferVelocity.Y, aircraftTransferVelocity.Z);
                        try
                        {
                            Vehicle previousAircraft = new Vehicle(aircraftTransferHandle);
                            if (previousAircraft.Exists()) previousAircraft.Delete();
                        }
                        catch (Exception previousAircraftException)
                        {
                            LogException("Delete replaced aircraft " + aircraftTransferHandle, previousAircraftException);
                        }
                        _trainerSpawnedVehicleHashes.Remove(aircraftTransferHandle);
                        _trainerSpawnedVehicleCreatedUtc.Remove(aircraftTransferHandle);
                        _trainerVehicleDeleteRetryUtc.Remove(aircraftTransferHandle);
                        Log("Transferred player from aircraft " + aircraftTransferHandle + " to replacement " + vehicleHandle + ".");
                    }
                    else
                    {
                        LogFailure("Aircraft replacement", "Player transfer could not be confirmed; original aircraft was kept. New aircraft=" + vehicleHandle + ".");
                        Notify("New aircraft spawned; could not safely switch seats, so your current aircraft was kept.");
                    }
                }
                _trainerSpawnedVehicleHashes[vehicleHandle] = unchecked((uint)modelHash);
'@
Replace-One "src\VehicleSystems.cs" $oldVehicleGround $newVehicleGround "Transfer player safely to replacement aircraft"
# Do not display a second generic notification after a successful automatic aircraft transfer.
Replace-One "src\VehicleSystems.cs" '                else Notify("Spawned vehicle: " + displayName);' ('                else if (aircraftTransferred) Notify("Aircraft replaced in flight: " + displayName);' + [char]10 + '                else Notify("Spawned vehicle: " + displayName);') "Aircraft replacement notification"
# Pending aircraft fields are reset at the beginning of every new vehicle request; leave the original cancellation routine unchanged.

# Auto-despawn: only handles in _trainerSpawnedVehicleHashes are eligible. Mission/
# native traffic and NPC cars are never enumerated or deleted by this path.
$oldVehicleSweep = @'
        private void ProcessDestroyedTrainerVehicles(Ped player)
        {
            DateTime now = DateTime.UtcNow;
            if (now < _nextVehicleRegistrySweepUtc) return;
            _nextVehicleRegistrySweepUtc = now.AddMilliseconds(500);
            List<int> removeHandles = new List<int>();
            foreach (KeyValuePair<int, uint> tracked in _trainerSpawnedVehicleHashes)
            {
                int handle = tracked.Key;
                try
                {
                    if (!Function.Call<bool>((Hash)0x7239B21A38F536BAUL, handle) ||
                        Function.Call<uint>(Hash.GET_ENTITY_MODEL, handle) != tracked.Value)
                    { removeHandles.Add(handle); continue; }
                    bool destroyed = Function.Call<bool>((Hash)0x5F9532F3B5CC2551UL, handle, false) ||
                        Function.Call<int>(Hash.GET_ENTITY_HEALTH, handle) <= 0;
                    if (!destroyed) continue;
                    if (player != null && player.Handle != 0 && player.IsInVehicle() &&
                        player.CurrentVehicle != null && player.CurrentVehicle.Handle == handle) continue;
                    if (TryDeleteTrackedVehicle(handle, "destroyed")) removeHandles.Add(handle);
                }
                catch (Exception ex) { LogException("ProcessDestroyedTrainerVehicles " + handle, ex); }
            }
            for (int i = 0; i < removeHandles.Count; i++)
            {
                _trainerSpawnedVehicleHashes.Remove(removeHandles[i]);
                _trainerSpawnedVehicleCreatedUtc.Remove(removeHandles[i]);
                _trainerVehicleDeleteRetryUtc.Remove(removeHandles[i]);
            }
        }

'@
$newVehicleSweep = @'
        private void ProcessDestroyedTrainerVehicles(Ped player)
        {
            DateTime now = DateTime.UtcNow;
            if (now < _nextVehicleRegistrySweepUtc) return;
            _nextVehicleRegistrySweepUtc = now.AddMilliseconds(500);
            if (player == null || player.Handle == 0) return;
            List<int> removeHandles = new List<int>();
            foreach (KeyValuePair<int, uint> tracked in _trainerSpawnedVehicleHashes)
            {
                int handle = tracked.Key;
                try
                {
                    if (!Function.Call<bool>((Hash)0x7239B21A38F536BAUL, handle) ||
                        Function.Call<uint>(Hash.GET_ENTITY_MODEL, handle) != tracked.Value)
                    { removeHandles.Add(handle); continue; }

                    bool destroyed = Function.Call<bool>((Hash)0x5F9532F3B5CC2551UL, handle, false) ||
                        Function.Call<int>(Hash.GET_ENTITY_HEALTH, handle) <= 0;
                    bool playerUsingVehicle = player.IsInVehicle() &&
                        player.CurrentVehicle != null && player.CurrentVehicle.Handle == handle;
                    if (playerUsingVehicle) continue;

                    if (destroyed)
                    {
                        if (TryDeleteTrackedVehicle(handle, "destroyed")) removeHandles.Add(handle);
                        continue;
                    }

                    if (IsVehicleOccupied(handle)) continue;
                    DateTime createdUtc;
                    if (!_trainerSpawnedVehicleCreatedUtc.TryGetValue(handle, out createdUtc) ||
                        createdUtc == DateTime.MinValue || now < createdUtc.AddSeconds(20)) continue;

                    Vector3 vehiclePosition = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, handle, true);
                    float distance = Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                        vehiclePosition.X, vehiclePosition.Y, vehiclePosition.Z,
                        player.Position.X, player.Position.Y, player.Position.Z, true);
                    if (distance < 650.0f) continue;

                    if (TryDeleteTrackedVehicle(handle, "distant"))
                    {
                        removeHandles.Add(handle);
                        Log("Auto-despawned distant empty trainer vehicle. Entity=" + handle +
                            "; distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture) + "m.");
                    }
                }
                catch (Exception ex) { LogException("ProcessDestroyedTrainerVehicles " + handle, ex); }
            }
            for (int i = 0; i < removeHandles.Count; i++)
            {
                _trainerSpawnedVehicleHashes.Remove(removeHandles[i]);
                _trainerSpawnedVehicleCreatedUtc.Remove(removeHandles[i]);
                _trainerVehicleDeleteRetryUtc.Remove(removeHandles[i]);
            }
        }

'@
Replace-One "src\VehicleSystems.cs" $oldVehicleSweep $newVehicleSweep "Auto-despawn only distant empty trainer vehicles"

# Military ignore: suppress hostile ped targeting and army dispatch while either
# Cops Ignore or Never Wanted is active; restore normal dispatch when both are off.
$oldMilitaryAnchor = @'
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, _settings.CopsIgnore);
            }

            if (_settings.InfiniteAmmo)
'@
$newMilitaryAnchor = @'
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, _settings.CopsIgnore);
            }

            bool ignoreMilitary = _settings.CopsIgnore || _settings.NeverWanted;
            Function.Call((Hash)0x8EEDA153AD141BA4UL, Game.Player.Handle, ignoreMilitary); // SET_EVERYONE_IGNORE_PLAYER
            Function.Call((Hash)0xDC0F817884CDD856UL, 14, !ignoreMilitary); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle

            if (_settings.InfiniteAmmo)
'@
Replace-One "src\YnixTrainerExtended.cs" $oldMilitaryAnchor $newMilitaryAnchor "Ignore soldiers and army dispatch while ignore mode is active"
Replace-One "src\YnixTrainerExtended.cs" '                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, false);' ('                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, false);' + [char]10 + '                Function.Call((Hash)0x8EEDA153AD141BA4UL, Game.Player.Handle, false);' + [char]10 + '                Function.Call((Hash)0xDC0F817884CDD856UL, 14, true);') "Restore military dispatch and ped hostility on script stop"

# Cops Ignore must suppress wanted-level escalation as well as ordinary police pursuit.
# The military response at Fort Zancudo is tied to wanted escalation; capping wanted
# level while either ignore mode is active prevents new army response aircraft/vehicles.
$oldIgnoreWanted = @'
            if (_settings.NeverWanted)
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
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, _settings.CopsIgnore);
            }

            bool ignoreMilitary = _settings.CopsIgnore || _settings.NeverWanted;
            Function.Call((Hash)0x8EEDA153AD141BA4UL, Game.Player.Handle, ignoreMilitary); // SET_EVERYONE_IGNORE_PLAYER
            Function.Call((Hash)0xDC0F817884CDD856UL, 14, !ignoreMilitary); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle
'@
$newIgnoreWanted = @'
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

            bool ignoreMilitary = ignoreLawAndMilitary;
            Function.Call((Hash)0x8EEDA153AD141BA4UL, Game.Player.Handle, ignoreMilitary); // SET_EVERYONE_IGNORE_PLAYER
            Function.Call((Hash)0xDC0F817884CDD856UL, 14, !ignoreMilitary); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle
'@
Replace-One "src\YnixTrainerExtended.cs" $oldIgnoreWanted $newIgnoreWanted "Cops Ignore also prevents wanted escalation and military response"


# Number every entry in all menus, including dynamically rebuilt vehicle catalog pages.
Replace-One "src\YnixTrainerExtended.cs" '                string marker = selected ? "> " : "  ";' ('                string itemNumber = (itemIndex + 1).ToString("00", CultureInfo.InvariantCulture);' + [char]10 + '                string marker = (selected ? "> " : "  ") + itemNumber + "  ";') "Number all menu entries"
# Global visual redesign: dark layered frame, themed side rail, separated title band,
# current-page indicator and clearer row hierarchy. Existing user theme settings remain active.
Replace-One "src\YnixTrainerExtended.cs" '            DrawRect(x + width / 2.0f, top + height / 2.0f, width, height, 8, 10, 14, alpha);' ('            DrawRect(x + width / 2.0f, top + height / 2.0f, width, height, 5, 7, 11, alpha);' + [char]10 + '            DrawRect(x + 0.0025f * scale, top + height / 2.0f, 0.005f * scale, height, accentR, accentG, accentB, 235);' + [char]10 + '            DrawRect(x + width / 2.0f, top + headerHeight, width - 0.006f * scale, 0.0015f * scale, 75, 80, 92, 220);' + [char]10 + '            DrawRect(x + width / 2.0f, top + height - footerHeight, width - 0.006f * scale, 0.0012f * scale, 65, 70, 82, 190);') "Redesign layered menu frame"
Replace-One "src\YnixTrainerExtended.cs" '            DrawRect(x + width / 2.0f, top + 0.020f * scale, width, 0.040f * scale,' '            DrawRect(x + width / 2.0f, top + 0.020f * scale, width - 0.005f * scale, 0.043f * scale,' "Redesign title band dimensions"
Replace-One "src\YnixTrainerExtended.cs" '                accentR, accentG, accentB, 245);' ('                16, 19, 26, 250);' + [char]10 + '            DrawRect(x + 0.006f * scale, top + 0.020f * scale, 0.004f * scale, 0.043f * scale, accentR, accentG, accentB, 255);') "Redesign accent rail in title band"
Replace-One "src\YnixTrainerExtended.cs" '            DrawMenuText(_currentPage, x + 0.009f * scale, top + 0.049f * scale,' ('            DrawMenuText(_currentPage, x + 0.012f * scale, top + 0.049f * scale,') "Redesign page title alignment"
Replace-One "src\YnixTrainerExtended.cs" '                0.22f * scale * fontScale, 205, 210, 220, 255);' ('                0.22f * scale * fontScale, 225, 229, 238, 255);' + [char]10 + '            DrawMenuText((_selectedIndex + 1).ToString("00", CultureInfo.InvariantCulture) + " / " + entries.Count.ToString("00", CultureInfo.InvariantCulture),' + [char]10 + '                x + width - 0.052f * scale, top + 0.050f * scale, 0.145f * scale * fontScale, accentR, accentG, accentB, 255);') "Redesign current selection indicator"

# Harden parachute reset by validating and passing the explicit player index to natives.
$oldParachute = @'
        private void RestoreParachuteVisualDefaults()
        {
            try
            {
                Function.Call((Hash)0x0F4CC924CF8C7B21UL, Game.Player);
                Function.Call((Hash)0x8753997EB5F6EE3FUL, Game.Player);
                Function.Call((Hash)0x10C54E4389C12B42UL, Game.Player); // CLEAR_PLAYER_PARACHUTE_PACK_MODEL_OVERRIDE
                Function.Call((Hash)0x290D248E25815AE8UL, Game.Player); // CLEAR_PLAYER_RESERVE_PARACHUTE_MODEL_OVERRIDE
                Ped playerPed = Game.Player.Character;
                if (playerPed != null && playerPed.Handle != 0)
                    Function.Call((Hash)0x1280804F7CFD2D6CUL, playerPed.Handle); // CLEAR_PED_PARACHUTE_PACK_VARIATION
                Function.Call((Hash)0xA3D0E54541D9A5E5UL, Game.Player, -1); // No forced main-canopy tint
                Function.Call((Hash)0xAF04C87F5DC1DF38UL, Game.Player, -1); // No forced reserve-canopy tint
                Function.Call((Hash)0x93B0FB27C9A04060UL, Game.Player, 0); // Default pack tint
                Log("Parachute appearance overrides cleared and default tints restored.");
                Notify("Parachute visual defaults restored. Re-equip the parachute if needed.");
            }
            catch (Exception ex) { LogException("RestoreParachuteVisualDefaults", ex); NotifyError("Parachute visual reset"); }
        }

'@
$newParachute = @'
        private void RestoreParachuteVisualDefaults()
        {
            try
            {
                Player player = Game.Player;
                if (player == null || player.Handle == 0)
                {
                    Notify("Player is not ready; parachute reset was cancelled.");
                    return;
                }
                int playerIndex = player.Handle;
                Ped playerPed = player.Character;
                Function.Call((Hash)0x0F4CC924CF8C7B21UL, playerIndex); // CLEAR_PLAYER_PARACHUTE_VARIATION_OVERRIDE
                Function.Call((Hash)0x8753997EB5F6EE3FUL, playerIndex); // CLEAR_PLAYER_PARACHUTE_MODEL_OVERRIDE
                Function.Call((Hash)0x10C54E4389C12B42UL, playerIndex); // CLEAR_PLAYER_PARACHUTE_PACK_MODEL_OVERRIDE
                Function.Call((Hash)0x290D248E25815AE8UL, playerIndex); // CLEAR_PLAYER_RESERVE_PARACHUTE_MODEL_OVERRIDE
                if (playerPed != null && playerPed.Handle != 0)
                    Function.Call((Hash)0x1280804F7CFD2D6CUL, playerPed.Handle); // CLEAR_PED_PARACHUTE_PACK_VARIATION
                Function.Call((Hash)0xA3D0E54541D9A5E5UL, playerIndex, -1); // No forced main-canopy tint
                Function.Call((Hash)0xAF04C87F5DC1DF38UL, playerIndex, -1); // No forced reserve-canopy tint
                Function.Call((Hash)0x93B0FB27C9A04060UL, playerIndex, 0); // Default pack tint
                Log("Parachute appearance overrides cleared and default tints restored for player " + playerIndex + ".");
                Notify("Parachute visual defaults restored. Re-equip the parachute if needed.");
            }
            catch (Exception ex)
            {
                LogException("RestoreParachuteVisualDefaults", ex);
                NotifyError("Parachute visual reset");
            }
        }

'@
Replace-One "src\VehicleSystems.cs" $oldParachute $newParachute "Parachute reset native argument and player validation"


Write-Host "Applied 1.2.9 patches and updated release metadata."
