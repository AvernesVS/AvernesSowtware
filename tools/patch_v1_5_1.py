from pathlib import Path
import sys, re
root = Path(sys.argv[1]).resolve()

def replace(rel, old, new, count=1):
    p = root / rel
    s = p.read_text(encoding="utf-8-sig")
    n = s.count(old)
    if n != count:
        raise SystemExit("%s: expected %d matches, got %d" % (rel, count, n))
    p.write_text(s.replace(old, new, count), encoding="utf-8-sig", newline="")

# Bodyguards: delete the entity after a clean vehicle exit; do not relinquish an armed NPC.
p = root / "src/Core/AvernesSowtwareScript.cs"
s = p.read_text(encoding="utf-8-sig")
pattern = r"                    // Stage 2: replace the companion task first; defer group mutation\.[\s\S]*?                    _queuedBodyguardDismissalHandles\.Remove\(handle\);"
replacement = """                    // Delete the ped instead of releasing an armed NPC into ambient AI.
                    // One entity is processed per scheduler tick; group-table natives are avoided.
                    RemoveBodyguardBlip(handle);
                    Ped guard = new Ped(handle);
                    if (IsEntityUsable(guard))
                    {
                        Function.Call((Hash)0xAD738C3085FE7E11UL, handle, true, true); // SET_ENTITY_AS_MISSION_ENTITY
                        Function.Call((Hash)0xAAA34F8A7CB32098UL, handle); // CLEAR_PED_TASKS_IMMEDIATELY
                        guard.Delete();
                        Log("Bodyguard entity deleted after safe vehicle exit: handle=" + handle + ".");
                    }
                    _bodyguardGroupHasDeadMember = true;
                    _bodyguardDismissalDetachedHandles.Remove(handle);
                    _bodyguardDismissalGroupRemovedHandles.Remove(handle);
                    _bodyguardDismissalStartedUtc.Remove(handle);
                    _bodyguardDismissalForcedExitHandles.Remove(handle);
                    _bodyguardDismissalReleaseAfterUtc.Remove(handle);
                    _queuedBodyguardDismissalHandles.Remove(handle);"""
s, n = re.subn(pattern, replacement, s, count=1)
if n != 1: raise SystemExit("bodyguard dismissal block not found")
old = """                catch (Exception ex)
                {
                    _bodyguardGroupHasDeadMember = true;
                    _bodyguardDismissalDetachedHandles.Remove(handle);
                    _bodyguardDismissalGroupRemovedHandles.Remove(handle);
                    _bodyguardDismissalStartedUtc.Remove(handle);
                    _bodyguardDismissalForcedExitHandles.Remove(handle);
                    _bodyguardDismissalReleaseAfterUtc.Remove(handle);
                    _queuedBodyguardDismissalHandles.Remove(handle);
                    LogException("Process bodyguard dismissal " + handle, ex);
                }"""
new = """                catch (Exception ex)
                {
                    LogException("Process bodyguard dismissal; retry queued " + handle, ex);
                    _pendingBodyguardDismissals.Enqueue(handle);
                    _nextBodyguardDismissalUtc = DateTime.UtcNow.AddSeconds(2);
                    return;
                }"""
if old not in s: raise SystemExit("bodyguard catch block not found")
s = s.replace(old, new, 1)
s = s.replace("_nextBodyguardDismissalUtc = DateTime.UtcNow.AddMilliseconds(650);",
              "_nextBodyguardDismissalUtc = now.AddMilliseconds(700);", 1)
s = s.replace('ProductVersion = "1.5.0"', 'ProductVersion = "1.5.1"', 1)
s = s.replace('Function.Call((Hash)0x821FDC827D6F4090UL, Game.Player, 0); // SPECIAL_ABILITY_ACTIVATE',
              'Function.Call((Hash)0x821FDC827D6F4090UL, Game.Player); // SPECIAL_ABILITY_ACTIVATE')
p.write_text(s, encoding="utf-8-sig", newline="")

# Paint palette fallback for native index -1.
p = root / "src/Managers/VehicleSystems.cs"
s = p.read_text(encoding="utf-8-sig")
old = """                using (OutputArgument type = new OutputArgument())
                using (OutputArgument color = new OutputArgument())
                {
                    Function.Call((Hash)0xA19435F193E081ACUL, handle, type, color); // GET_VEHICLE_COLOURS
                    secondaryType = 0;
                    secondaryColor = color.GetResult<int>();
                }"""
