import json
import os
import pathlib
import unittest

ROOT = pathlib.Path(os.environ.get('AVERNE_PROJECT_ROOT', str(pathlib.Path(__file__).resolve().parents[2])))
CORE = (ROOT / 'src/Core/AvernesSowtwareScript.cs').read_text(encoding='utf-8-sig')
CATALOG_TEXT = (ROOT / 'package/AvernesSowtware/Vehicles/vehicles.json').read_text(encoding='utf-8-sig')
CATALOG = json.loads(CATALOG_TEXT)
BACKUP_CATALOG = json.loads((ROOT / 'package/AvernesSowtware/vehicle_catalog.json').read_text(encoding='utf-8-sig'))
VEHICLES = (ROOT / 'src/Managers/VehicleCatalog.cs').read_text(encoding='utf-8-sig')
VEHICLE_SYSTEMS = (ROOT / 'src/Managers/VehicleSystems.cs').read_text(encoding='utf-8-sig')
TEST_PLAN = (ROOT / 'docs/REGRESSION_TEST_PLAN_RU.md').read_text(encoding='utf-8-sig')
PROJECT = (ROOT / 'AvernesSowtware.csproj').read_text(encoding='utf-8-sig')
BUILD = (ROOT / 'build/Build.bat').read_text(encoding='utf-8-sig')
VALIDATOR = (ROOT / 'build/ReleaseValidator.cs').read_text(encoding='utf-8-sig')

class Flagship134RegressionTests(unittest.TestCase):
    def test_super_diamond_uses_real_model_code_in_both_databases(self):
        for catalog in (CATALOG, BACKUP_CATALOG):
            entry = next((x for x in catalog if x.get('model') == 'superd'), None)
            self.assertIsNotNone(entry, 'missing model superd')
            self.assertIn('Super Diamond', entry['name'])
            self.assertEqual(entry['category'], 'Sedans / Coupes / SUVs')
        value = 0
        for byte in b'superd':
            value = (value + byte) & 0xFFFFFFFF
            value = (value + (value << 10)) & 0xFFFFFFFF
            value ^= value >> 6
        value = (value + (value << 3)) & 0xFFFFFFFF
        value ^= value >> 11
        value = (value + (value << 15)) & 0xFFFFFFFF
        self.assertEqual(value, 0x42F2ED16)

    def test_super_diamond_search_alias_maps_to_superd(self):
        self.assertIn('compactNeedle', VEHICLES)
        self.assertIn('compactNeedle == "superdiamond"', VEHICLES)
        self.assertIn('(superDiamondAlias && String.Equals(model, "superd", StringComparison.Ordinal))', VEHICLES)

    def test_removed_features_are_not_compiled_or_exposed(self):
        self.assertNotIn('PageEntry("Atlas"', CORE)
        self.assertNotIn('BuildAtlasMenus()', CORE)
        self.assertNotIn('StartAutopilot', CORE + VEHICLE_SYSTEMS)
        self.assertNotIn('StopAutopilot', CORE + VEHICLE_SYSTEMS)
        self.assertNotIn('TeleportToWaypoint', CORE)
        self.assertNotIn('BuildWardrobeMenus()', CORE)
        for old_file in ('src/Managers/Atlas.cs', 'src/Managers/Wardrobe.cs'):
            self.assertFalse((ROOT / old_file).exists(), old_file)
        for source in (PROJECT, BUILD):
            self.assertNotIn('Atlas.cs', source)
            self.assertNotIn('Wardrobe.cs', source)

    def test_vehicle_customization_actions_exist_and_guard_missing_parts(self):
        self.assertIn('Максимальный тюнинг', CORE)
        self.assertIn('Установить все обвесы', CORE)
        self.assertIn('ApplyAllVehicleBodyKits', VEHICLE_SYSTEMS)
        self.assertIn('if (count <= 0)', VEHICLE_SYSTEMS)
        self.assertIn('CycleVehicleWheelType', VEHICLE_SYSTEMS)
        self.assertIn('CycleVehicleWheelStyle', VEHICLE_SYSTEMS)
        self.assertIn('CycleVehicleMod', VEHICLE_SYSTEMS)
        self.assertIn('VehicleVisuals', CORE)

    def test_bodyguards_have_weapon_aim_passenger_and_vulnerability_logic(self):
        for marker in ('CycleBodyguardWeapon', 'GiveSelectedWeaponToBodyguards', 'TASK_COMBAT_PED',
                       '0x2975C866E6713290UL', '0xC20E50AA46D09CA8UL',
                       '0x22AC59A870E6A669UL', 'SET_PED_ARMOUR, guard, 0',
                       'SET_ENTITY_INVINCIBLE, guard, false',
                       '_lastBodyguardAimTargetHandle = 0', '_bodyguards.RemoveAt(i)', 'previouslyRequestedVehicle'):
            self.assertIn(marker, CORE, marker)

    def test_validator_checks_current_features_and_retires_old_ones(self):
        self.assertIn('CycleBodyguardWeapon', CORE)
        self.assertIn('superDiamondAlias', VALIDATOR)
        self.assertIn('1.3.4', VALIDATOR)
        self.assertIn('_groundLoadSceneStarted', VALIDATOR)

    def test_max_tuning_applies_engine_turbo_and_armour_and_tint(self):
        self.assertIn('const int maximumLimoTint = 5', VEHICLE_SYSTEMS)
        self.assertIn('int[] toggleModTypes = new int[] { 18, 20, 22 }', VEHICLE_SYSTEMS)
        self.assertIn('modType <= 49', VEHICLE_SYSTEMS)
        self.assertIn('GetWheelTypeForVehicleClass', VEHICLE_SYSTEMS)

    def test_active_regression_plan_covers_flagship_changes_not_removed_modules(self):
        self.assertIn('Super Diamond', TEST_PLAN)
        self.assertIn('Body kits', TEST_PLAN)
        self.assertIn('Wheel and visual selector', TEST_PLAN)
        self.assertNotIn('**Atlas:**', TEST_PLAN)
        self.assertNotIn('**Wardrobe:**', TEST_PLAN)

if __name__ == '__main__':
    unittest.main(verbosity=2)
