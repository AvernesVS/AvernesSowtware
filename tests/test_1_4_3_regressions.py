from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / 'src' / 'Core' / 'AvernesSowtwareScript.cs').read_text(encoding='utf-8-sig')

class Release143BaselineTests(unittest.TestCase):
    def test_source_contains_bodyguard_deferred_dismissal(self):
        self.assertIn('ProcessPendingBodyguardDismissals', SOURCE)
        self.assertIn('ReleaseBodyguardsOnScriptAbort', SOURCE)

    def test_source_contains_vehicle_protection_and_door_animation(self):
        vehicle = (ROOT / 'src' / 'Managers' / 'VehicleSystems.cs').read_text(encoding='utf-8-sig')
        self.assertIn('SET_ENTITY_PROOFS', vehicle)
        self.assertIn('TASK_OPEN_VEHICLE_DOOR', SOURCE)

    def test_bodyguards_are_configured_for_offensive_combat(self):
        self.assertIn('ApplyBodyguardCombatTactics', SOURCE)
        self.assertIn('0x6B7A646C242A7059UL', SOURCE)
        self.assertIn('0x4D9CA1009AFBD057UL', SOURCE)
        self.assertIn('0x9F7794730795E019UL', SOURCE)

    def test_diagnostics_are_written_to_scripts_directory(self):
        self.assertIn('_logDirectory = _scriptsDirectory;', SOURCE)
        self.assertIn('Logs: " + _logPath', SOURCE)

if __name__ == '__main__':
    unittest.main()