new = """                int vehiclePrimaryColor;
                using (OutputArgument primary = new OutputArgument())
                using (OutputArgument secondary = new OutputArgument())
                {
                    Function.Call((Hash)0xA19435F193E081ACUL, handle, primary, secondary); // GET_VEHICLE_COLOURS
                    vehiclePrimaryColor = primary.GetResult<int>();
                    secondaryType = 0;
                    secondaryColor = secondary.GetResult<int>();
                }"""
if old not in s: raise SystemExit("vehicle colour query not found")
s = s.replace(old, new, 1)
old = "                else primaryRgbKnown = TryGetStandardVehicleColorRgb(primaryColor, out primaryR, out primaryG, out primaryB);"
new = """                else
                {
                    primaryRgbKnown = TryGetStandardVehicleColorRgb(primaryColor, out primaryR, out primaryG, out primaryB);
                    if (!primaryRgbKnown && vehiclePrimaryColor >= 0)
                    {
                        primaryColor = vehiclePrimaryColor;
                        primaryRgbKnown = TryGetStandardVehicleColorRgb(primaryColor, out primaryR, out primaryG, out primaryB);
                    }
                }"""
if old not in s: raise SystemExit("primary colour lookup not found")
s = s.replace(old, new, 1)
s = s.replace('LogFailure("Max tuning matte paint", "Primary paint palette index " + primaryColor + " is not recognized; original primary finish was preserved.");',
              'Log("Max tuning matte paint: primary palette index unavailable in both natives; original finish preserved.");', 1)

# Nitro: motorcycle torque stays stock; collision request follows airborne trajectory; aircraft cap at vehicle level.
old = """                    float effectiveNitroPower = isToreador ? Math.Min(nitroPower, 3.0f) : nitroPower;
                    int vehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, handle); // GET_VEHICLE_CLASS
                    bool aircraft = vehicleClass == 15 || vehicleClass == 16;
                    float maximumNitroSpeed = aircraft ? 333.333f : CalculateNitroMaximumSpeedMetersPerSecond(nitroPower);
                    if (isToreador) maximumNitroSpeed = 82.0f; // Preserve controlled Toreador boost envelope."""
new = """                    float effectiveNitroPower = isToreador ? Math.Min(nitroPower, 3.0f) : nitroPower;
                    int vehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, handle); // GET_VEHICLE_CLASS
                    bool aircraft = vehicleClass == 15 || vehicleClass == 16;
                    bool motorcycle = vehicleClass == 8;
                    float maximumNitroSpeed = aircraft ? 333.333f : CalculateNitroMaximumSpeedMetersPerSecond(nitroPower);"""
if old not in s: raise SystemExit("nitro class block not found")
s = s.replace(old, new, 1)
old = """                        float entitySpeedCap = maximumNitroSpeed + 1.0f;
                        Function.Call((Hash)0x0E46A3FCBDE2A1B1UL, handle, entitySpeedCap); // SET_ENTITY_MAX_SPEED
                        _nitroVehicleHandle = handle;"""
new = """                        float entitySpeedCap = maximumNitroSpeed + 1.0f;
                        Function.Call((Hash)0x0E46A3FCBDE2A1B1UL, handle, entitySpeedCap); // SET_ENTITY_MAX_SPEED
                        if (aircraft)
                            Function.Call((Hash)0xBAA045B4E42F3C06UL, handle, maximumNitroSpeed); // SET_VEHICLE_MAX_SPEED
                        _nitroVehicleHandle = handle;"""
if old not in s: raise SystemExit("entity speed cap block not found")
s = s.replace(old, new, 1)
old = "Function.Call((Hash)0xB59E4BD37AE292DBUL, handle, isToreador ? 1.25f : (aircraft ? 1.45f : 1.35f)); // SET_VEHICLE_CHEAT_POWER_INCREASE"
new = """Function.Call((Hash)0xB59E4BD37AE292DBUL, handle,
                        motorcycle ? 1.0f : (isToreador ? 1.25f : (aircraft ? 2.0f : 1.35f))); // SET_VEHICLE_CHEAT_POWER_INCREASE"""
if old not in s: raise SystemExit("nitro torque expression not found (strict)")
s = s.replace(old, new, 1)
# Motorcycle class is checked directly in the target-Z branch; no extra local is required. Trigger patch retry.

# Collision prefetch remains unchanged in this patch; no unverified projection native added. Re-run validation.
old = """                if (Math.Abs(forwardSpeed) < 45.0f) return;
                float lateralX = velocity.X - fx * forwardSpeed;
                float lateralY = velocity.Y - fy * forwardSpeed;
                float lateralSpeed = (float)Math.Sqrt(lateralX * lateralX + lateralY * lateralY);
                // Do not fight normal steering input. Only trim extreme side-slip,
                // and do so gently rather than multiplying lateral velocity every tick.
                if (lateralSpeed < 10.0f) return;
                float damping = lateralSpeed > 25.0f ? 0.96f : 0.985f;"""
