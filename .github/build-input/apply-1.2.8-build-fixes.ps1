param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath($ProjectRoot)
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

function Read-Source([string]$RelativePath) {
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path)) { throw "Required source file is missing: $RelativePath" }
    return @{ Path = $path; Text = [IO.File]::ReadAllText($path) }
}
function Write-Source($File, [string]$Text) {
    [IO.File]::WriteAllText($File.Path, $Text, $utf8Bom)
}

# Fix a try block that had no catch/finally and prevented C# compilation.
$f = Read-Source "src\VehicleSystems.cs"
$old = @'
                try { Function.Call((Hash)0x07503F7948F491A7UL, candidate.X, candidate.Y, candidate.Z); } // REQUEST_COLLISION_AT_COORD
                try
                {
                    if (Function.Call<bool>((Hash)0x61E1DD6125A3EEE6UL, candidate.X, candidate.Y, candidate.Z, 3.5f))
                        continue; // Do not overlap an existing vehicle when an alternate point is available.
                }
                catch (Exception proximityException) { LogException("Vehicle spawn proximity check", proximityException); }
'@
$new = @'
                try
                {
                    Function.Call((Hash)0x07503F7948F491A7UL, candidate.X, candidate.Y, candidate.Z); // REQUEST_COLLISION_AT_COORD
                    if (Function.Call<bool>((Hash)0x61E1DD6125A3EEE6UL, candidate.X, candidate.Y, candidate.Z, 3.5f))
                        continue; // Do not overlap an existing vehicle when an alternate point is available.
                }
                catch (Exception proximityException) { LogException("Vehicle spawn proximity check", proximityException); }
'@
if ($f.Text.Contains($old)) {
    $f.Text = $f.Text.Replace($old, $new)
    Write-Source $f $f.Text
} elseif (-not $f.Text.Contains("catch (Exception proximityException)")) {
    throw "VehicleSystems.cs collision/proximity block did not match expected source."
}

# Add the native namespace needed by the keyboard native calls.
$f = Read-Source "src\VehicleCatalog.cs"
if (-not $f.Text.Contains("using GTA.Native;")) {
    $marker = "using System.Web.Script.Serialization;"
    if (-not $f.Text.Contains($marker)) { throw "VehicleCatalog.cs using insertion point was not found." }
    $f.Text = $f.Text.Replace($marker, $marker + [char]10 + "using GTA.Native;")
    Write-Source $f $f.Text
}

# Let CI provide the exact SHVDN2 reference assembly without needing a real GTA installation.
$f = Read-Source "build\Build.bat"
$old = @'
set "SHVDN_DLL="
if exist "%GTAV_DIR%\ScriptHookVDotNet2.dll" set "SHVDN_DLL=%GTAV_DIR%\ScriptHookVDotNet2.dll"
'@
$new = @'
set "SHVDN_DLL="
if defined YNIX_SHVDN_DLL if exist "%YNIX_SHVDN_DLL%" set "SHVDN_DLL=%YNIX_SHVDN_DLL%"
if not defined SHVDN_DLL if exist "%GTAV_DIR%\ScriptHookVDotNet2.dll" set "SHVDN_DLL=%GTAV_DIR%\ScriptHookVDotNet2.dll"
'@
if ($f.Text.Contains($old)) {
    $f.Text = $f.Text.Replace($old, $new)
    Write-Source $f $f.Text
} elseif (-not $f.Text.Contains("if defined YNIX_SHVDN_DLL")) {
    throw "Build.bat SHVDN2 reference block did not match expected source."
}

# Avoid CMD parsing FAIL_REASON parentheses as part of an IF block.
$f = Read-Source "BUILD_RELEASE.bat"
$start = $f.Text.IndexOf(":fail" + [char]10)
if ($start -lt 0) { throw "BUILD_RELEASE.bat :fail label was not found." }
$replacement = @'
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
if exist "YnixTrainer_Build.log" call :log "Trainer compiler diagnostics: %PROJECT_DIR%\YnixTrainer_Build.log"
>"%REPORT%" echo YnixTrainer Extended 1.2.8 - BUILD FAILED
echo.>>"%REPORT%"
echo Result: FAILED>>"%REPORT%"
echo Reason: %FAIL_REASON%>>"%REPORT%"
echo.>>"%REPORT%"
echo No new release ZIP was published by this build.>>"%REPORT%"
exit /b 0
'@
$f.Text = $f.Text.Substring(0, $start) + $replacement
Write-Source $f $f.Text

Write-Host "Applied 1.2.8 build fixes."
