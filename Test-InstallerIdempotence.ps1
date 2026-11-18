param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$installer = Join-Path $root 'Install_AvernesSowtware.bat'
$dll = Join-Path $root 'dist\AvernesSowtware.dll'
if (-not (Test-Path -LiteralPath $installer)) { throw 'Installer batch file is missing.' }
if (-not (Test-Path -LiteralPath $dll)) { throw 'Release DLL is missing before installer test.' }
$fakeGame = Join-Path $env:RUNNER_TEMP ('AvernesSowtware-Installer-Test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fakeGame -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $fakeGame 'ScriptHookVDotNet2.dll'), 'CI reference placeholder')
$scriptsSeed = Join-Path $fakeGame 'scripts'
$legacyRootSeed = Join-Path $scriptsSeed 'YnixTrainer'
New-Item -ItemType Directory -Path (Join-Path $legacyRootSeed 'Vehicles') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $legacyRootSeed 'Profiles') -Force | Out-Null
$legacySettingsText = "; legacy folder settings`n[PLAYER]`nGODMODE=false`n[LEGACY_TEST]`nSource=YnixTrainerFolder`n"
$rootLegacySettingsText = "; root legacy settings`n[PLAYER]`nGODMODE=true`n[LEGACY_TEST]`nSource=ScriptsRoot`n"
[IO.File]::WriteAllText((Join-Path $legacyRootSeed 'YnixTrainerExtended.ini'), $legacySettingsText, [Text.Encoding]::UTF8)
[IO.File]::WriteAllText((Join-Path $legacyRootSeed 'Config.ini'), "[LEGACY]`nConfigPreserved=true`n", [Text.Encoding]::UTF8)
[IO.File]::WriteAllText((Join-Path $legacyRootSeed 'Vehicles\saved_vehicles.json'), '[{"slot":3,"model":"legacy_saved_car"}]', [Text.Encoding]::UTF8)
[IO.File]::WriteAllText((Join-Path $legacyRootSeed 'Profiles\custom_profile.dat'), 'legacy-profile-preserve-me', [Text.Encoding]::UTF8)
[IO.File]::WriteAllText((Join-Path $scriptsSeed 'YnixTrainerExtended.ini'), $rootLegacySettingsText, [Text.Encoding]::UTF8)
[IO.File]::WriteAllText((Join-Path $scriptsSeed 'YnixTrainerExtended.dll'), 'legacy-dll-placeholder', [Text.Encoding]::ASCII)
$expectedFolderSettingsHash = (Get-FileHash -LiteralPath (Join-Path $legacyRootSeed 'YnixTrainerExtended.ini') -Algorithm SHA256).Hash
$expectedRootSettingsHash = (Get-FileHash -LiteralPath (Join-Path $scriptsSeed 'YnixTrainerExtended.ini') -Algorithm SHA256).Hash
$expectedProfileHash = (Get-FileHash -LiteralPath (Join-Path $legacyRootSeed 'Profiles\custom_profile.dat') -Algorithm SHA256).Hash
try {
    & $installer $fakeGame --no-pause
    if ($LASTEXITCODE -ne 0) { throw "First installer execution failed with exit code $LASTEXITCODE." }

    $scripts = Join-Path $fakeGame 'scripts'
    $data = Join-Path $scripts 'AvernesSowtware'
    $vehicles = Join-Path $data 'Vehicles'
    $backups = Join-Path $scripts 'AvernesSowtware_Backups'
    $settings = Join-Path $data 'AvernesSowtware.ini'
    $saved = Join-Path $vehicles 'saved_vehicles.json'
    $hotkeys = Join-Path $data 'hotkeys.json'
    $db = Join-Path $vehicles 'vehicles.json'
    $builtinDb = Join-Path $vehicles 'vehicles_builtin.json'
    $legacyProfile = Join-Path $data 'Profiles\custom_profile.dat'
    $legacyConflict = Join-Path $data 'LegacyMigration\Conflicts\scripts\YnixTrainerExtended.ini'
    $legacyDllBackup = @(Get-ChildItem -LiteralPath $backups -File -Filter 'YnixTrainerExtended.dll.legacy_*.bak' -ErrorAction SilentlyContinue | Select-Object -First 1)
    foreach ($path in @($settings,$saved,$hotkeys,$db,(Join-Path $scripts 'AvernesSowtware.dll'),$legacyProfile,$legacyConflict)) {
        if (-not (Test-Path -LiteralPath $path)) { throw "First install did not create required migrated/installed file: $path" }
    }
    if ((Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash -ne $expectedFolderSettingsHash) { throw 'Legacy folder settings were not migrated byte-for-byte.' }
    $expectedDefaultHotkeysHash = (Get-FileHash -LiteralPath (Join-Path $root 'package\AvernesSowtware\hotkeys.json') -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $hotkeys -Algorithm SHA256).Hash -ne $expectedDefaultHotkeysHash) { throw 'Default hotkeys.json was not installed byte-for-byte on first install.' }
    if ((Get-FileHash -LiteralPath $legacyConflict -Algorithm SHA256).Hash -ne $expectedRootSettingsHash) { throw 'Conflicting root legacy settings were not preserved in the conflict archive.' }
    if ((Get-FileHash -LiteralPath $legacyProfile -Algorithm SHA256).Hash -ne $expectedProfileHash) { throw 'Nested legacy profile failed SHA-256 verification.' }
    if (Test-Path -LiteralPath $legacyRootSeed) { throw 'Old YnixTrainer folder was not removed after verified migration.' }
    if (Test-Path -LiteralPath (Join-Path $scripts 'YnixTrainerExtended.ini')) { throw 'Old root YnixTrainerExtended.ini remains after migration.' }
    if (Test-Path -LiteralPath (Join-Path $scripts 'YnixTrainerExtended.dll')) { throw 'Old YnixTrainerExtended.dll remains after migration.' }
    if ($legacyDllBackup.Count -ne 1) { throw 'Legacy YnixTrainer DLL was not preserved in backups before deletion.' }
    foreach ($critical in @($settings,$db,$builtinDb)) {
        if (-not (Test-Path -LiteralPath ($critical + '.sha256')) -or -not (Test-Path -LiteralPath ($critical + '.bak'))) { throw "Critical file backup/checksum missing: $critical" }
    }
    Set-Content -LiteralPath $settings -Value "[USER_TEST]`nPreserveMe=true" -Encoding UTF8
    Set-Content -LiteralPath $saved -Value '[{"slot":7,"model":"ci_saved_vehicle"}]' -Encoding UTF8
    Set-Content -LiteralPath $hotkeys -Value '{"schema_version":1,"bindings":{"MenuToggle":"F10"}}' -Encoding UTF8
    $expectedHotkeysHash = (Get-FileHash -LiteralPath $hotkeys -Algorithm SHA256).Hash
    $duplicate = Join-Path $vehicles 'test_duplicate (2).json'
    Set-Content -LiteralPath $duplicate -Value '[]' -Encoding UTF8
    $customCatalog = '[{"model":"user_custom_model_keep","name":"User custom model","category":"Custom"}]'
    [IO.File]::WriteAllText($db, $customCatalog, [Text.Encoding]::UTF8)
    $expectedBuiltinDbHash = (Get-FileHash -LiteralPath (Join-Path $root 'package\AvernesSowtware\Vehicles\vehicles.json') -Algorithm SHA256).Hash

    & $installer $fakeGame --no-pause
    if ($LASTEXITCODE -ne 0) { throw "Second installer execution failed with exit code $LASTEXITCODE." }

    if ((Get-Content -LiteralPath $settings -Raw) -notmatch 'PreserveMe=true') { throw 'User settings were overwritten on repeated installation.' }
    if ((Get-Content -LiteralPath $saved -Raw) -notmatch 'ci_saved_vehicle') { throw 'Saved vehicles were overwritten on repeated installation.' }
    if ((Get-FileHash -LiteralPath $hotkeys -Algorithm SHA256).Hash -ne $expectedHotkeysHash) { throw 'User hotkeys.json was overwritten on repeated installation.' }
    if ((Get-Content -LiteralPath $db -Raw) -notmatch 'user_custom_model_keep') { throw 'User vehicle catalog was overwritten on repeated installation.' }
    if ((Get-FileHash -LiteralPath $builtinDb -Algorithm SHA256).Hash -ne $expectedBuiltinDbHash) { throw 'Built-in vehicle fallback was not refreshed with the release database.' }
    if (Test-Path -LiteralPath $duplicate) { throw 'Numbered duplicate JSON remained in the active vehicles folder.' }
    if (-not (Get-ChildItem -LiteralPath $backups -Recurse -File -Filter 'test_duplicate (2).json' -ErrorAction SilentlyContinue)) { throw 'Numbered duplicate JSON was not preserved in the backup folder.' }
    if (Test-Path -LiteralPath (Join-Path $scripts 'YnixTrainerExtended.dll')) { throw 'Legacy DLL remains active beside the renamed DLL.' }
    if (Test-Path -LiteralPath (Join-Path $scripts 'YnixTrainerExtended.ini')) { throw 'Legacy INI remains after repeat install.' }
    if (Test-Path -LiteralPath (Join-Path $scripts 'YnixTrainer')) { throw 'Legacy data folder remains after repeat install.' }
    if (-not (Test-Path -LiteralPath ($settings + '.sha256')) -or -not (Test-Path -LiteralPath ($settings + '.bak'))) { throw 'Settings backup/checksum missing after repeat install.' }
    $activeDlls = @(Get-ChildItem -LiteralPath $scripts -File -Filter '*.dll' | Where-Object { $_.Name -match 'AvernesSowtware|YnixTrainer' })
    if ($activeDlls.Count -ne 1 -or $activeDlls[0].Name -ne 'AvernesSowtware.dll') { throw 'The scripts folder does not contain exactly one active trainer DLL.' }
    Write-Host '[OK] Installer regression passed: legacy folder/root INI/DLL migration, SHA-256 conflict preservation, critical-file backups, repeat-run settings/saves/hotkeys/custom vehicle catalog retention, built-in fallback refresh, duplicate archive, one active DLL.'
}
finally {
    if (Test-Path -LiteralPath $fakeGame) { Remove-Item -LiteralPath $fakeGame -Recurse -Force }
}
