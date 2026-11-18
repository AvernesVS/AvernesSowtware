from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
VEHICLE = (ROOT / 'src' / 'Managers' / 'VehicleSystems.cs').read_text(encoding='utf-8-sig')
MAIN = (ROOT / 'src' / 'Core' / 'AvernesSowtwareScript.cs').read_text(encoding='utf-8-sig')


class Release145VehicleEntryRegressionTests(unittest.TestCase):
    def test_vehicle_god_mode_checks_native_entry_state(self):
        self.assertIn('0xBB062B2B5722478EUL', VEHICLE)
        self.assertIn('playerEnteringVehicle', VEHICLE)

    def test_initial_repair_is_deferred_until_entry_finishes(self):
        self.assertIn('_pendingGodModeRepairVehicleHandle = currentHandle', VEHICLE)
        self.assertIn('AddMilliseconds(1200)', VEHICLE)
        self.assertIn('DateTime.UtcNow >= _pendingGodModeRepairAtUtc && !playerEnteringVehicle', VEHICLE)

    def test_deferred_repair_is_cancelled_when_vehicle_or_setting_changes(self):
        self.assertIn('_pendingGodModeRepairVehicleHandle != currentHandle', VEHICLE)
        self.assertIn('else if (_pendingGodModeRepairVehicleHandle == currentHandle)', VEHICLE)
        self.assertIn('_pendingGodModeRepairVehicleHandle = 0', VEHICLE)

    def test_repair_remains_one_shot_and_contains_full_repair(self):
        start = VEHICLE.index('private void RepairVehicleForGodMode')
        end = VEHICLE.index('private void RestoreVehicleProtection', start)
        body = VEHICLE[start:end]
        self.assertIn('Hash.SET_VEHICLE_FIXED', body)
        self.assertIn('Hash.SET_VEHICLE_DEFORMATION_FIXED', body)
        self.assertIn('0x772282EBEB95E682UL', body)

    def test_version_is_145_and_existing_features_remain(self):
        self.assertIn('ProductVersion = "1.4.5"', MAIN)
        self.assertIn('ApplyBodyguardCombatTactics', MAIN)
        self.assertIn('SuppressActiveMilitaryCombat', MAIN)


if __name__ == '__main__':
    unittest.main()