new = """                if (Math.Abs(forwardSpeed) < 25.0f) return;
                float lateralX = velocity.X - fx * forwardSpeed;
                float lateralY = velocity.Y - fy * forwardSpeed;
                float lateralSpeed = (float)Math.Sqrt(lateralX * lateralX + lateralY * lateralY);
                if (lateralSpeed < 4.0f) return;
                float damping = lateralSpeed > 15.0f ? 0.90f : 0.96f;"""
if old not in s: raise SystemExit("stability thresholds not found")
s = s.replace(old, new, 1)
p.write_text(s, encoding="utf-8-sig", newline="")

# Update current release markers, package manifests, docs, and tests without removing historical functionality.
for rel in ["Install_AvernesSowtware.bat", "BUILD_RELEASE.bat", "build/Build.bat", "README_RU.md",
            "START_HERE_RU.txt", "BUILD_RELEASE_REPORT_RU.txt", "dist/README_RU.txt"]:
    p = root / rel
    if p.exists():
        p.write_text(p.read_text(encoding="utf-8-sig").replace("1.5.0", "1.5.1"), encoding="utf-8-sig", newline="")
# Keep the final cleanup cadence based on the tick timestamp. (v1.5.1 CI revision)
s = s.replace("_nextBodyguardDismissalUtc = DateTime.UtcNow.AddMilliseconds(700);", "_nextBodyguardDismissalUtc = now.AddMilliseconds(700);")
for p in (root / "tests").glob("test_*.py"):
    p.write_text(p.read_text(encoding="utf-8-sig").replace('ProductVersion = "1.5.0"', 'ProductVersion = "1.5.1"'),
                 encoding="utf-8-sig", newline="")

def test_replace(rel, old, new):
    replace(rel, old, new)

test_replace("tests/test_1_4_6_regressions.py", 'self.assertIn("MarkAsNoLongerNeeded", body)', 'self.assertIn("guard.Delete();", body)')
test_replace("tests/test_1_4_6_regressions.py", r'ProductVersion\\s*=\\s*"1\\.5\\.0"', r'ProductVersion\\s*=\\s*"1\\.5\\.1"')
test_replace("tests/test_1_4_7_regressions.py", 'self.assertIn("REMOVE_PED_FROM_GROUP", dismiss)', 'self.assertIn("guard.Delete();", dismiss)')
test_replace("tests/test_1_4_7_regressions.py", 'self.assertNotIn("CLEAR_PED_TASKS", dismiss)', 'self.assertIn("CLEAR_PED_TASKS_IMMEDIATELY", dismiss)')
test_replace("tests/test_1_4_7_regressions.py", 'self.assertIn("AddMilliseconds(650)", dismiss)', 'self.assertIn("AddMilliseconds(700)", dismiss)')
test_replace("tests/test_1_4_7_regressions.py", 'self.assertIn("Math.Abs(forwardSpeed) < 45.0f", stability)', 'self.assertIn("Math.Abs(forwardSpeed) < 25.0f", stability)')
test_replace("tests/test_1_4_7_regressions.py", 'self.assertIn("lateralSpeed < 10.0f", stability)', 'self.assertIn("lateralSpeed < 4.0f", stability)')
test_replace("tests/test_1_4_7_regressions.py", 'self.assertIn("lateralSpeed > 25.0f ? 0.96f : 0.985f", stability)', 'self.assertIn("lateralSpeed > 15.0f ? 0.90f : 0.96f", stability)')
test_replace("tests/test_1_4_8_regressions.py", 'self.assertIn("lateralSpeed < 10.0f", stability)', 'self.assertIn("lateralSpeed < 4.0f", stability)')
test_replace("tests/test_1_4_8_regressions.py", 'self.assertIn("lateralSpeed > 25.0f ? 0.96f : 0.985f", stability)', 'self.assertIn("lateralSpeed > 15.0f ? 0.90f : 0.96f", stability)')
# Replace old group-table dismissal assertions with deletion contract.
p = root / "tests/test_1_4_8_regressions.py"
s = p.read_text(encoding="utf-8-sig")
s = re.sub(r'    def test_dismissal_checks_group_membership_before_reading_group_index\(self\):[\s\S]*?(?=    def test_abort_detaches_script_events)', '''    def test_dismissal_checks_group_membership_before_reading_group_index(self):
        dismiss = body(CORE, "private void ProcessPendingBodyguardDismissals")
        self.assertIn("guard.Delete();", dismiss)
        self.assertIn("RemoveBodyguardBlip(handle);", dismiss)
        self.assertNotIn("GET_PED_GROUP_INDEX", dismiss)
        self.assertNotIn("REMOVE_PED_FROM_GROUP", dismiss)

''', s, count=1)
s = s.replace('''        self.assertIn("GET_PED_GROUP_INDEX", dismiss)
        self.assertIn("IS_PED_IN_GROUP", dismiss)
        self.assertIn("REMOVE_PED_FROM_GROUP", dismiss)
        self.assertIn("DOES_GROUP_EXIST", dismiss)''', '''        self.assertIn("guard.Delete();", dismiss)
        self.assertIn("RemoveBodyguardBlip(handle);", dismiss)''')
