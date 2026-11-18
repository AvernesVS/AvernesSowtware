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
        self.assertNotIn("0xBAA045B4E42F3C06UL, handle, maximumNitroSpeed", effects)
        self.assertIn("MODIFY_VEHICLE_TOP_SPEED", effects)

    def test_wash_and_stability_controls(self):
        self.assertIn("WashCurrentVehicle", CORE)
        self.assertIn("High-Speed Stability", CORE)
        self.assertIn("ApplyVehicleStabilityAssist", VEHICLE)
        self.assertIn("VehicleStability", PERSISTENCE)


    def test_bodyguards_use_distinct_follow_formation(self):
        follow = body(CORE, "private void IssueBodyguardFollowTask")
        self.assertIn("_bodyguards.IndexOf(pedHandle)", follow)
        self.assertIn("formationIndex % 2", follow)
        self.assertIn("formationIndex / 2", follow)
        self.assertIn("offsetX, offsetY", follow)
        self.assertEqual(CORE.count("Function.Call((Hash)0x304AE42E357B8C7EUL"), 1)
        self.assertIn("IsScriptTaskActive(handle, (Hash)0x3EF867F4UL)", CORE)
        self.assertIn("IsScriptTaskActive(handle, (Hash)0xAA05B492UL)", CORE)

    def test_bodyguard_dismissal_is_serialized(self):
        dismiss = body(CORE, "private void ProcessPendingBodyguardDismissals")
        self.assertIn("AddMilliseconds(650)", dismiss)
        self.assertIn("_bodyguardDismissalGroupRemovedHandles", dismiss)
        self.assertNotIn("DeleteEntity", dismiss)

    def test_fort_zancudo_suppresses_army_without_disabling_police_heli(self):
        dispatch = body(CORE, "private void UpdateFortZancudoArmyDispatch")
        restore = body(CORE, "private void RestoreFortZancudoArmyDispatch")
        self.assertIn("0xDC0F817884CDD856UL, 14, false", dispatch)
        self.assertIn("0x9B2BD3773123EA2FUL, 14, true", dispatch)
        self.assertNotIn(", 2,", dispatch)
        self.assertIn("0xDC0F817884CDD856UL, 14, true", restore)
        self.assertIn("0x9B2BD3773123EA2FUL, 14, false", restore)
        self.assertNotIn(", 2,", restore)

    def test_auto_parachute_uses_gadget_item(self):
        parachute = body(CORE, "private void ProcessAutomaticParachuteDeployment")
        self.assertIn('GetHash("GADGET_PARACHUTE")', parachute)
        self.assertIn("0x8DECB02F88F428BCUL", parachute)
        self.assertNotIn('GetHash("WEAPON_PARACHUTE")', parachute)

    def test_vehicle_god_mode_cleans_dirt_and_decals(self):
        initial = body(VEHICLE, "private void RepairVehicleForGodMode")
        refresh = body(VEHICLE, "private void RefreshVehicleGodModeDamage")
        for source in (initial, refresh):
            self.assertIn("SET_VEHICLE_DIRT_LEVEL", source)
            self.assertIn("0xE91F1B65F2B48D57UL", source)
            self.assertIn("SET_VEHICLE_DEFORMATION_FIXED", source)
        self.assertIn("IS_VEHICLE_DAMAGED", refresh)
        self.assertIn("SET_VEHICLE_FIXED", refresh)

if __name__ == "__main__":
    unittest.main()
