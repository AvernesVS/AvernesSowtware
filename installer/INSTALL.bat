@echo off
setlocal
title AvernesTreiner 0.8.1 Installer
set "PACKAGE=%~dp0"
set "TARGET=%~dp0"
if exist "%TARGET%GTA5.exe" goto target_ok
echo AvernesTreiner 0.8.1 - GTA V Legacy installer
echo.
set /p TARGET=Enter the full GTA V Legacy folder path:
if not defined TARGET goto failed
for %%I in ("%TARGET%") do set "TARGET=%%~fI"
if not exist "%TARGET%\GTA5.exe" goto failed
:target_ok
if not exist "%PACKAGE%files\scripts\AvernesTreiner.dll" goto package_error
if not exist "%PACKAGE%files\scripts\AvernesTreiner\Languages\en-US.ini" goto package_error
if not exist "%PACKAGE%files\scripts\AvernesTreiner\Languages\ru-RU.ini" goto package_error
if not exist "%TARGET%\scripts" mkdir "%TARGET%\scripts"
set "DATA=%TARGET%\scripts\AvernesTreiner"
set "BACKUP=%USERPROFILE%\Documents\AvernesTreiner_Backup\%RANDOM%_%RANDOM%"
if not exist "%DATA%" mkdir "%DATA%"
if exist "%DATA%\Config.ini" (
  if not exist "%BACKUP%" mkdir "%BACKUP%"
  copy /y "%DATA%\Config.ini" "%BACKUP%\Config.ini" >nul
)
if exist "%DATA%\SavedVehicles.json" (
  if not exist "%BACKUP%" mkdir "%BACKUP%"
  copy /y "%DATA%\SavedVehicles.json" "%BACKUP%\SavedVehicles.json" >nul
)
if exist "%DATA%\addons.json" (
  if not exist "%BACKUP%" mkdir "%BACKUP%"
  copy /y "%DATA%\addons.json" "%BACKUP%\addons.json" >nul
)
copy /y "%PACKAGE%files\scripts\AvernesTreiner.dll" "%TARGET%\scripts\AvernesTreiner.dll" >nul
if errorlevel 1 goto failed
xcopy "%PACKAGE%files\scripts\AvernesTreiner\Languages\*.ini" "%DATA%\Languages\" /I /Y >nul
if errorlevel 1 goto failed
echo.
echo Installation completed.
if exist "%BACKUP%" echo Backup saved to: "%BACKUP%"
echo Your Config.ini, SavedVehicles.json and addons.json were preserved.
pause
exit /b 0
:backup_failed
echo ERROR: Backup failed. Installation was cancelled before replacing the trainer.
pause
exit /b 3
:package_error
echo ERROR: Installer package is incomplete.
pause
exit /b 2
:failed
echo ERROR: GTA5.exe was not found or installation failed.
echo Check the selected GTA V Legacy folder and try again.
pause
exit /b 1
