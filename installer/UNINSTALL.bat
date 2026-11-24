@echo off
setlocal
title AvernesTreiner 1.0 Uninstaller
set "TARGET=%~dp0"
if exist "%TARGET%GTA5.exe" goto target_ok
echo AvernesTreiner 1.0 - Uninstaller
set /p TARGET=Enter the full GTA V Legacy folder path:
if not defined TARGET goto failed
set "TARGET=%TARGET:"=%"
for %%I in ("%TARGET%") do set "TARGET=%%~fI"
if not exist "%TARGET%\GTA5.exe" goto failed
:target_ok
echo This removes only AvernesTreiner.dll and its shipped language files.
echo User settings and saved vehicles will be kept.
set /p CONFIRM=Continue? (Y/N):
if /I not "%CONFIRM%"=="Y" exit /b 0
set "DATA=%TARGET%\scripts\AvernesTreiner"
set "BACKUP=%USERPROFILE%\Documents\AvernesTreiner_Backup\uninstall_%RANDOM%_%RANDOM%"
if exist "%DATA%\Config.ini" (
  if not exist "%BACKUP%" mkdir "%BACKUP%"
  copy /y "%DATA%\Config.ini" "%BACKUP%\Config.ini" >nul
  if errorlevel 1 goto backup_failed
)
if exist "%DATA%\SavedVehicles.json" (
  if not exist "%BACKUP%" mkdir "%BACKUP%"
  copy /y "%DATA%\SavedVehicles.json" "%BACKUP%\SavedVehicles.json" >nul
  if errorlevel 1 goto backup_failed
)
if exist "%DATA%\addons.json" (
  if not exist "%BACKUP%" mkdir "%BACKUP%"
  copy /y "%DATA%\addons.json" "%BACKUP%\addons.json" >nul
  if errorlevel 1 goto backup_failed
)
if exist "%TARGET%\scripts\AvernesTreiner.dll" del /q "%TARGET%\scripts\AvernesTreiner.dll"
if exist "%DATA%\Languages\en-US.ini" del /q "%DATA%\Languages\en-US.ini"
if exist "%DATA%\Languages\ru-RU.ini" del /q "%DATA%\Languages\ru-RU.ini"
echo AvernesTreiner files removed. User data and other mods were left untouched.
if exist "%BACKUP%" echo Backup saved to: "%BACKUP%"
pause
exit /b 0
:backup_failed
echo ERROR: Backup failed. No trainer files were removed.
pause
exit /b 3
:failed
echo ERROR: GTA5.exe was not found. Nothing was removed.
pause
exit /b 1
