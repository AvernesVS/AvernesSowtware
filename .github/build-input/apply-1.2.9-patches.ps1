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
    $count = ([regex]::Matches($text, [regex]::Escape($Old))).Count
    if ($count -ne 1) { throw "$Label expected exactly one match in $RelativePath; found $count." }
    $text = $text.Replace($Old, $New)
    Write-Text $RelativePath $text
}
function Replace-All([string]$RelativePath, [string]$Old, [string]$New, [string]$Label) {
    $text = Read-Text $RelativePath
    if (-not $text.Contains($Old)) { throw "$Label was not found in $RelativePath." }
    $text = $text.Replace($Old, $New)
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
$oldRow = @'
                string marker = selected ? "> " : "  ";
                string suffix = item.Value == null ? (item.ChildPage == null ? String.Empty : "  >") : "  [" + item.Value() + "]";
                DrawMenuText(marker + item.Title + suffix, x + 0.010f * scale, y + 0.003f * scale,
                    0.205f * scale * fontScale,
'@
$newRow = @'
                string marker = selected ? "> " : "  ";
                string suffix = item.Value == null ? (item.ChildPage == null ? String.Empty : "  >") : "  [" + item.Value() + "]";
                string rowText = FitMenuRowText(marker, item.Title, suffix, width, scale, fontScale);
                DrawMenuText(rowText, x + 0.010f * scale, y + 0.003f * scale,
                    0.205f * scale * fontScale,
'@
Replace-One "src\YnixTrainerExtended.cs" $oldRow $newRow "Menu row width clipping"
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
$labelNeedle = [Environment]::NewLine + ":relocate_misplaced_dll"
$labelIndex = $install.LastIndexOf($labelNeedle, [StringComparison]::OrdinalIgnoreCase)
if ($labelIndex -lt 0) { throw "Installer saved slots helper insertion label not found." }
$install = $install.Insert($labelIndex + [Environment]::NewLine.Length, $helper)
Write-Text "Install_YnixTrainer.bat" $install

Replace-One "Install_YnixTrainer.bat" 'if not exist "%VEHICLES_DIR%\vehicles.json" (' ('for %%I in ("%VEHICLES_DIR%\saved_vehicles.json") do set "FINAL_SAVED_SLOTS_SIZE=%%~zI"' + [char]10 + 'if "%FINAL_SAVED_SLOTS_SIZE%"=="0" (' + [char]10 + '  set "FAIL_REASON=Saved vehicle slots file is empty after installation."' + [char]10 + '  goto :fail' + [char]10 + ')') + [char]10 + 'if not exist "%VEHICLES_DIR%\vehicles.json" (' "Saved vehicle slots size validation"
Replace-All "Install_YnixTrainer.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Installer visible branding/version"
Replace-All "Install_YnixTrainer.bat" 'pre_1_2_8' 'pre_1_2_9' "Installer backup release marker"
Replace-All "Uninstall_YnixTrainer.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Uninstaller visible branding/version"

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
Replace-One "START_HERE_RU.txt" 'AvernesSowtware / YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Start guide branding/version"

$changelog = @'
# AvernesSowtware 1.2.9 — журнал изменений

## Исправления
- Atlas теперь явно помечает 161 пункт как отдельный ручной чек-лист и не выдаёт его за процент прохождения текущего сохранения GTA V.
- Ограничена длина строк меню; длинные названия и значения больше не должны накладываться на соседние элементы.
- Навигация текстур одежды циклическая; исправлен переход при отсутствии текущего drawable. Страница одежды перемещена в Player, сохранённые комплекты и JSON не удаляются.
- Телохранители создаются как mission entities, остаются у скрипта под контролем, экипируются оружием, не становятся бессмертными и получают задачу защищать игрока во время боя.
- Для Обсерватории Галилео и горы Чильяд используется загрузка коллизии и высоты грунта; время ожидания для телепорта к waypoint увеличено.
- Never Wanted дополнительно ограничивает максимальный уровень розыска нулём и восстанавливает прежнее значение при выключении или остановке скрипта.
- Установщик отдельно копирует и проверяет saved_vehicles.json; существующие пользовательские слоты не перезаписываются.

## Совместимость
- Существующие пути пользовательских данных и JSON сохранены для миграции с 1.2.8.
- GTA V Legacy / ScriptHookVDotNet2; изменения требуют игрового тестирования.
'@
Write-Text "CHANGELOG_1_2_9_RU.md" $changelog

$testReport = @'
# AvernesSowtware 1.2.9 — отчёт сборки

Статус: исходники и сценарии сборки подготовлены; автоматическая сборка и статические проверки выполняются отдельным Windows CI заданием.

Подтверждённые на этапе подготовки изменения:
- новая метка продукта и версия 1.2.9;
- ограничения длины строк меню и отдельная маркировка ручного прогресса Atlas;
- изменение логики навигации одежды;
- коррекция сопровождения телохранителей;
- потоковый телепорт с запросами коллизии для двух проблемных локаций;
- ограничение wanted level в Never Wanted;
- отдельная проверка и копирование файла слотов транспорта.

Не подтверждено без запуска GTA V Legacy:
- фактическая синхронизация Atlas с игровым сохранением (в этой версии автоматический процент из save не заявляется);
- поведение Arwing, Tornado B и Toreador на установленной пользовательской сборке игры;
- работа пожарной техники без повреждённых стёкол;
- все игровые сценарии телохранителей, одежды, телепортов и Fort Zancudo.

Runtime-тест в GTA V не проводился этим отчётом. Он не заявляет о прохождении игровых проверок.
'@
Write-Text "TEST_REPORT_1_2_9_RU.md" $testReport

Write-Host "Applied 1.2.9 patches and updated release metadata."
)
if (-not $labelMatch.Success) { throw "Installer saved slots helper insertion label not found." }
$install = $install.Insert($labelMatch.Index, $helper)
Write-Text "Install_YnixTrainer.bat" $install
Replace-One "Install_YnixTrainer.bat" 'if not exist "%VEHICLES_DIR%\vehicles.json" (' ('for %%I in ("%VEHICLES_DIR%\saved_vehicles.json") do set "FINAL_SAVED_SLOTS_SIZE=%%~zI"' + [char]10 + 'if "%FINAL_SAVED_SLOTS_SIZE%"=="0" (' + [char]10 + '  set "FAIL_REASON=Saved vehicle slots file is empty after installation."' + [char]10 + '  goto :fail' + [char]10 + ')') + [char]10 + 'if not exist "%VEHICLES_DIR%\vehicles.json" (' "Saved vehicle slots size validation"
Replace-All "Install_YnixTrainer.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Installer visible branding/version"
Replace-All "Install_YnixTrainer.bat" 'pre_1_2_8' 'pre_1_2_9' "Installer backup release marker"
Replace-All "Uninstall_YnixTrainer.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Uninstaller visible branding/version"

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
Replace-One "START_HERE_RU.txt" 'AvernesSowtware / YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Start guide branding/version"

$changelog = @'
# AvernesSowtware 1.2.9 — журнал изменений

## Исправления
- Atlas теперь явно помечает 161 пункт как отдельный ручной чек-лист и не выдаёт его за процент прохождения текущего сохранения GTA V.
- Ограничена длина строк меню; длинные названия и значения больше не должны накладываться на соседние элементы.
- Навигация текстур одежды циклическая; исправлен переход при отсутствии текущего drawable. Страница одежды перемещена в Player, сохранённые комплекты и JSON не удаляются.
- Телохранители создаются как mission entities, остаются у скрипта под контролем, экипируются оружием, не становятся бессмертными и получают задачу защищать игрока во время боя.
- Для Обсерватории Галилео и горы Чильяд используется загрузка коллизии и высоты грунта; время ожидания для телепорта к waypoint увеличено.
- Never Wanted дополнительно ограничивает максимальный уровень розыска нулём и восстанавливает прежнее значение при выключении или остановке скрипта.
- Установщик отдельно копирует и проверяет saved_vehicles.json; существующие пользовательские слоты не перезаписываются.

## Совместимость
- Существующие пути пользовательских данных и JSON сохранены для миграции с 1.2.8.
- GTA V Legacy / ScriptHookVDotNet2; изменения требуют игрового тестирования.
'@
Write-Text "CHANGELOG_1_2_9_RU.md" $changelog

$testReport = @'
# AvernesSowtware 1.2.9 — отчёт сборки

Статус: исходники и сценарии сборки подготовлены; автоматическая сборка и статические проверки выполняются отдельным Windows CI заданием.

Подтверждённые на этапе подготовки изменения:
- новая метка продукта и версия 1.2.9;
- ограничения длины строк меню и отдельная маркировка ручного прогресса Atlas;
- изменение логики навигации одежды;
- коррекция сопровождения телохранителей;
- потоковый телепорт с запросами коллизии для двух проблемных локаций;
- ограничение wanted level в Never Wanted;
- отдельная проверка и копирование файла слотов транспорта.

Не подтверждено без запуска GTA V Legacy:
- фактическая синхронизация Atlas с игровым сохранением (в этой версии автоматический процент из save не заявляется);
- поведение Arwing, Tornado B и Toreador на установленной пользовательской сборке игры;
- работа пожарной техники без повреждённых стёкол;
- все игровые сценарии телохранителей, одежды, телепортов и Fort Zancudo.

Runtime-тест в GTA V не проводился этим отчётом. Он не заявляет о прохождении игровых проверок.
'@
Write-Text "TEST_REPORT_1_2_9_RU.md" $testReport

Write-Host "Applied 1.2.9 patches and updated release metadata."
)
if (-not $labelMatch.Success) { throw "Installer saved slots helper insertion label not found." }
$install = $install.Insert($labelMatch.Index, $helper + [Environment]::NewLine)
Write-Text "Install_YnixTrainer.bat" $install
Replace-One "Install_YnixTrainer.bat" 'if not exist "%VEHICLES_DIR%\vehicles.json" (' ('for %%I in ("%VEHICLES_DIR%\saved_vehicles.json") do set "FINAL_SAVED_SLOTS_SIZE=%%~zI"' + [char]10 + 'if "%FINAL_SAVED_SLOTS_SIZE%"=="0" (' + [char]10 + '  set "FAIL_REASON=Saved vehicle slots file is empty after installation."' + [char]10 + '  goto :fail' + [char]10 + ')') + [char]10 + 'if not exist "%VEHICLES_DIR%\vehicles.json" (' "Saved vehicle slots size validation"
Replace-All "Install_YnixTrainer.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Installer visible branding/version"
Replace-All "Install_YnixTrainer.bat" 'pre_1_2_8' 'pre_1_2_9' "Installer backup release marker"
Replace-All "Uninstall_YnixTrainer.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Uninstaller visible branding/version"

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
Replace-One "START_HERE_RU.txt" 'AvernesSowtware / YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Start guide branding/version"

$changelog = @'
# AvernesSowtware 1.2.9 — журнал изменений

## Исправления
- Atlas теперь явно помечает 161 пункт как отдельный ручной чек-лист и не выдаёт его за процент прохождения текущего сохранения GTA V.
- Ограничена длина строк меню; длинные названия и значения больше не должны накладываться на соседние элементы.
- Навигация текстур одежды циклическая; исправлен переход при отсутствии текущего drawable. Страница одежды перемещена в Player, сохранённые комплекты и JSON не удаляются.
- Телохранители создаются как mission entities, остаются у скрипта под контролем, экипируются оружием, не становятся бессмертными и получают задачу защищать игрока во время боя.
- Для Обсерватории Галилео и горы Чильяд используется загрузка коллизии и высоты грунта; время ожидания для телепорта к waypoint увеличено.
- Never Wanted дополнительно ограничивает максимальный уровень розыска нулём и восстанавливает прежнее значение при выключении или остановке скрипта.
- Установщик отдельно копирует и проверяет saved_vehicles.json; существующие пользовательские слоты не перезаписываются.

## Совместимость
- Существующие пути пользовательских данных и JSON сохранены для миграции с 1.2.8.
- GTA V Legacy / ScriptHookVDotNet2; изменения требуют игрового тестирования.
'@
Write-Text "CHANGELOG_1_2_9_RU.md" $changelog

$testReport = @'
# AvernesSowtware 1.2.9 — отчёт сборки

Статус: исходники и сценарии сборки подготовлены; автоматическая сборка и статические проверки выполняются отдельным Windows CI заданием.

Подтверждённые на этапе подготовки изменения:
- новая метка продукта и версия 1.2.9;
- ограничения длины строк меню и отдельная маркировка ручного прогресса Atlas;
- изменение логики навигации одежды;
- коррекция сопровождения телохранителей;
- потоковый телепорт с запросами коллизии для двух проблемных локаций;
- ограничение wanted level в Never Wanted;
- отдельная проверка и копирование файла слотов транспорта.

Не подтверждено без запуска GTA V Legacy:
- фактическая синхронизация Atlas с игровым сохранением (в этой версии автоматический процент из save не заявляется);
- поведение Arwing, Tornado B и Toreador на установленной пользовательской сборке игры;
- работа пожарной техники без повреждённых стёкол;
- все игровые сценарии телохранителей, одежды, телепортов и Fort Zancudo.

Runtime-тест в GTA V не проводился этим отчётом. Он не заявляет о прохождении игровых проверок.
'@
Write-Text "TEST_REPORT_1_2_9_RU.md" $testReport

Write-Host "Applied 1.2.9 patches and updated release metadata."
)
if (-not $labelMatch.Success) { throw "Installer saved slots helper insertion label not found." }
$install = $install.Insert($labelMatch.Index, $helper)
Write-Text "Install_YnixTrainer.bat" $install
Replace-One "Install_YnixTrainer.bat" 'if not exist "%VEHICLES_DIR%\vehicles.json" (' ('for %%I in ("%VEHICLES_DIR%\saved_vehicles.json") do set "FINAL_SAVED_SLOTS_SIZE=%%~zI"' + [char]10 + 'if "%FINAL_SAVED_SLOTS_SIZE%"=="0" (' + [char]10 + '  set "FAIL_REASON=Saved vehicle slots file is empty after installation."' + [char]10 + '  goto :fail' + [char]10 + ')') + [char]10 + 'if not exist "%VEHICLES_DIR%\vehicles.json" (' "Saved vehicle slots size validation"
Replace-All "Install_YnixTrainer.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Installer visible branding/version"
Replace-All "Install_YnixTrainer.bat" 'pre_1_2_8' 'pre_1_2_9' "Installer backup release marker"
Replace-All "Uninstall_YnixTrainer.bat" 'YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Uninstaller visible branding/version"

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
Replace-One "START_HERE_RU.txt" 'AvernesSowtware / YnixTrainer Extended 1.2.8' 'AvernesSowtware 1.2.9' "Start guide branding/version"

