param(
    [Parameter(Mandatory=$true)][string]$ScriptsDirectory,
    [Parameter(Mandatory=$true)][string]$DataDirectory,
    [Parameter(Mandatory=$true)][string]$BackupDirectory
)
$ErrorActionPreference = 'Stop'
$ScriptsDirectory = [IO.Path]::GetFullPath($ScriptsDirectory)
$DataDirectory = [IO.Path]::GetFullPath($DataDirectory)
$BackupDirectory = [IO.Path]::GetFullPath($BackupDirectory)
$legacyRoot = Join-Path $ScriptsDirectory 'YnixTrainer'
$legacyIni = Join-Path $ScriptsDirectory 'YnixTrainerExtended.ini'
$legacyDll = Join-Path $ScriptsDirectory 'YnixTrainerExtended.dll'
$conflictRoot = Join-Path $DataDirectory 'LegacyMigration\Conflicts'

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-LegacyFiles([string]$Root) {
    $result = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $pending.Push($Root)
    while ($pending.Count -gt 0) {
        $current = $pending.Pop()
        foreach ($entry in [IO.Directory]::GetFileSystemEntries($current)) {
            $attributes = [IO.File]::GetAttributes($entry)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Migration stopped: reparse point found and will not be followed: $entry"
            }
            if (($attributes -band [IO.FileAttributes]::Directory) -ne 0) {
                $pending.Push($entry)
            } else {
                $result.Add([IO.FileInfo]::new($entry))
            }
        }
    }
    return $result.ToArray()
}

function Get-UniqueConflictPath([string]$RelativePath) {
    $candidate = Join-Path $conflictRoot $RelativePath
    $parent = Split-Path -Parent $candidate
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    if (-not (Test-Path -LiteralPath $candidate)) { return $candidate }
    # Idempotence: a prior interrupted migration may already have preserved this exact file.
    $legacySource = $script:CurrentMigrationSource
    if ($legacySource -and (Test-Path -LiteralPath $legacySource -PathType Leaf)) {
        $existingInfo = Get-Item -LiteralPath $candidate
        $sourceInfo = Get-Item -LiteralPath $legacySource
        if ($existingInfo.Length -eq $sourceInfo.Length -and (Get-Sha256 $candidate) -eq (Get-Sha256 $legacySource)) {
            return $candidate
        }
    }
    $sourceName = [IO.Path]::GetFileNameWithoutExtension($candidate)
    $extension = [IO.Path]::GetExtension($candidate)
    $index = 2
    do {
        $candidate = Join-Path $parent ($sourceName + '_' + $index + $extension)
        $index++
    } while (Test-Path -LiteralPath $candidate)
    return $candidate
}

function Copy-And-Verify([string]$Source, [string]$Destination, [string]$RelativePath) {
    $target = $Destination
    if (Test-Path -LiteralPath $Destination -PathType Leaf) {
        $existingInfo = Get-Item -LiteralPath $Destination
        $sourceInfo = Get-Item -LiteralPath $Source
        if ($existingInfo.Length -eq $sourceInfo.Length -and (Get-Sha256 $Destination) -eq (Get-Sha256 $Source)) {
            return $Destination
        }
        $script:CurrentMigrationSource = $Source
        try { $target = Get-UniqueConflictPath $RelativePath }
        finally { $script:CurrentMigrationSource = $null }
    }

    $parent = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $temporary = $target + '.migration.tmp'
    Copy-Item -LiteralPath $Source -Destination $temporary -Force
    $sourceInfo = Get-Item -LiteralPath $Source
    $temporaryInfo = Get-Item -LiteralPath $temporary
    if ($sourceInfo.Length -ne $temporaryInfo.Length -or (Get-Sha256 $Source) -ne (Get-Sha256 $temporary)) {
        Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        throw "Migration verification failed (size/SHA-256): $RelativePath"
    }
    Move-Item -LiteralPath $temporary -Destination $target -Force
    $targetInfo = Get-Item -LiteralPath $target
    if ($sourceInfo.Length -ne $targetInfo.Length -or (Get-Sha256 $Source) -ne (Get-Sha256 $target)) {
        throw "Final migration verification failed (size/SHA-256): $RelativePath"
    }
    return $target
}

New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
$migrated = 0
$conflicts = 0

# Phase 1: copy every legacy file; old data is not removed until all copies pass verification.
if (Test-Path -LiteralPath $legacyRoot -PathType Container) {
    $files = Get-LegacyFiles $legacyRoot
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($legacyRoot.Length).TrimStart([char[]]@(92,47))
        $destinationRelative = $relative
        if ($relative -ieq 'YnixTrainerExtended.ini') { $destinationRelative = 'AvernesSowtware.ini' }
        $destination = Join-Path $DataDirectory $destinationRelative
        $before = Test-Path -LiteralPath $destination -PathType Leaf
        $written = Copy-And-Verify $file.FullName $destination $relative
        if ($written.StartsWith($conflictRoot, [StringComparison]::OrdinalIgnoreCase)) { $conflicts++ }
        $migrated++
    }
}

if (Test-Path -LiteralPath $legacyIni -PathType Leaf) {
    $written = Copy-And-Verify $legacyIni (Join-Path $DataDirectory 'AvernesSowtware.ini') 'scripts/YnixTrainerExtended.ini'
    if ($written.StartsWith($conflictRoot, [StringComparison]::OrdinalIgnoreCase)) { $conflicts++ }
    $migrated++
}

# Phase 2: preserve the old DLL first. Remove it only after the data-copy phase completed successfully.
if (Test-Path -LiteralPath $legacyDll -PathType Leaf) {
    $dllBackup = Join-Path $BackupDirectory ('YnixTrainerExtended.dll.legacy_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff') + '.bak')
    Copy-Item -LiteralPath $legacyDll -Destination $dllBackup
    if ((Get-Item -LiteralPath $dllBackup).Length -ne (Get-Item -LiteralPath $legacyDll).Length -or
        (Get-Sha256 $dllBackup) -ne (Get-Sha256 $legacyDll)) {
        throw 'Legacy DLL backup verification failed; old DLL was not deleted.'
    }
}

# Phase 3: only after every migrated file and the DLL backup are verified, remove the old storage.
if (Test-Path -LiteralPath $legacyRoot -PathType Container) {
    Remove-Item -LiteralPath $legacyRoot -Recurse -Force
    if (Test-Path -LiteralPath $legacyRoot) { throw 'Old YnixTrainer data folder could not be removed after successful migration.' }
}
if (Test-Path -LiteralPath $legacyIni -PathType Leaf) {
    Remove-Item -LiteralPath $legacyIni -Force
    if (Test-Path -LiteralPath $legacyIni) { throw 'Old YnixTrainerExtended.ini could not be removed after successful migration.' }
}
if (Test-Path -LiteralPath $legacyDll -PathType Leaf) {
    Remove-Item -LiteralPath $legacyDll -Force
    if (Test-Path -LiteralPath $legacyDll) { throw 'Old YnixTrainerExtended.dll could not be removed; close GTA V and rerun the installer.' }
}

Write-Host "[OK] Legacy migration verified: files=$migrated; conflicts archived=$conflicts; old YnixTrainer folder, DLL and INI removed."
