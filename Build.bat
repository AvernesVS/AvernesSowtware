@echo off
setlocal EnableExtensions DisableDelayedExpansion
title AvernesSowtware 1.4.5 Build
cd /d "%~dp0.." || (
  echo [ERROR] Cannot enter the project folder.
  pause
  exit /b 10
)
set "PROJECT_DIR=%CD%"
set "BUILD_LOG=%PROJECT_DIR%\AvernesSowtware_Build.log"
>"%BUILD_LOG%" echo AvernesSowtware 1.4.5 build log
call :log "Build started. Project directory: %PROJECT_DIR%"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  set "FAIL_REASON=.NET Framework csc.exe was not found. Verify .NET Framework 4.8 compiler tools."
  goto :fail
)
set "GTAV_DIR=%~1"
if not defined GTAV_DIR set "GTAV_DIR=C:\Program Files (x86)\Grand Theft Auto V"
set "GTAV_DIR=%GTAV_DIR:"=%"
for %%I in ("%GTAV_DIR%") do set "GTAV_DIR=%%~fI"
rem Prefer the explicit build-time reference (used by CI); otherwise use the installed game copy.
set "SHVDN_DLL=%AVERNE_SHVDN_DLL%"
if defined SHVDN_DLL if not exist "%SHVDN_DLL%" (
  set "FAIL_REASON=AVERNE_SHVDN_DLL points to a missing file: %SHVDN_DLL%"
  goto :fail
)
if not defined SHVDN_DLL if exist "%GTAV_DIR%\ScriptHookVDotNet2.dll" set "SHVDN_DLL=%GTAV_DIR%\ScriptHookVDotNet2.dll"
if not defined SHVDN_DLL if exist "%GTAV_DIR%\scripts\ScriptHookVDotNet2.dll" set "SHVDN_DLL=%GTAV_DIR%\scripts\ScriptHookVDotNet2.dll"
if not defined SHVDN_DLL call :prompt_game_path
if not defined SHVDN_DLL (
  set "FAIL_REASON=ScriptHookVDotNet2.dll was not found. Set AVERNE_SHVDN_DLL or select the GTA V root/scripts folder."
  goto :fail
)
if not exist "src\Core\AvernesSowtwareScript.cs" (
  set "FAIL_REASON=Source file src\Core\AvernesSowtwareScript.cs is missing."
  goto :fail
)
call :log "Compiler: %CSC%"
call :log "GTA V directory: %GTAV_DIR%"
call :log "ScriptHookVDotNet2 reference: %SHVDN_DLL%"
echo.
echo AvernesSowtware 1.4.5 - source build
if not exist "dist" mkdir "dist" >>"%BUILD_LOG%" 2>&1
set "BUILD_DLL=dist\AvernesSowtware.build.dll"
set "FINAL_DLL=dist\AvernesSowtware.dll"
if exist "%BUILD_DLL%" del /f /q "%BUILD_DLL%" >>"%BUILD_LOG%" 2>&1
if exist "%FINAL_DLL%.new" del /f /q "%FINAL_DLL%.new" >>"%BUILD_LOG%" 2>&1
call :log "Invoking C# compiler."
"%CSC%" /nologo /target:library /platform:anycpu /langversion:4 /optimize+ /warn:4 /out:"%BUILD_DLL%" /reference:"%SHVDN_DLL%" /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll "src\Core\AvernesSowtwareScript.cs" "src\Core\SettingsPersistence.cs" "src\Core\RuntimeDiagnostics.cs" "src\Core\HotkeyManager.cs" "src\Core\TeleportHistory.cs" "src\Managers\VehicleSystems.cs" "src\Managers\VehicleCatalog.cs" "src\UI\MenuRenderer.cs" >>"%BUILD_LOG%" 2>&1
set "COMPILE_RC=%ERRORLEVEL%"
if not "%COMPILE_RC%"=="0" (
  set "FAIL_REASON=C# compilation failed (compiler exit code %COMPILE_RC%). See compiler output in the build log."
  goto :fail
)
if not exist "%BUILD_DLL%" (
  set "FAIL_REASON=Compiler returned success but the temporary DLL is missing."
  goto :fail
)
for %%I in ("%BUILD_DLL%") do set "BUILD_SIZE=%%~zI"
if "%BUILD_SIZE%"=="0" (
  set "FAIL_REASON=Compiler produced an empty DLL."
  goto :fail
)
copy /b /y "%BUILD_DLL%" "%FINAL_DLL%.new" >>"%BUILD_LOG%" 2>&1
if errorlevel 1 (
  set "FAIL_REASON=Could not stage the compiled DLL in dist. Previous output was kept."
  goto :fail
)
for %%I in ("%FINAL_DLL%.new") do set "STAGED_SIZE=%%~zI"
if not "%BUILD_SIZE%"=="%STAGED_SIZE%" (
  set "FAIL_REASON=Staged build output size mismatch."
  goto :fail
)
move /y "%FINAL_DLL%.new" "%FINAL_DLL%" >>"%BUILD_LOG%" 2>&1
if errorlevel 1 (
  set "FAIL_REASON=Could not commit the compiled DLL. Previous output was kept."
  goto :fail
)
del /f /q "%BUILD_DLL%" >>"%BUILD_LOG%" 2>&1
call :log "Compilation succeeded: %FINAL_DLL% (%BUILD_SIZE% bytes)."
echo.
echo [OK] dist\AvernesSowtware.dll built successfully.
if /i "%~2"=="--build-only" goto :build_only_success
echo Starting automatic installation. UAC is requested only if the game folder is not writable.
call "Install_AvernesSowtware.bat" "%GTAV_DIR%"
set "INSTALL_RC=%ERRORLEVEL%"
if not "%INSTALL_RC%"=="0" (
  set "FAIL_REASON=Compilation succeeded, but installation failed (exit code %INSTALL_RC%). See AvernesSowtware_Install.log."
  goto :fail
)
call :log "Build and automatic installation completed successfully."
echo.
echo [SUCCESS] Build and installation completed.
echo Build log: "%BUILD_LOG%"
if /i "%~3"=="--no-pause" exit /b 0
pause
exit /b 0
:build_only_success
call :log "Build-only mode selected; GTA V installation was not executed."
echo [SUCCESS] Trainer DLL compiled. Installation was intentionally skipped.
echo Build log: "%BUILD_LOG%"
if /i "%~3"=="--no-pause" exit /b 0
pause
exit /b 0
:prompt_game_path
echo ScriptHookVDotNet2.dll was not found in the default GTA V folder.
echo It may be in GTA V root or GTA V\scripts.
set "GTAV_DIR="
set /p "GTAV_DIR=Enter GTA V root folder: "
if not defined GTAV_DIR exit /b 1
set "GTAV_DIR=%GTAV_DIR:"=%"
for %%I in ("%GTAV_DIR%") do set "GTAV_DIR=%%~fI"
set "SHVDN_DLL="
if exist "%GTAV_DIR%\ScriptHookVDotNet2.dll" set "SHVDN_DLL=%GTAV_DIR%\ScriptHookVDotNet2.dll"
if not defined SHVDN_DLL if exist "%GTAV_DIR%\scripts\ScriptHookVDotNet2.dll" set "SHVDN_DLL=%GTAV_DIR%\scripts\ScriptHookVDotNet2.dll"
exit /b 0
:log
>>"%BUILD_LOG%" echo [%DATE% %TIME%] %*
exit /b 0
:fail
call :log "ERROR: %FAIL_REASON%"
echo.
echo [ERROR] %FAIL_REASON%
echo Build log: "%BUILD_LOG%"
if exist "AvernesSowtware_Install.log" echo Install log: "%PROJECT_DIR%\AvernesSowtware_Install.log"
if /i "%~3"=="--no-pause" exit /b 1
pause
exit /b 1
