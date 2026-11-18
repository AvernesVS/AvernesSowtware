@echo off
setlocal EnableExtensions DisableDelayedExpansion
title AvernesSowtware 1.4.5 Installer
cd /d "%~dp0" || goto :project_dir_fail
set "PROJECT_DIR=%CD%"
set "INSTALL_LOG=%PROJECT_DIR%\AvernesSowtware_Install.log"
if /i "%~2"=="--elevated" (
  call :log "Installer resumed with elevated permissions."
) else (
  >"%INSTALL_LOG%" echo AvernesSowtware 1.4.5 installation log
  call :log "Installer started. Project directory: %PROJECT_DIR%"
)
set "GTAV_DIR=%~1"
if not defined GTAV_DIR set "GTAV_DIR=C:\Program Files (x86)\Grand Theft Auto V"
set "GTAV_DIR=%GTAV_DIR:"=%"
for %%I in ("%GTAV_DIR%") do set "GTAV_DIR=%%~fI"
call :find_shvdn
if not defined SHVDN_DLL call :prompt_game_path
if not defined SHVDN_DLL (
  set "FAIL_REASON=ScriptHookVDotNet2.dll was not found in the selected GTA V root or scripts folder."
  goto :fail
)
set "SCRIPTS_DIR=%GTAV_DIR%\scripts"
set "DATA_DIR=%SCRIPTS_DIR%\AvernesSowtware"
set "VEHICLES_DIR=%DATA_DIR%\Vehicles"
set "LEGACY_DIR=%SCRIPTS_DIR%\YnixTrainer"
set "LOG_DIR=%SCRIPTS_DIR%\AvernesSowtware_Logs"
set "BACKUP_DIR=%SCRIPTS_DIR%\AvernesSowtware_Backups"
set "SOURCE_DLL=%PROJECT_DIR%\dist\AvernesSowtware.dll"
set "PS_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%PS_EXE%" set "PS_EXE=powershell.exe"
"%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -Command "$game=[IO.Path]::GetFullPath($env:GTAV_DIR).TrimEnd('\'); $roots=@([Environment]::GetFolderPath('ProgramFiles'),[Environment]::GetFolderPath('ProgramFilesX86')); $protected=$false; foreach($r in $roots){ if($r){ $r=[IO.Path]::GetFullPath($r).TrimEnd('\'); if($game.Equals($r,[StringComparison]::OrdinalIgnoreCase) -or $game.StartsWith($r+'\',[StringComparison]::OrdinalIgnoreCase)){ $protected=$true } } }; $admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator); if($protected -and -not $admin){ exit 5 }; exit 0" >>"%INSTALL_LOG%" 2>&1
if errorlevel 5 goto :request_elevation
if errorlevel 1 (set "FAIL_REASON=Could not determine Windows installer permissions." & goto :fail)
if not exist "%SOURCE_DLL%" (
  set "FAIL_REASON=Compiled DLL is missing: %SOURCE_DLL%"
  goto :fail
)
for %%I in ("%SOURCE_DLL%") do set "SOURCE_SIZE=%%~zI"
if "%SOURCE_SIZE%"=="0" (
  set "FAIL_REASON=Compiled DLL is empty."
  goto :fail
)
for %%F in (
  "%PROJECT_DIR%\package\AvernesSowtware\AvernesSowtware.ini"
  "%PROJECT_DIR%\package\AvernesSowtware\addons.json"
  "%PROJECT_DIR%\package\AvernesSowtware\favorite_locations.json"
  "%PROJECT_DIR%\package\AvernesSowtware\hotkeys.json"
  "%PROJECT_DIR%\package\AvernesSowtware\Vehicles\vehicles.json"
  "%PROJECT_DIR%\package\AvernesSowtware\Vehicles\saved_vehicles.json"
  "%PROJECT_DIR%\package\AvernesSowtware\vehicle_catalog.json"
) do if not exist "%%~fF" (
  set "FAIL_REASON=Required packaged file is missing: %%~fF"
  goto :fail
)
for %%D in ("%SCRIPTS_DIR%" "%DATA_DIR%" "%VEHICLES_DIR%" "%LOG_DIR%" "%BACKUP_DIR%") do if not exist "%%~fD" mkdir "%%~fD" >>"%INSTALL_LOG%" 2>&1
if errorlevel 1 goto :request_elevation
set "WRITE_TEST=%SCRIPTS_DIR%\.__avernes_write_test_%RANDOM%_%RANDOM%.tmp"
>"%WRITE_TEST%" echo AvernesSowtware write test 2>>"%INSTALL_LOG%"
if errorlevel 1 goto :request_elevation
del /f /q "%WRITE_TEST%" >>"%INSTALL_LOG%" 2>&1

rem Migrate every legacy file before removing the old YnixTrainer folder and DLL.
"%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%\build\Migrate-LegacyData.ps1" -ScriptsDirectory "%SCRIPTS_DIR%" -DataDirectory "%DATA_DIR%" -BackupDirectory "%BACKUP_DIR%" >>"%INSTALL_LOG%" 2>&1
if errorlevel 1 (set "FAIL_REASON=Legacy migration did not complete with verified copies. Old data was retained; see install log." & goto :fail)

rem Migrate old user files only when the new location is absent.
call :preserve_or_default "%DATA_DIR%\AvernesSowtware.ini" "%LEGACY_DIR%\YnixTrainerExtended.ini" "%PROJECT_DIR%\package\AvernesSowtware\AvernesSowtware.ini" "settings"
if errorlevel 1 goto :fail
call :preserve_or_default "%DATA_DIR%\Config.ini" "%LEGACY_DIR%\Config.ini" "" "legacy Config.ini"
if errorlevel 1 goto :fail
call :preserve_or_default "%DATA_DIR%\addons.json" "%LEGACY_DIR%\addons.json" "%PROJECT_DIR%\package\AvernesSowtware\addons.json" "add-on list"
if errorlevel 1 goto :fail
call :preserve_or_default "%DATA_DIR%\favorite_locations.json" "%LEGACY_DIR%\favorite_locations.json" "%PROJECT_DIR%\package\AvernesSowtware\favorite_locations.json" "favorite locations"
if errorlevel 1 goto :fail
call :preserve_or_default "%DATA_DIR%\hotkeys.json" "" "%PROJECT_DIR%\package\AvernesSowtware\hotkeys.json" "hotkey configuration"
if errorlevel 1 goto :fail
call :preserve_or_default "%VEHICLES_DIR%\saved_vehicles.json" "%LEGACY_DIR%\Vehicles\saved_vehicles.json" "%PROJECT_DIR%\package\AvernesSowtware\Vehicles\saved_vehicles.json" "saved vehicle slots"
if errorlevel 1 goto :fail

rem Preserve any existing user master catalog; refresh only separate built-in fallbacks.
call :preserve_or_default "%VEHICLES_DIR%\vehicles.json" "%LEGACY_DIR%\Vehicles\vehicles.json" "%PROJECT_DIR%\package\AvernesSowtware\Vehicles\vehicles.json" "main vehicle database"
if errorlevel 1 goto :fail
call :copy_overwrite "%PROJECT_DIR%\package\AvernesSowtware\Vehicles\vehicles.json" "%VEHICLES_DIR%\vehicles_builtin.json" "built-in vehicle fallback"
if errorlevel 1 goto :fail
call :preserve_or_default "%VEHICLES_DIR%\vehicle_catalog.json" "%LEGACY_DIR%\Vehicles\vehicle_catalog.json" "%PROJECT_DIR%\package\AvernesSowtware\vehicle_catalog.json" "vehicle compatibility database"
if errorlevel 1 goto :fail
call :copy_overwrite "%PROJECT_DIR%\package\AvernesSowtware\vehicle_catalog.json" "%VEHICLES_DIR%\vehicle_catalog_builtin.json" "built-in compatibility fallback"
if errorlevel 1 goto :fail
call :preserve_or_default "%DATA_DIR%\vehicle_catalog.json" "%LEGACY_DIR%\vehicle_catalog.json" "%PROJECT_DIR%\package\AvernesSowtware\vehicle_catalog.json" "legacy database fallback"
if errorlevel 1 goto :fail
call :copy_overwrite "%PROJECT_DIR%\package\AvernesSowtware\addons.json" "%DATA_DIR%\addons_builtin.json" "built-in add-on fallback"
if errorlevel 1 goto :fail
copy /y "%PROJECT_DIR%\package\AvernesSowtware\Vehicles\README.txt" "%VEHICLES_DIR%\README.txt" >>"%INSTALL_LOG%" 2>&1

rem Create verified recoverable copies and SHA-256 sidecars for every critical file.
"%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%\build\Finalize-InstallStorage.ps1" -DataDirectory "%DATA_DIR%" -VehiclesDirectory "%VEHICLES_DIR%" >>"%INSTALL_LOG%" 2>&1
if errorlevel 1 (set "FAIL_REASON=Critical settings/database backup or checksum validation failed." & goto :fail)

rem Move numbered duplicate JSONs aside instead of deleting user data.
call :archive_numbered_duplicates "%DATA_DIR%"
call :archive_numbered_duplicates "%VEHICLES_DIR%"

rem Stage and verify the new DLL before replacing any active copy.
set "STAGED_DLL=%SCRIPTS_DIR%\AvernesSowtware.dll.new"
if exist "%STAGED_DLL%" del /f /q "%STAGED_DLL%" >>"%INSTALL_LOG%" 2>&1
copy /b /y "%SOURCE_DLL%" "%STAGED_DLL%" >>"%INSTALL_LOG%" 2>&1
if errorlevel 1 (
  set "FAIL_REASON=Could not stage the new AvernesSowtware.dll."
  goto :fail
)
for %%I in ("%STAGED_DLL%") do set "STAGED_SIZE=%%~zI"
if not "%SOURCE_SIZE%"=="%STAGED_SIZE%" (
  set "FAIL_REASON=Staged DLL size mismatch."
  goto :fail
)

rem Back up and replace the current branded DLL; never leave two active trainer DLLs.
set "CURRENT_DLL=%SCRIPTS_DIR%\AvernesSowtware.dll"
if exist "%CURRENT_DLL%" (
  call :make_backup_name "AvernesSowtware.dll.pre_1_3_3"
  copy /b /y "%CURRENT_DLL%" "%BACKUP_FILE%" >>"%INSTALL_LOG%" 2>&1
  if errorlevel 1 (
    set "FAIL_REASON=Could not back up the installed AvernesSowtware.dll."
    goto :fail
  )
)
move /y "%STAGED_DLL%" "%CURRENT_DLL%" >>"%INSTALL_LOG%" 2>&1
if errorlevel 1 (
  set "FAIL_REASON=Could not replace AvernesSowtware.dll. Close GTA V and retry."
  goto :fail
)

rem Preserve the legacy binary, then remove it from scripts so SHVDN cannot load two copies.
if exist "%SCRIPTS_DIR%\YnixTrainerExtended.dll" (
  call :make_backup_name "YnixTrainerExtended.dll.legacy"
  copy /b /y "%SCRIPTS_DIR%\YnixTrainerExtended.dll" "%BACKUP_FILE%" >>"%INSTALL_LOG%" 2>&1
  if errorlevel 1 (
    set "FAIL_REASON=Could not back up the previous YnixTrainer DLL."
    goto :fail
  )
  del /f /q "%SCRIPTS_DIR%\YnixTrainerExtended.dll" >>"%INSTALL_LOG%" 2>&1
  if exist "%SCRIPTS_DIR%\YnixTrainerExtended.dll" (
    set "FAIL_REASON=The old YnixTrainer DLL is still active. Close GTA V and retry."
    goto :fail
  )
)

if not exist "%CURRENT_DLL%" (
  set "FAIL_REASON=AvernesSowtware.dll is missing after installation."
  goto :fail
)
if not exist "%VEHICLES_DIR%\vehicles.json" (
  set "FAIL_REASON=Vehicle database is missing after installation."
  goto :fail
)
for %%I in ("%VEHICLES_DIR%\vehicles.json") do set "DB_SIZE=%%~zI"
if "%DB_SIZE%"=="0" (
  set "FAIL_REASON=Vehicle database is empty after installation."
  goto :fail
)
if not exist "%VEHICLES_DIR%\saved_vehicles.json" (
  set "FAIL_REASON=Saved vehicle slots file is missing after installation."
  goto :fail
)
for %%I in ("%CURRENT_DLL%") do set "FINAL_SIZE=%%~zI"
if not "%SOURCE_SIZE%"=="%FINAL_SIZE%" (
  set "FAIL_REASON=Installed DLL size does not match the release DLL."
  goto :fail
)
call :log "Installation successful. DLL=%CURRENT_DLL%; database=%VEHICLES_DIR%\vehicles.json; bytes=%FINAL_SIZE%."
>>"%LOG_DIR%\install.log" echo [%DATE% %TIME%] AvernesSowtware 1.4.5 installation completed. Full log: %INSTALL_LOG%
echo.
echo [SUCCESS] AvernesSowtware 1.4.5 installed.
echo DLL: "%CURRENT_DLL%"
echo Vehicle database: "%VEHICLES_DIR%\vehicles.json"
echo User files preserved; shipped databases updated in place.
echo Restart GTA V Story Mode to load AvernesSowtware.
if /i "%~2"=="--no-pause" exit /b 0
if /i "%~3"=="--no-pause" exit /b 0
pause
exit /b 0

:preserve_or_default
if exist "%~1" exit /b 0
if not "%~2"=="" if exist "%~2" (
  copy /b /y "%~2" "%~1" >>"%INSTALL_LOG%" 2>&1
  if errorlevel 1 (set "FAIL_REASON=Could not migrate legacy %~4." & exit /b 1)
  call :log "Migrated legacy %~4 to %~1"
  exit /b 0
)
if not "%~3"=="" (
  copy /b /y "%~3" "%~1" >>"%INSTALL_LOG%" 2>&1
  if errorlevel 1 (set "FAIL_REASON=Could not install default %~4." & exit /b 1)
  call :log "Installed default %~4 to %~1"
)
exit /b 0

:copy_overwrite
copy /b /y "%~1" "%~2" >>"%INSTALL_LOG%" 2>&1
if errorlevel 1 (set "FAIL_REASON=Could not update %~3." & exit /b 1)
if not exist "%~2" (set "FAIL_REASON=%~3 is missing after copy." & exit /b 1)
for %%I in ("%~2") do if "%%~zI"=="0" (set "FAIL_REASON=%~3 is empty after copy." & exit /b 1)
call :log "Updated %~3: %~2"
exit /b 0

:make_backup_name
set "BACKUP_FILE=%BACKUP_DIR%\%~1.bak"
set /a BACKUP_INDEX=2
:make_backup_name_loop
if exist "%BACKUP_FILE%" (
  set "BACKUP_FILE=%BACKUP_DIR%\%~1_%BACKUP_INDEX%.bak"
  set /a BACKUP_INDEX+=1
  if %BACKUP_INDEX% GTR 100 exit /b 1
  goto :make_backup_name_loop
)
exit /b 0

:archive_numbered_duplicates
set "DUPLICATE_DIR=%BACKUP_DIR%\numbered_json_duplicates"
if not exist "%~1" exit /b 0
set "DUP_DIR_ARG=%~1"
set "DUP_BACKUP_ARG=%DUPLICATE_DIR%"
set "PS_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%PS_EXE%" (
  "%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -Command "$d=$env:DUP_DIR_ARG; $b=$env:DUP_BACKUP_ARG; if(Test-Path -LiteralPath $d){ Get-ChildItem -LiteralPath $d -Recurse | Where-Object { -not $_.PSIsContainer -and $_.Extension -ieq '.json' -and $_.BaseName -match ' \(\d+\)$' } | ForEach-Object { if(-not (Test-Path -LiteralPath $b)){ New-Item -ItemType Directory -Path $b -Force | Out-Null }; $target=Join-Path $b $_.Name; $i=2; while(Test-Path -LiteralPath $target){ $target=Join-Path $b ($_.BaseName+'_'+$i+$_.Extension); $i++ }; Move-Item -LiteralPath $_.FullName -Destination $target } }" >>"%INSTALL_LOG%" 2>&1
)
exit /b 0

:find_shvdn
set "SHVDN_DLL="
if exist "%GTAV_DIR%\ScriptHookVDotNet2.dll" set "SHVDN_DLL=%GTAV_DIR%\ScriptHookVDotNet2.dll"
if not defined SHVDN_DLL if exist "%GTAV_DIR%\scripts\ScriptHookVDotNet2.dll" set "SHVDN_DLL=%GTAV_DIR%\scripts\ScriptHookVDotNet2.dll"
exit /b 0

:prompt_game_path
echo ScriptHookVDotNet2.dll was not found in the default GTA V location.
set "GTAV_DIR="
set /p "GTAV_DIR=Enter GTA V root folder: "
if not defined GTAV_DIR exit /b 1
set "GTAV_DIR=%GTAV_DIR:"=%"
for %%I in ("%GTAV_DIR%") do set "GTAV_DIR=%%~fI"
call :find_shvdn
exit /b 0

:request_elevation
if /i "%~2"=="--elevated" (
  set "FAIL_REASON=Windows denied write access even after elevation. Check permissions or security software."
  goto :fail
)
call :log "Requesting administrator approval for GTA V installation folder."
set "ELEVATE_SCRIPT=%~f0"
set "ELEVATE_GAME=%GTAV_DIR%"
set "ELEVATE_ARGS=/d /c ""%ELEVATE_SCRIPT%"" ""%ELEVATE_GAME%"" --elevated %~2"
set "PS_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%PS_EXE%" set "PS_EXE=powershell.exe"
"%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -Command "$p=Start-Process -FilePath $env:ComSpec -ArgumentList $env:ELEVATE_ARGS -Verb RunAs -Wait -PassThru; exit $p.ExitCode"
if errorlevel 1 (set "FAIL_REASON=Administrator approval was cancelled or installation failed." & goto :fail)
exit /b 0

:log
>>"%INSTALL_LOG%" echo [%DATE% %TIME%] %*
exit /b 0

:project_dir_fail
echo [ERROR] Could not enter the installer project directory.
if /i not "%~2"=="--no-pause" if /i not "%~3"=="--no-pause" pause
exit /b 1

:fail
if defined FAIL_REASON call :log "ERROR: %FAIL_REASON%"
if exist "%SCRIPTS_DIR%\AvernesSowtware.dll.new" del /f /q "%SCRIPTS_DIR%\AvernesSowtware.dll.new" >>"%INSTALL_LOG%" 2>&1
echo.
echo [ERROR] Installation did not complete.
if defined FAIL_REASON echo Reason: %FAIL_REASON%
if defined INSTALL_LOG echo Log: "%INSTALL_LOG%"
echo Existing user saves and settings were preserved.
if /i not "%~2"=="--no-pause" if /i not "%~3"=="--no-pause" pause
exit /b 1
