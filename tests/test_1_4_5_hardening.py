from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
CORE = (ROOT / "src/Core/AvernesSowtwareScript.cs").read_text(encoding="utf-8-sig")
VEHICLE = (ROOT / "src/Managers/VehicleSystems.cs").read_text(encoding="utf-8-sig")
STORAGE = (ROOT / "src/Core/SettingsPersistence.cs").read_text(encoding="utf-8-sig")


def method_body(source: str, signature: str) -> str:
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
    raise AssertionError(f"Unclosed method: {signature}")


class Hardening145Tests(unittest.TestCase):
    def test_nitro_uses_1200_kmh_target_and_one_shot_vehicle_burst(self):
        self.assertIn("return ClampFloat(nitroPower, 0.5f, 8.0f) * (150.0f / 3.6f);", VEHICLE)
        self.assertIn("SET_VEHICLE_FORWARD_SPEED", VEHICLE)
        self.assertIn("if (vehicleHandle == 0 || !IsVehicleHandleUsable(vehicleHandle) || _nitroBurstApplied) return;", VEHICLE)
        self.assertIn("ApplyNitroVelocityAssist(handle, maximumNitroSpeed);", VEHICLE)
        self.assertIn("_nitroBurstApplied = false;", VEHICLE)

    def test_bodyguards_have_evasive_dive_cower_cover_and_bullet_reactions_disabled(self):
        body = method_body(CORE, "private void ApplyBodyguardCombatTactics(int handle)")
        self.assertIn("SET_PED_CAN_EVASIVE_DIVE", body)
        self.assertIn("handle, false", body)
        self.assertIn("SET_PED_CAN_COWER_IN_COVER", body)
        self.assertIn("SET_PED_CAN_PEEK_IN_COVER", body)
        self.assertIn("handle, 38, true", body)
        self.assertIn("SET_PED_COMBAT_MOVEMENT", body)

    def test_mass_dismissal_is_two_phase_and_never_deletes_entities(self):
        body = method_body(CORE, "private void ProcessPendingBodyguardDismissals()")
        self.assertIn("_bodyguardDismissalDetachedHandles.Contains(handle)", body)
        self.assertIn("REMOVE_PED_FROM_GROUP", body)
        self.assertIn("MarkAsNoLongerNeeded", body)
        self.assertNotIn("Function.Call(Hash.DELETE_ENTITY", body)
        self.assertNotIn("0xAE3CBE5BF394C9C9", body)

    def test_bodyguard_door_open_precedes_animated_entry(self):
        self.assertIn("TASK_OPEN_VEHICLE_DOOR", CORE)
        self.assertIn("_bodyguardBoardingEnterAtUtc[handle] = DateTime.UtcNow.AddMilliseconds(900);", CORE)
        self.assertIn("TASK_ENTER_VEHICLE: normal animated entry", CORE)

    def test_vehicle_god_mode_does_not_call_full_repair_during_door_animation(self):
        body = method_body(VEHICLE, "private void RepairVehicleForGodMode(int vehicleHandle)")
        self.assertNotIn("SET_VEHICLE_FIXED", body)
        self.assertIn("SET_VEHICLE_DEFORMATION_FIXED", body)
        self.assertIn("FIX_VEHICLE_WINDOW", body)
        self.assertIn("AddMilliseconds(3000)", VEHICLE)

    def test_saved_vehicle_paint_operations_are_independently_guarded(self):
        start = CORE.index("private void ApplySavedVehicleTuning")
        end = CORE.index("private void DeleteSavedVehicle", start)
        body = CORE[start:end]
        self.assertGreaterEqual(body.count("catch (Exception ex) { LogException(\"Restore saved"), 8)
        self.assertIn("Restore saved custom primary RGB", body)
        self.assertIn("Restore saved custom secondary RGB", body)

    def test_each_vehicle_slot_has_own_json_and_legacy_aggregate_is_retained(self):
        self.assertIn("slot_\" + item.Slot.ToString(\"00\"", CORE)
        self.assertIn("_savedVehiclesManifestPath", CORE)
        self.assertIn("saved_vehicles.json", CORE)
        self.assertIn("One-time migration", CORE)
        self.assertIn("TryAtomicWriteFile(_savedVehiclesPath", CORE)

    def test_saved_vehicle_menu_is_grouped_into_five_pages(self):
        body = method_body(CORE, "private void RebuildSavedVehiclesMenu()")
        self.assertIn("group < 5", body)
        self.assertIn("Save current vehicle to next free slot", body)
        self.assertIn("Spawn saved vehicle", body)
        self.assertIn("Overwrite from current vehicle", body)
        self.assertIn("Delete this slot", body)

    def test_high_jump_suppresses_automatic_parachute(self):
        body = method_body(CORE, "private void ProcessAutomaticParachuteDeployment(Ped ped)")
        self.assertIn("if (_settings.SuperJump)", body)
        self.assertLess(body.index("if (_settings.SuperJump)"), body.index("FORCE_PED_TO_OPEN_PARACHUTE"))

    def test_fort_zancudo_only_redirects_hostile_aircraft(self):
        body = method_body(CORE, "private void SuppressActiveMilitaryCombat(Ped player)")
        self.assertIn("if (!attackingPlayer) continue;", body)
        self.assertIn("TASK_PLANE_MISSION", body)
        self.assertIn("TASK_HELI_MISSION", body)
        self.assertNotIn("if (!attackingPlayer && !pilotInAircraft) continue;", body)

    def test_franklin_ability_is_reenabled_while_driving(self):
        body = method_body(CORE, "private void ApplyPlayerSettings(Ped ped)")
        self.assertIn('currentPlayerModel == GetHash("player_one")', body)
        self.assertIn("ENABLE_SPECIAL_ABILITY", body)

    def test_logs_use_writable_directory_and_separate_error_crash_files(self):
        self.assertIn("ResolveWritableLogDirectory", STORAGE)
        self.assertIn('"AvernesSowtware_Logs"', STORAGE)
        self.assertIn('"AvernesSowtware_errors.log"', CORE)
        self.assertIn('"AvernesSowtware_crashes.log"', CORE)
        append = method_body(CORE, "private void AppendLogFile(string path, string content)")
        self.assertNotIn("Flush(true)", append)

    def test_non_hostile_aircraft_and_911_traffic_are_not_blanket_cleared(self):
        body = method_body(CORE, "private void SuppressActiveMilitaryCombat(Ped player)")
        self.assertIn("Never clear ambient aircraft/dispatch tasks", body)
        self.assertIn("if (!attackingPlayer) continue;", body)


if __name__ == "__main__":
    unittest.main(verbosity=2)
