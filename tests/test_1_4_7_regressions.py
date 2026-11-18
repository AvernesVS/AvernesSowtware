from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
CORE = (ROOT / "src/Core/AvernesSowtwareScript.cs").read_text(encoding="utf-8-sig")
VEHICLE = (ROOT / "src/Managers/VehicleSystems.cs").read_text(encoding="utf-8-sig")
PERSISTENCE = (ROOT / "src/Core/SettingsPersistence.cs").read_text(encoding="utf-8-sig")

def body(source, signature):
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 0
    in_string = False
    escaped = False
    for i in range(opening, len(source)):
        ch = source[i]
        if in_string:
            if escaped:
                escaped = False
            elif ch == "\\":
                escaped = True
            elif ch == '"':
                in_string = False
            continue
        if ch == '"':
            in_string = True
        elif ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return source[opening:i + 1]
    raise AssertionError(signature)

class Release147Tests(unittest.TestCase):
    def test_version_and_log_names(self):
        self.assertIn('ProductVersion = "1.4.7"', CORE)
        for name in ('"session.log"', '"error.log"', '"crash.log"'):
            self.assertIn(name, CORE)

    def test_logs_stay_in_scripts_directory(self):
        resolver = body(PERSISTENCE, "private static string ResolveWritableLogDirectory")
        self.assertIn('Path.Combine(scriptsDirectory, "AvernesSowtware_Logs")', resolver)
        self.assertNotIn("LocalApplicationData", resolver)
        self.assertNotIn("MyDocuments", resolver)
        writer = body(CORE, "private void AppendLogFile")
        self.assertIn("attempt < 3", writer)
        self.assertNotIn("fallbackDir", writer)

    def test_max_tuning_airborne_guard_and_native_removal(self):
        tune = body(VEHICLE, "private void MaxTuneVehicle")
        self.assertIn("Land the vehicle before applying Max Tuning", tune)
        matte = body(VEHICLE, "private void ApplyMattePaintPreservingColor")
        self.assertNotIn("0x81592BE4E4746D88UL", matte)
        self.assertIn("GET_VEHICLE_COLOURS", matte)

    def test_toreador_nitro_and_airborne_impulse_guard(self):
        effects = body(VEHICLE, "private void ApplyLegacyVehicleEffects")
        self.assertIn('GetHash("toreador")', effects)
        assist = body(VEHICLE, "private void ApplyNitroVelocityAssist(int vehicleHandle, float maximumSpeed, float nitroPower)")
        self.assertIn("if (airborne) return", assist)

    def test_wash_and_stability_controls(self):
        self.assertIn("WashCurrentVehicle", CORE)
        self.assertIn("High-Speed Stability", CORE)
        self.assertIn("ApplyVehicleStabilityAssist", VEHICLE)
        self.assertIn("VehicleStability", PERSISTENCE)

if __name__ == "__main__":
    unittest.main()
