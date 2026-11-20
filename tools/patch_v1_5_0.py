from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve()

def replace(rel, old, new, count=1):
    path = root / rel
    text = path.read_text(encoding="utf-8-sig")
    found = text.count(old)
    if found < count:
        raise SystemExit(f"{rel}: expected at least {count} occurrence(s), found {found}: {old!r}")
    path.write_text(text.replace(old, new, count), encoding="utf-8-sig", newline="")

# 1200 km/h = 333.333 m/s. Keep frame-time-bounded velocity impulses.
replace("src/Managers/VehicleSystems.cs",
        "float maximumNitroSpeed = aircraft ? 300.0f : CalculateNitroMaximumSpeedMetersPerSecond(nitroPower);",
        "float maximumNitroSpeed = aircraft ? 333.333f : CalculateNitroMaximumSpeedMetersPerSecond(nitroPower);")
replace("src/Managers/VehicleSystems.cs",
        "float entitySpeedCap = aircraft ? 320.0f : maximumNitroSpeed + 1.0f;",
        "float entitySpeedCap = maximumNitroSpeed + 1.0f;")
replace("src/Managers/VehicleSystems.cs",
        "// Keep land vehicles below the unstable high-speed collision boundary.\n            // Aircraft use a separate, higher envelope in ApplyLegacyVehicleEffects.\n            return 206.94f; // 745 km/h; frame-time-scaled velocity ceiling.",
        "// Common target for land and air vehicles: 1200 km/h.\n            // The frame-time-limited velocity assist remains in place; GTA collision/streaming\n            // stability at this speed still requires in-game validation.\n            return 333.333f; // 1200 km/h; frame-time-scaled velocity ceiling.")
replace("build/ReleaseValidator.cs", "return 206.94f", "return 333.333f")
replace("build/ReleaseValidator.cs", "aircraft ? 300.0f", "aircraft ? 333.333f")
replace("build/ReleaseValidator.cs", 'ProductVersion = "1.4.9"', 'ProductVersion = "1.5.0"')
\n# Register the new release documentation and regression suite with validator and packager.\nreplace("build/ReleaseValidator.cs", '"docs/CHANGELOG_1_4_9_RU.md", "docs/TEST_REPORT_1_4_9_RU.md", "tests/test_1_4_9_regressions.py",', '"docs/CHANGELOG_1_4_9_RU.md", "docs/TEST_REPORT_1_4_9_RU.md", "tests/test_1_4_9_regressions.py", "docs/CHANGELOG_1_5_0_RU.md", "docs/TEST_REPORT_1_5_0_RU.md", "tests/test_1_5_0_regressions.py",')\nreplace("build/ReleasePackager.cs", '"docs/CHANGELOG_1_4_9_RU.md", "docs/TEST_REPORT_1_4_9_RU.md",', '"docs/CHANGELOG_1_4_9_RU.md", "docs/TEST_REPORT_1_4_9_RU.md",\n                    archiveRootName + "/docs/CHANGELOG_1_5_0_RU.md", archiveRootName + "/docs/TEST_REPORT_1_5_0_RU.md",\n                    archiveRootName + "/tests/test_1_5_0_regressions.py",')\nreplace("src/Core/AvernesSowtwareScript.cs", 'ProductVersion = "1.4.9"', 'ProductVersion = "1.5.0"')

# Update current release entry points, not historical changelogs.
for rel in ["BUILD_RELEASE.bat", "build/Build.bat", "Install_AvernesSowtware.bat",
            "Uninstall_AvernesSowtware.bat", "README_RU.md", "START_HERE_RU.txt",
            "dist/README_RU.txt", "BUILD_RELEASE_REPORT_RU.txt"]:
    p = root / rel
    p.write_text(p.read_text(encoding="utf-8-sig").replace("1.4.9", "1.5.0"),
                 encoding="utf-8-sig", newline="")

# Retain all previous regression tests, updating only their current-release expectations.
for p in (root / "tests").glob("test_1_4_*.py"):
    s = p.read_text(encoding="utf-8-sig")
    s = s.replace('ProductVersion = "1.4.9"', 'ProductVersion = "1.5.0"')
    s = s.replace(r'ProductVersion\s*=\s*"1\.4\.9"', r'ProductVersion\s*=\s*"1\.5\.0"')
    s = s.replace("return 206.94f", "return 333.333f")
    s = s.replace("aircraft ? 300.0f", "aircraft ? 333.333f")
    p.write_text(s, encoding="utf-8-sig", newline="")

