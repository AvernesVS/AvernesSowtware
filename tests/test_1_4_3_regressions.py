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

if __name__ == '__main__':
    unittest.main()
