@echo off
setlocal EnableExtensions DisableDelayedExpansion
title AvernesSowtware 1.4.5 Release Builder
cd /d "%~dp0" || (
  echo [ERROR] Cannot enter project directory.
  pause
  exit /b 10
)
set "PROJECT_DIR=%CD%"
set "NO_PAUSE="
if /i "%~2"=="--no-pause" set "NO_PAUSE=1"
set "BUILD_LOG=%PROJECT_DIR%\BUILD_RELEASE.log"
set "REPORT=%PROJECT_DIR%\BUILD_RELEASE_REPORT_RU.txt"
set "VALIDATOR=%PROJECT_DIR%\build\ReleaseValidator.exe"
set "PACKAGER=%PROJECT_DIR%\build\ReleasePackager.exe"
set "OUTPUT_ZIP=%PROJECT_DIR%\dist\AvernesSowtware_v1.4.5_RELEASE.zip"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  set "FAIL_REASON=The .NET Framework C# compiler was not found. Install/repair .NET Framework 4.8 developer compiler tools."
  goto :fail
)
if not exist "build\ReleaseValidator.cs" (
  set "FAIL_REASON=build\ReleaseValidator.cs is missing."
  goto :fail
)
if not exist "build\ReleasePackager.cs" (
  set "FAIL_REASON=build\ReleasePackager.cs is missing."
  goto :fail
)
if not exist "dist" mkdir "dist" >>"%BUILD_LOG%" 2>&1
>"%BUILD_LOG%" echo AvernesSowtware 1.4.5 release build
call :log "Starting pre-build validation."
call :log "Compiler: %CSC%"
"%CSC%" /nologo /target:exe /platform:anycpu /langversion:4 /optimize+ /warn:4 /out:"%VALIDATOR%" /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll "build\ReleaseValidator.cs" >>"%BUILD_LOG%" 2>&1
if errorlevel 1 (
  set "FAIL_REASON=ReleaseValidator compilation failed. See BUILD_RELEASE.log."
  goto :fail
)
"%VALIDATOR%" "%PROJECT_DIR%" >>"%BUILD_LOG%" 2>&1
if errorlevel 1 (
  set "FAIL_REASON=Pre-build validation failed. Release package was not created. See BUILD_RELEASE.log."
  goto :fail
)
call :log "Pre-build validation passed. Compiling release packager."
"%CSC%" /nologo /target:exe /platform:anycpu /langversion:4 /optimize+ /warn:4 /out:"%PACKAGER%" /reference:System.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "build\ReleasePackager.cs" >>"%BUILD_LOG%" 2>&1
if errorlevel 1 (
  set "FAIL_REASON=ReleasePackager compilation failed. See BUILD_RELEASE.log."
  goto :fail
)
call :log "Starting trainer compilation (build-only; no GTA V files will be installed)."
call "build\Build.bat" "%~1" --build-only --no-pause
set "TRAINER_RC=%ERRORLEVEL%"
if exist "AvernesSowtware_Build.log" (
  call :log "----- BEGIN trainer compiler log -----"
  type "AvernesSowtware_Build.log" >>"%BUILD_LOG%" 2>&1
  call :log "----- END trainer compiler log -----"
)
if not "%TRAINER_RC%"=="0" (
  set "FAIL_REASON=Trainer C# compilation failed (exit code %TRAINER_RC%). Full compiler diagnostics are in AvernesSowtware_Build.log and BUILD_RELEASE.log."
  goto :fail
)
if not exist "dist\AvernesSowtware.dll" (
  set "FAIL_REASON=The final compiled trainer DLL is missing."
  goto :fail
)
call :log "Trainer compilation completed. Running post-build validation."
"%VALIDATOR%" "%PROJECT_DIR%" >>"%BUILD_LOG%" 2>&1
if errorlevel 1 (
  set "FAIL_REASON=Post-build validation failed. Release package was not created."
  goto :fail
)
call :write_success_report
call :log "Creating full release ZIP package."
"%PACKAGER%" "%PROJECT_DIR%" "%OUTPUT_ZIP%" "1.4.5" >>"%BUILD_LOG%" 2>&1
if errorlevel 1 (
  set "FAIL_REASON=Release ZIP packaging or archive validation failed."
  goto :fail
)
if not exist "%OUTPUT_ZIP%" (
  set "FAIL_REASON=Packager returned success but output ZIP is missing."
  goto :fail
)
for %%I in ("%OUTPUT_ZIP%") do set "ZIP_SIZE=%%~zI"
if "%ZIP_SIZE%"=="0" (
  set "FAIL_REASON=Release ZIP is empty."
  goto :fail
)
call :log "SUCCESS: %OUTPUT_ZIP% (%ZIP_SIZE% bytes)."
echo.
echo [SUCCESS] AvernesSowtware 1.4.5 release package created.
echo Package: "%OUTPUT_ZIP%"
echo Report: "%REPORT%"
echo Log: "%BUILD_LOG%"
echo.
echo The trainer was compiled and static checks passed. The installed GTA V trainer was not modified.
echo GTA V Story Mode runtime testing is still required before a final release.
if defined NO_PAUSE exit /b 0
pause
exit /b 0

:write_success_report
>"%REPORT%" echo AvernesSowtware 1.4.5 - RELEASE BUILD REPORT
echo.>>"%REPORT%"
echo Build: SUCCESS>>"%REPORT%"
echo Source language/runtime: C# 4 / .NET Framework 4.0 API>>"%REPORT%"
echo Target compatibility: Windows 7 x64 / ScriptHookVDotNet2>>"%REPORT%"
echo Static pre-build validation: PASSED>>"%REPORT%"
echo C# trainer compilation: PASSED>>"%REPORT%"
echo Installer scripts and package validation: PASSED>>"%REPORT%"
echo GTA V installation was not executed by BUILD_RELEASE.bat.>>"%REPORT%"
echo Static post-build validation: PASSED>>"%REPORT%"
echo ZIP packaging: PASSED>>"%REPORT%"
echo.>>"%REPORT%"
echo IMPORTANT: This report confirms build-time checks only. It does not claim GTA V in-game runtime testing.>>"%REPORT%"
echo This package is build-complete only. GTA V Legacy Story Mode runtime behavior has not been validated by this script.>>"%REPORT%"
exit /b 0

:log
>>"%BUILD_LOG%" echo [%DATE% %TIME%] %*
exit /b 0

:fail
if defined FAIL_REASON call :write_fail_report
if exist "%OUTPUT_ZIP%" echo A previous release ZIP, if present, was preserved.>>"%REPORT%"
echo.
echo [ERROR] Build did not complete.
if defined FAIL_REASON echo %FAIL_REASON%
echo Report: "%REPORT%"
echo Log: "%BUILD_LOG%"
if defined NO_PAUSE exit /b 1
pause
exit /b 1

:write_fail_report
call :log "ERROR: %FAIL_REASON%"
if exist "AvernesSowtware_Build.log" call :log "Trainer compiler diagnostics: %PROJECT_DIR%\AvernesSowtware_Build.log"
>"%REPORT%" echo AvernesSowtware 1.4.5 - BUILD FAILED
echo.>>"%REPORT%"
echo Result: FAILED>>"%REPORT%"
echo Reason: %FAIL_REASON%>>"%REPORT%"
echo.>>"%REPORT%"
echo No new release ZIP was published by this build.>>"%REPORT%"
exit /b 0