s = s.replace('self.assertIn("TASK_WANDER_STANDARD", dismiss)', 'self.assertIn("CLEAR_PED_TASKS_IMMEDIATELY", dismiss)')
s = s.replace('self.assertIn("bool stillInGroup", dismiss)', 'self.assertNotIn("bool stillInGroup", dismiss)')
s = s.replace('self.assertIn("finalGroup == _bodyguardGroupId", dismiss)', 'self.assertNotIn("finalGroup == _bodyguardGroupId", dismiss)')
s = s.replace('self.assertIn("now.AddMilliseconds(900)", dismiss)', 'self.assertIn("now.AddMilliseconds(700)", dismiss)')
s = s.replace('self.assertNotIn("CLEAR_PED_TASKS", dismiss)', 'self.assertIn("CLEAR_PED_TASKS_IMMEDIATELY", dismiss)')
p.write_text(s, encoding="utf-8-sig", newline="")

# Hotfix/user-reported tests track deletion rather than delayed group release.
p = root / "tests/test_1_4_8_hotfix_regressions.py"
s = p.read_text(encoding="utf-8-sig")
s = re.sub(r'    def test_dismissal_defers_group_read_until_later_tick\(self\):[\s\S]*?(?=    def test_automatic_parachute)', '''    def test_dismissal_defers_group_read_until_later_tick(self):
        start = MAIN.index("private void ProcessPendingBodyguardDismissals()")
        end = MAIN.index("private void ReleaseBodyguardsOnScriptAbort()", start)
        body = MAIN[start:end]
        self.assertIn("guard.Delete();", body)
        self.assertIn("RemoveBodyguardBlip(handle);", body)
        self.assertIn("_pendingBodyguardDismissals.Enqueue(handle);", body)
        self.assertNotIn("bool stillInGroup", body)

''', s, count=1)
p.write_text(s, encoding="utf-8-sig", newline="")
p = root / "tests/test_1_4_8_user_reported_fixes.py"
s = p.read_text(encoding="utf-8-sig")
s = s.replace('''        self.assertIn("_bodyguardDismissalReleaseAfterUtc", dismiss)
        self.assertIn("AddSeconds(1.5)", dismiss)
        self.assertIn("REMOVE_PED_FROM_GROUP", dismiss)''', '''        self.assertIn("guard.Delete();", dismiss)
        self.assertIn("RemoveBodyguardBlip(handle);", dismiss)
        self.assertIn("_pendingBodyguardDismissals.Enqueue(handle);", dismiss)''')
p.write_text(s, encoding="utf-8-sig", newline="")

# Release validator and packager must include 1.5.1 artifacts and validate the new deletion path.
p = root / "build/ReleaseValidator.cs"
s = p.read_text(encoding="utf-8-sig").replace('ProductVersion = "1.5.0"', 'ProductVersion = "1.5.1"')
s = s.replace('"docs/CHANGELOG_1_5_0_RU.md", "docs/TEST_REPORT_1_5_0_RU.md", "tests/test_1_5_0_regressions.py",',
              '"docs/CHANGELOG_1_5_0_RU.md", "docs/TEST_REPORT_1_5_0_RU.md", "tests/test_1_5_0_regressions.py", "docs/CHANGELOG_1_5_1_RU.md", "docs/TEST_REPORT_1_5_1_RU.md", "tests/test_1_5_1_regressions.py",')
s = s.replace('mainSource.IndexOf("AddMilliseconds(700)", StringComparison.Ordinal) < 0',
              'mainSource.IndexOf("guard.Delete();", StringComparison.Ordinal) < 0 || mainSource.IndexOf("AddMilliseconds(700)", StringComparison.Ordinal) < 0')