$changelog = @'
# AvernesSowtware 1.2.9 — журнал изменений

## Исправления
- Atlas теперь явно помечает 161 пункт как отдельный ручной чек-лист и не выдаёт его за процент прохождения текущего сохранения GTA V.
- Ограничена длина строк меню; длинные названия и значения больше не должны накладываться на соседние элементы.
- Навигация текстур одежды циклическая; исправлен переход при отсутствии текущего drawable. Страница одежды перемещена в Player, сохранённые комплекты и JSON не удаляются.
- Телохранители создаются как mission entities, остаются у скрипта под контролем, экипируются оружием, не становятся бессмертными и получают задачу защищать игрока во время боя.
- Для Обсерватории Галилео и горы Чильяд используется загрузка коллизии и высоты грунта; время ожидания для телепорта к waypoint увеличено.
- Never Wanted дополнительно ограничивает максимальный уровень розыска нулём и восстанавливает прежнее значение при выключении или остановке скрипта.
- Установщик отдельно копирует и проверяет saved_vehicles.json; существующие пользовательские слоты не перезаписываются.

## Совместимость
- Существующие пути пользовательских данных и JSON сохранены для миграции с 1.2.8.
- GTA V Legacy / ScriptHookVDotNet2; изменения требуют игрового тестирования.
'@
Write-Text "CHANGELOG_1_2_9_RU.md" $changelog

