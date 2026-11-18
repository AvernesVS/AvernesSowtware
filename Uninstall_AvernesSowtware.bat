@echo off
setlocal EnableExtensions DisableDelayedExpansion
title AvernesSowtware 1.4.5 Uninstaller
set "UNINSTALL_LOG=%TEMP%\AvernesSowtware_Uninstall.log"
>"%UNINSTALL_LOG%" echo AvernesSowtware 1.4.5 uninstall log
set "GTAV_DIR=%~1"
if not defined GTAV_DIR set "GTAV_DIR=C:\Program Files (x86)\Grand Theft Auto V"
set "GTAV_DIR=%GTAV_DIR:"=%"
for %%I in ("%GTAV_DIR%") do set "GTAV_DIR=%%~fI"
if not exist "%GTAV_DIR%\scripts" (
  set "GTAV_DIR="
  set /p "GTAV_DIR=Enter GTA V root folder: "
  if not defined GTAV_DIR goto :fail
  set "GTAV_DIR=%GTAV_DIR:"=%"
  for %%I in ("%GTAV_DIR%") do set "GTAV_DIR=%%~fI"
)
set "SCRIPTS_DIR=%GTAV_DIR%\scripts"
set "BACKUP_DIR=%SCRIPTS_DIR%\AvernesSowtware_Backups"
set "CURRENT_DLL=%SCRIPTS_DIR%\AvernesSowtware.dll"
set "RESTORE_SOURCE="
for /f "delims=" %%F in ('dir /b /a-d /o-d "%BACKUP_DIR%\AvernesSowtware.dll.pre_*.bak" 2^>nul') do if not defined RESTORE_SOURCE set "RESTORE_SOURCE=%BACKUP_DIR%\%%F"
if defined RESTORE_SOURCE (
  set "RESTORE_STAGE=%SCRIPTS_DIR%\AvernesSowtware.dll.restore.new"
  copy /b /y "%RESTORE_SOURCE%" "%RESTORE_STAGE%" >>"%UNINSTALL_LOG%" 2>&1
  if errorlevel 1 goto :fail
  move /y "%RESTORE_STAGE%" "%CURRENT_DLL%" >>"%UNINSTALL_LOG%" 2>&1
  if errorlevel 1 goto :fail
  set "RESULT=Previous AvernesSowtware DLL backup restored."
) else (
  if exist "%CURRENT_DLL%" del /f /q "%CURRENT_DLL%" >>"%UNINSTALL_LOG%" 2>&1
  if exist "%CURRENT_DLL%" goto :fail
  set "RESULT=AvernesSowtware DLL removed; no previous backup found."
)
if exist "%SCRIPTS_DIR%\AvernesSowtware.dll.new" del /f /q "%SCRIPTS_DIR%\AvernesSowtware.dll.new" >>"%UNINSTALL_LOG%" 2>&1
echo.
echo [SUCCESS] %RESULT%
echo User settings, vehicle databases, logs and backup folders were preserved.
echo Uninstall log: "%UNINSTALL_LOG%"
pause
exit /b 0
:fail
echo.
echo [ERROR] Uninstall did not complete. Close GTA V and retry.
echo Log: "%UNINSTALL_LOG%"
pause
exit /b 1