s = s.replace('''            mainSource.IndexOf("TASK_WANDER_STANDARD", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("bool stillInGroup", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("finalGroup == _bodyguardGroupId", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("DOES_GROUP_EXIST", StringComparison.Ordinal) < 0 ||''', '''            mainSource.IndexOf("guard.Delete();", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("RemoveBodyguardBlip(handle);", StringComparison.Ordinal) < 0 ||''')
p.write_text(s, encoding="utf-8-sig", newline="")
p = root / "build/ReleasePackager.cs"
s = p.read_text(encoding="utf-8-sig").replace('archiveRootName + "/docs/CHANGELOG_1_5_0_RU.md", archiveRootName + "/docs/TEST_REPORT_1_5_0_RU.md",',
'''archiveRootName + "/docs/CHANGELOG_1_5_0_RU.md", archiveRootName + "/docs/TEST_REPORT_1_5_0_RU.md",
                    archiveRootName + "/docs/CHANGELOG_1_5_1_RU.md", archiveRootName + "/docs/TEST_REPORT_1_5_1_RU.md",
                    archiveRootName + "/tests/test_1_5_1_regressions.py",''')
p.write_text(s, encoding="utf-8-sig", newline="")

(root / "docs/CHANGELOG_1_5_1_RU.md").write_text("""# AvernesSowtware 1.5.1

- Массовое удаление телохранителей удаляет NPC после безопасного выхода из транспорта; обработка сериализована, ошибки сохраняют задачу в очереди.
- Исправлена обработка индекса основной палитры -1: используется резервный индекс GET_VEHICLE_COLOURS, если он доступен.
- Исправлена сигнатура SPECIAL_ABILITY_ACTIVATE для Franklin в Toreador.
- Nitro мотоциклов больше не добавляет torque boost; запрос коллизий учитывает высоту траектории в воздухе.
- Для авиации добавлен vehicle-level speed cap; High-Speed Stability начинает корректировать боковое скольжение при меньших порогах.

Игровая проверка GTA V Legacy обязательна для подтверждения отсутствия native crash, поведения NPC, текстуры парашюта, скорости авиации и управляемости транспорта.
""", encoding="utf-8")
(root / "docs/TEST_REPORT_1_5_1_RU.md").write_text("""# Проверка v1.5.1

- Regression suite проверяет удаление NPC, повторную очередь, fallback палитры -1, вызов способности Franklin, мотоциклетный Nitro и vehicle-level aircraft cap.
- Windows CI выполняет компиляцию DLL, ReleaseValidator, упаковку и установочную миграцию.
- Предоставленный crash.log не содержит native stack; точную причину прежнего завершения процесса можно подтвердить только игровым воспроизведением и crash dump.
- Парашютная текстура не изменяется текущим исходным кодом; исправление требует диагностики конкретной модели/игрового ресурса.
""", encoding="utf-8")

(root / "tests/test_1_5_1_regressions.py").write_text("""from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
CORE=(ROOT/"src/Core/AvernesSowtwareScript.cs").read_text(encoding="utf-8-sig")
VEHICLE=(ROOT/"src/Managers/VehicleSystems.cs").read_text(encoding="utf-8-sig")
VALIDATOR=(ROOT/"build/ReleaseValidator.cs").read_text(encoding="utf-8-sig")
class Release151Regressions(unittest.TestCase):
    def test_version(self): self.assertIn('ProductVersion = "1.5.1"', CORE)
    def test_bodyguards_are_deleted_and_retried(self):
        b=CORE[CORE.index("private void ProcessPendingBodyguardDismissals"):CORE.index("private void ReleaseBodyguardsOnScriptAbort")]
        self.assertIn("guard.Delete();",b); self.assertIn("RemoveBodyguardBlip(handle);",b)
        self.assertNotIn("guard.MarkAsNoLongerNeeded();",b); self.assertIn("_pendingBodyguardDismissals.Enqueue(handle);",b)
    def test_palette_minus_one_fallback(self):
        self.assertIn("vehiclePrimaryColor >= 0",VEHICLE); self.assertIn("GET_VEHICLE_COLOURS",VEHICLE)
    def test_toreador_ability_signature(self):
        self.assertIn("0x821FDC827D6F4090UL, Game.Player);",CORE)
    def test_motorcycle_nitro_and_aircraft_speed_cap(self):
        self.assertIn("motorcycle ? 1.0f",VEHICLE)
        self.assertIn("0xBAA045B4E42F3C06UL, handle, maximumNitroSpeed",VEHICLE)
    def test_stability_threshold(self):
        self.assertIn("Math.Abs(forwardSpeed) < 25.0f",VEHICLE)
    def test_release_assets(self):
        self.assertIn('"docs/CHANGELOG_1_5_1_RU.md"',VALIDATOR)
if __name__=="__main__": unittest.main()
""", encoding="utf-8")
