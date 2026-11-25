@echo off
setlocal
title AvernesTreiner 0.8.3 Installer
set "PACKAGE=%~dp0"
set "TARGET=C:\Program Files (x86)\Grand Theft Auto V"
if exist "%TARGET%\GTA5.exe" goto target_ok
set "TARGET=%~dp0"
if exist "%TARGET%GTA5.exe" goto target_ok
echo AvernesTreiner 0.8.3 - GTA V Legacy installer
echo.
set /p TARGET=Enter the full GTA V Legacy folder path:
if not defined TARGET goto failed
set "TARGET=%TARGET:"=%"
for %%I in ("%TARGET%") do set "TARGET=%%~fI"
if not exist "%TARGET%\GTA5.exe" goto failed
:target_ok
if not exist "%PACKAGE%files\scripts\AvernesTreiner.dll" goto package_error
if not exist "%PACKAGE%files\scripts\AvernesTreiner\Languages\en-US.ini" goto package_error
if not exist "%PACKAGE%files\scripts\AvernesTreiner\Languages\ru-RU.ini" goto package_error
if not exist "%TARGET%\scripts" mkdir "%TARGET%\scripts"
set "DATA=%TARGET%\scripts\AvernesTreiner"
set "DOCUMENTS="
for /f "usebackq delims=" %%D in (`powershell -NoProfile -Command "[Environment]::GetFolderPath('MyDocuments')"`) do set "DOCUMENTS=%%D"
if not defined DOCUMENTS set "DOCUMENTS=%USERPROFILE%\Documents"
set "USERDATA=%DOCUMENTS%\AvernesTreiner"
set "BACKUP=%DOCUMENTS%\AvernesTreiner_Backup\update_%RANDOM%_%RANDOM%"
if not exist "%USERDATA%" mkdir "%USERDATA%"
if not exist "%BACKUP%" mkdir "%BACKUP%"
if errorlevel 1 goto backup_failed

rem Back up current persistent data and old game-local data before migration.
if exist "%USERDATA%\*" xcopy "%USERDATA%\*" "%BACKUP%\persistent\" /E /I /Y >nul
if errorlevel 1 goto backup_failed
if exist "%DATA%\*" xcopy "%DATA%\*" "%BACKUP%\legacy-game-data\" /E /I /Y >nul
if errorlevel 1 goto backup_failed

rem Migrate from the previous game-local folder without overwriting newer Documents data.
for %%F in (Config.ini SavedVehicles.json addons.json) do (
  if not exist "%USERDATA%\%%F" if exist "%DATA%\%%F" copy "%DATA%\%%F" "%USERDATA%\%%F" >nul
  if errorlevel 1 goto migration_failed
)

rem Migrate the latest backup created by older 0.8.1 installers, if needed.
set "LEGACYBACKUP="
for /f "delims=" %%D in ('dir /b /ad /o-d "%DOCUMENTS%\AvernesTrainer_Backup" 2^>nul') do if not defined LEGACYBACKUP set "LEGACYBACKUP=%USERPROFILE%\Documents\AvernesTrainer_Backup\%%D"
if not defined LEGACYBACKUP for /f "delims=" %%D in ('dir /b /ad /o-d "%DOCUMENTS%\AvernesTreiner_Backup" 2^>nul') do if not defined LEGACYBACKUP set "LEGACYBACKUP=%USERPROFILE%\Documents\AvernesTreiner_Backup\%%D"
if defined LEGACYBACKUP (
  for %%F in (Config.ini SavedVehicles.json addons.json) do (
    if not exist "%USERDATA%\%%F" if exist "%LEGACYBACKUP%\%%F" copy "%LEGACYBACKUP%\%%F" "%USERDATA%\%%F" >nul
    if errorlevel 1 goto migration_failed
  )
)
if not exist "%USERDATA%\Config.ini" if exist "%APPDATA%\AvernesTreiner\Config.ini" copy "%APPDATA%\AvernesTreiner\Config.ini" "%USERDATA%\Config.ini" >nul
if errorlevel 1 goto migration_failed

copy /y "%PACKAGE%files\scripts\AvernesTreiner.dll" "%TARGET%\scripts\AvernesTreiner.dll" >nul
if errorlevel 1 goto failed
if not exist "%DATA%\Languages" mkdir "%DATA%\Languages"
xcopy "%PACKAGE%files\scripts\AvernesTreiner\Languages\*.ini" "%DATA%\Languages\" /I /Y >nul
if errorlevel 1 goto failed

echo.
echo Installation completed.
echo User data location: "%USERDATA%"
echo Backup saved to: "%BACKUP%"
echo Existing user files were migrated only when the Documents copy did not exist.
pause
exit /b 0
:migration_failed
echo ERROR: User data migration failed. Do not start the trainer; your previous files remain in the backup.
echo Backup: "%BACKUP%"
pause
exit /b 4
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
