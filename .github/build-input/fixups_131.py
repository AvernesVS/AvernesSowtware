from pathlib import Path
import sys

ROOT = Path(sys.argv[1]).resolve()

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8-sig")

def write(rel, value):
    (ROOT / rel).write_text(value, encoding="utf-8-sig", newline="")

# 1) Preserve relationships for the previous player group when the active character changes.
p = "src/Core/AvernesSowtwareScript.cs"
s = read(p)
old = """                    if (!_militaryRelationshipCaptured || _militaryRelationshipPlayerGroup != playerGroup ||
                        _militaryRelationshipArmyGroup != armyGroup)
                    {
                        _militaryRelationshipPlayerGroup = playerGroup;
                        _militaryRelationshipArmyGroup = armyGroup;
                        _militaryRelationshipOriginalArmyToPlayer = Function.Call<int>((Hash)0x9E6B70061662AE5CUL, armyGroup, playerGroup);
                        _militaryRelationshipOriginalPlayerToArmy = Function.Call<int>((Hash)0x9E6B70061662AE5CUL, playerGroup, armyGroup);
                        _militaryRelationshipCaptured = true;
                    }"""
new = """                    if (_militaryRelationshipCaptured &&
                        (_militaryRelationshipPlayerGroup != playerGroup || _militaryRelationshipArmyGroup != armyGroup))
                    {
                        Function.Call((Hash)0xBF25EB89375A37ADUL, _militaryRelationshipOriginalArmyToPlayer,
                            _militaryRelationshipArmyGroup, _militaryRelationshipPlayerGroup);
                        Function.Call((Hash)0xBF25EB89375A37ADUL, _militaryRelationshipOriginalPlayerToArmy,
                            _militaryRelationshipPlayerGroup, _militaryRelationshipArmyGroup);
                        _militaryRelationshipCaptured = false;
                    }
                    if (!_militaryRelationshipCaptured)
                    {
                        _militaryRelationshipPlayerGroup = playerGroup;
                        _militaryRelationshipArmyGroup = armyGroup;
                        _militaryRelationshipOriginalArmyToPlayer = Function.Call<int>((Hash)0x9E6B70061662AE5CUL, armyGroup, playerGroup);
                        _militaryRelationshipOriginalPlayerToArmy = Function.Call<int>((Hash)0x9E6B70061662AE5CUL, playerGroup, armyGroup);
                        _militaryRelationshipCaptured = true;
                    }"""
if old in s:
    s = s.replace(old, new, 1)
elif "Restore the previous player group" not in s and "_militaryRelationshipCaptured &&" not in s:
    raise SystemExit("Military relationship capture block not found.")
write(p, s)

# 2) Archive numbered JSON duplicates recursively, without deleting the user's data.
p = "Install_AvernesSowtware.bat"
s = read(p)
s = s.replace("Get-ChildItem -LiteralPath $d | Where-Object", "Get-ChildItem -LiteralPath $d -Recurse | Where-Object", 1)
write(p, s)

# 3) Correct only the current-facing path message; legacy migration paths remain untouched.
p = "src/Managers/VehicleCatalog.cs"
s = read(p)
s = s.replace(r"scripts\\YnixTrainer\\Vehicles", r"scripts\\AvernesSowtware\\Vehicles")
write(p, s)

# 4) Execute the real installer through PowerShell's argument handling in the repeat-install test.
p = "build/Test-InstallerIdempotence.ps1"
s = read(p)
s = s.replace("""    $command = '"{0}" "{1}" --no-pause' -f $installer, $fakeGame
    & $env:ComSpec /d /c $command""", """    & $installer $fakeGame --no-pause""", 1)
s = s.replace("""    & $env:ComSpec /d /c $command""", """    & $installer $fakeGame --no-pause""", 1)
if s.count("& $installer $fakeGame --no-pause") != 2 or "$command" in s:
    raise SystemExit("Installer regression test command arguments were not repaired.")
write(p, s)

# 5) Make installer-first ZIP ordering deterministic, regardless of file-system enumeration order.
p = "build/ReleasePackager.cs"
s = read(p)
call = "            EnsureInstallerFirst(output, archiveRootName);\n"
anchor = "            using (ZipArchive archive = ZipFile.OpenRead(output))"
if "EnsureInstallerFirst(output, archiveRootName);" not in s:
    if anchor not in s:
        raise SystemExit("Release packager archive-validation anchor not found.")
    s = s.replace(anchor, call + anchor, 1)

helper = r'''
    private static void EnsureInstallerFirst(string output, string archiveRootName)
    {
        string temp = output + ".ordered.tmp";
        if (File.Exists(temp)) File.Delete(temp);
        try
        {
            using (ZipArchive source = ZipFile.OpenRead(output))
            {
                using (ZipArchive destination = ZipFile.Open(temp, ZipArchiveMode.Create))
                {
                    string installerName = archiveRootName + "/Install_AvernesSowtware.bat";
                    ZipArchiveEntry installer = source.GetEntry(installerName);
                    if (installer == null)
                        throw new InvalidDataException("The unified installer is missing from the release ZIP.");
                    CopyEntryForOrderedArchive(installer, destination, installerName);
                    foreach (ZipArchiveEntry entry in source.Entries)
                    {
                        if (String.Equals(entry.FullName, installerName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        CopyEntryForOrderedArchive(entry, destination, entry.FullName);
                    }
                }
            }
            File.Delete(output);
            File.Move(temp, output);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static void CopyEntryForOrderedArchive(ZipArchiveEntry sourceEntry, ZipArchive destination, string entryName)
    {
        ZipArchiveEntry destinationEntry = destination.CreateEntry(entryName, CompressionLevel.Optimal);
        using (Stream sourceStream = sourceEntry.Open())
        using (Stream destinationStream = destinationEntry.Open())
        {
            sourceStream.CopyTo(destinationStream);
        }
    }
'''
if "private static void EnsureInstallerFirst(" not in s:
    # Insert before the class's final closing brace (four-space indentation).
    idx = s.rfind("\n    }")
    if idx < 0:
        raise SystemExit("Could not find ReleasePackager class end for ZIP ordering helper.")
    s = s[:idx] + "\n" + helper + s[idx:]
write(p, s)

# Verify the helper runs before checking ZipArchive.Entries[0].
s = read(p)
if s.index("EnsureInstallerFirst(output, archiveRootName);") > s.index(anchor):
    raise SystemExit("Installer-first ZIP reordering runs after validation.")
print("1.3.1 post-finalization safeguards applied; installer entry is explicitly reordered first.")
