param(
    [Parameter(Mandatory=$true)][string]$DataDirectory,
    [Parameter(Mandatory=$true)][string]$VehiclesDirectory
)
$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Write-VerifiedSidecar([string]$Path) {
    $hash = Get-Sha256 $Path
    $sidecar = $Path + '.sha256'
    $temporary = $sidecar + '.tmp'
    [IO.File]::WriteAllText($temporary, $hash + [Environment]::NewLine, [Text.Encoding]::ASCII)
    if ((Get-Content -LiteralPath $temporary -Raw).Trim() -ne $hash) { throw "Checksum sidecar verification failed: $sidecar" }
    if (Test-Path -LiteralPath $sidecar) { $replaceBackup = $sidecar + "." + [Guid]::NewGuid().ToString("N") + ".replace.bak"; try { [IO.File]::Replace($temporary, $sidecar, $replaceBackup) } finally { if (Test-Path -LiteralPath $replaceBackup) { Remove-Item -LiteralPath $replaceBackup -Force } } }
    else { [IO.File]::Move($temporary, $sidecar) }
}
function Write-VerifiedBackup([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Critical file is missing: $Path" }
    $backup = $Path + '.bak'
    $temporary = $backup + '.tmp'
    Copy-Item -LiteralPath $Path -Destination $temporary -Force
    if ((Get-Item -LiteralPath $temporary).Length -ne (Get-Item -LiteralPath $Path).Length -or
        (Get-Sha256 $temporary) -ne (Get-Sha256 $Path)) {
        Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        throw "Backup verification failed: $Path"
    }
    if (Test-Path -LiteralPath $backup) { $replaceBackup = $backup + "." + [Guid]::NewGuid().ToString("N") + ".replace.bak"; try { [IO.File]::Replace($temporary, $backup, $replaceBackup) } finally { if (Test-Path -LiteralPath $replaceBackup) { Remove-Item -LiteralPath $replaceBackup -Force } } }
    else { [IO.File]::Move($temporary, $backup) }
    Write-VerifiedSidecar $backup
    Write-VerifiedSidecar $Path
}

$critical = @(
    (Join-Path $DataDirectory 'AvernesSowtware.ini'),
    (Join-Path $VehiclesDirectory 'vehicles.json'),
    (Join-Path $VehiclesDirectory 'vehicles_builtin.json'),
    (Join-Path $VehiclesDirectory 'vehicle_catalog.json'),
    (Join-Path $VehiclesDirectory 'vehicle_catalog_builtin.json'),
    (Join-Path $DataDirectory 'vehicle_catalog.json')
)
foreach ($path in $critical) { Write-VerifiedBackup $path }
Write-Host '[OK] Critical settings and vehicle databases have verified SHA-256 sidecars and backup copies.'