(root / "tests/test_1_5_0_regressions.py").write_text('''from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[1]
CORE = (ROOT / "src/Core/AvernesSowtwareScript.cs").read_text(encoding="utf-8-sig")
VEHICLE = (ROOT / "src/Managers/VehicleSystems.cs").read_text(encoding="utf-8-sig")
VALIDATOR = (ROOT / "build/ReleaseValidator.cs").read_text(encoding="utf-8-sig")
class Release150Regressions(unittest.TestCase):
    def test_release_version_is_150(self):
        self.assertIn('ProductVersion = "1.5.0"', CORE)
    def test_nitro_target_is_1200_kmh_for_land_and_aircraft(self):
        for source in (VEHICLE, VALIDATOR):
            self.assertIn("return 333.333f", source)
            self.assertIn("aircraft ? 333.333f", source)
    def test_nitro_retains_frame_time_bounded_velocity_assist(self):
        start = VEHICLE.index("private void ApplyNitroVelocityAssist(int vehicleHandle, float maximumSpeed, float nitroPower)")
        end = VEHICLE.index("private void RestoreNitroSpeedLimit", start)
        method = VEHICLE[start:end]
        self.assertIn("GET_FRAME_TIME", method)
        self.assertIn("ClampFloat(deltaSeconds, 0.001f, 0.033f)", method)
        self.assertIn("SET_ENTITY_VELOCITY", method)
        self.assertNotIn("SET_VEHICLE_FORWARD_SPEED", method)
    def test_entity_speed_cap_tracks_requested_envelope(self):
        self.assertIn("float entitySpeedCap = maximumNitroSpeed + 1.0f", VEHICLE)
        self.assertIn("SET_ENTITY_MAX_SPEED", VEHICLE)
    def test_stability_assist_does_not_compete_with_nitro(self):
        self.assertIn("_settings.VehicleStability && !nitroRequested", VEHICLE)
    def test_existing_critical_systems_remain_present(self):
        for marker in ["ProcessPendingBodyguardDismissals", "ProcessBodyguardFollowing",
                       "ProcessAutomaticParachuteDeployment", "SuppressActiveMilitaryCombat",
                       "SuppressBolingbrokePrisonCombat", "ApplyPoliceIgnore",
                       'GetHash("toreador")', "EnsureFullscreenWindow"]:
            self.assertIn(marker, CORE)
if __name__ == "__main__": unittest.main()
''', encoding="utf-8")

(root / "docs/CHANGELOG_1_5_0_RU.md").write_text("""# AvernesSowtware 1.5.0 — Nitro X8\n\n- Целевая скорость Nitro X8 установлена на 1200 км/ч (333,333 м/с) для наземного и воздушного транспорта.\n- Сохранён ограниченный по времени кадра импульс SET_ENTITY_VELOCITY; резкие SET_VEHICLE_FORWARD_SPEED и телепортация скорости не добавлялись.\n- SET_ENTITY_MAX_SPEED теперь рассчитывается от целевого значения для всех классов.\n- Существующие подсистемы телохранителей, полиции, Fort Zancudo, Bolingbroke, парашюта, Toreador, логирования и полноэкранного режима сохранены.\n\n1200 км/ч — целевое значение разгона, а не подтверждённая безопасная скорость GTA V Legacy. Отсутствие невидимой стены, выброса транспорта, потери управления и проблем стриминга требует игрового теста.\n""", encoding="utf-8")
(root / "docs/TEST_REPORT_1_5_0_RU.md").write_text("""# Проверка AvernesSowtware 1.5.0\n\n- Regression suite проверяет версию, целевую скорость 1200 км/ч, frame-time clamp, velocity assist и наличие критических подсистем.\n- Windows CI собирает DLL и полный ZIP.\n- GTA V Legacy runtime не подтверждается автоматически; необходим ручной тест в Story Mode.\n\nДо игрового прогона не считать подтверждёнными: устойчивость на 1200 км/ч, отсутствие невидимой стены/подброса, NPC Fort Zancudo и Bolingbroke, телохранители, анимации, текстура парашюта, Toreador и Newer Wanted.\n""", encoding="utf-8")