$testReport = @'
# AvernesSowtware 1.2.9 — отчёт сборки

Статус: исходники и сценарии сборки подготовлены; автоматическая сборка и статические проверки выполняются отдельным Windows CI заданием.

Подтверждённые на этапе подготовки изменения:
- новая метка продукта и версия 1.2.9;
- ограничения длины строк меню и отдельная маркировка ручного прогресса Atlas;
- изменение логики навигации одежды;
- коррекция сопровождения телохранителей;
- потоковый телепорт с запросами коллизии для двух проблемных локаций;
- ограничение wanted level в Never Wanted;
- отдельная проверка и копирование файла слотов транспорта.

Не подтверждено без запуска GTA V Legacy:
- фактическая синхронизация Atlas с игровым сохранением (в этой версии автоматический процент из save не заявляется);
- поведение Arwing, Tornado B и Toreador на установленной пользовательской сборке игры;
- работа пожарной техники без повреждённых стёкол;
- все игровые сценарии телохранителей, одежды, телепортов и Fort Zancudo.

Runtime-тест в GTA V не проводился этим отчётом. Он не заявляет о прохождении игровых проверок.
'@
Write-Text "TEST_REPORT_1_2_9_RU.md" $testReport

Write-Host "Applied 1.2.9 patches and updated release metadata."
