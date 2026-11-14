from pathlib import Path
import sys

ROOT = Path(sys.argv[1]).resolve()

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8-sig")

def write(rel, value):
    (ROOT / rel).write_text(value, encoding="utf-8-sig", newline="")

# Restore the original ARMY/PLAYER relationship if the player switches character groups
# while military-ignore mode is active, then capture the new group's original values.
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
if old not in s:
    raise SystemExit("Military relationship capture block not found; source differs from expected 1.3.1 baseline.")
s = s.replace(old, new, 1)
if "ApplyMilitaryIgnore(ped, false)" not in s:
    raise SystemExit("Unload cleanup does not restore military ignore state.")
write(p, s)

# Search the whole data tree when archiving numbered duplicates during reinstallation.
p = "Install_AvernesSowtware.bat"
s = read(p)
old = "Get-ChildItem -LiteralPath $d | Where-Object"
if old not in s:
    raise SystemExit("Installer duplicate cleanup anchor not found.")
write(p, s.replace(old, "Get-ChildItem -LiteralPath $d -Recurse | Where-Object", 1))

# Replace stale branded text in the current catalog diagnostic, not the legacy migration path.
p = "src/Managers/VehicleCatalog.cs"
s = read(p)
s = s.replace(r"scripts\\YnixTrainer\\Vehicles", r"scripts\\AvernesSowtware\\Vehicles")
write(p, s)

# Launch the actual BAT directly with PowerShell-managed argument quoting in the CI test.
p = "build/Test-InstallerIdempotence.ps1"
s = read(p)
s = s.replace("""    $command = '"{0}" "{1}" --no-pause' -f $installer, $fakeGame
    & $env:ComSpec /d /c $command""", """    & $installer $fakeGame --no-pause""", 1)
s = s.replace("""    & $env:ComSpec /d /c $command""", """    & $installer $fakeGame --no-pause""", 1)
if "& $installer $fakeGame --no-pause" not in s or "$command" in s:
    raise SystemExit("Installer regression test command arguments were not repaired.")
write(p, s)

print("1.3.1 post-finalization safeguards applied.")
