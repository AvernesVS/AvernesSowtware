from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
CORE = (ROOT / "src/Core/AvernesSowtwareScript.cs").read_text(encoding="utf-8-sig")
VEHICLE = (ROOT / "src/Managers/VehicleSystems.cs").read_text(encoding="utf-8-sig")
VALIDATOR = (ROOT / "build/ReleaseValidator.cs").read_text(encoding="utf-8-sig")


class Release152Regressions(unittest.TestCase):
    def test_version(self):
        self.assertIn('ProductVersion = "1.5.2"', CORE)

    def test_bodyguard_dismissal_releases_instead_of_deleting(self):
        body = CORE[CORE.index("private void ProcessPendingBodyguardDismissals"):CORE.index("private void ReleaseBodyguardsOnScriptAbort")]
        self.assertIn("guard.MarkAsNoLongerNeeded();", body)
        self.assertIn("REMOVE_PED_FROM_GROUP", body)
        self.assertIn("CLEAR_PED_TASKS", body)
        self.assertNotIn("guard.Delete();", body)
        self.assertNotIn("CLEAR_PED_TASKS_IMMEDIATELY", body)

    def test_aimed_npc_dodge_and_weapon_drop_controls(self):
        aim = CORE[CORE.index("private void EnsurePlayerCanDamageAimedNpc"):CORE.index("private int GetPlayerAimedNpcHandle")]
        self.assertIn("SET_PED_CAN_EVASIVE_DIVE", aim)
        self.assertIn("SET_PED_DROPS_WEAPONS_WHEN_DEAD", aim)
        self.assertIn("restore SET_PED_CAN_EVASIVE_DIVE", aim)

    def test_bodyguards_do_not_drop_weapons(self):
        tactics = CORE[CORE.index("private void ApplyBodyguardCombatTactics"):CORE.index("private bool IsLivingBodyguardHandle")]
        self.assertIn("SET_PED_DROPS_WEAPONS_WHEN_DEAD", tactics)

    def test_nitro_vehicle_cap_applies_to_all_classes(self):
        nitro = VEHICLE[VEHICLE.index("private void ApplyLegacyVehicleEffects"):VEHICLE.index("private void ApplyVehicleStabilityAssist")]
        self.assertIn("SET_VEHICLE_MAX_SPEED", nitro)
        self.assertIn("motorcycle ? 1.85f", nitro)
        self.assertIn("aircraft ? 2.50f", nitro)
        self.assertIn("24.0f + ClampFloat(nitroPower", VEHICLE)

    def test_fort_zancudo_suppression_is_not_gated_by_global_wanted_toggle(self):
        self.assertIn("SuppressActiveMilitaryCombat(ped);", CORE)
        self.assertNotIn("if (ignoreLawAndMilitary) SuppressActiveMilitaryCombat(ped);", CORE)

    def test_parachute_auto_deploy_remains_disabled(self):
        body = CORE[CORE.index("private void ProcessAutomaticParachuteDeployment"):CORE.index("private void StartPlayerScenario")]
        self.assertIn("_parachuteAutoDeploymentRequested = false;", body)
        self.assertNotIn("FORCE_PED_TO_OPEN_PARACHUTE", CORE)
        loadout = CORE[CORE.index("private void GiveFullWeaponLoadout(Ped ped)"):CORE.index("private void ApplyWeaponFinishToCurrentLoadout")]
        self.assertIn('String.Equals(_weaponNames[i], "WEAPON_PARACHUTE"', loadout)
        self.assertIn("private void GiveParachuteToPlayer()", CORE)

    def test_shutdown_exit_path_avoids_forced_flush(self):
        body = CORE[CORE.index("private void OnDomainProcessExit"):CORE.index("private static string[] BuildWeaponCatalog")]
        self.assertIn("File.AppendAllText", body)
        self.assertNotIn("AppendLogFile(", body)
        self.assertNotIn("Flush(true)", body)

    def test_release_validator_targets_152(self):
        self.assertIn('ProductVersion = \\"1.5.2\\"', VALIDATOR)


if __name__ == "__main__":
    unittest.main()
