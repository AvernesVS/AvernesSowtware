from pathlib import Path
import os

root = Path(os.environ["AVERNE_ROOT"])
test_path = root / "tests" / "test_1_4_2_regressions.py"
validator_path = root / "build" / "ReleaseValidator.cs"
if not test_path.is_file() or not validator_path.is_file():
    raise SystemExit("Required v1.4.2 regression or build-validator source is missing.")

# Refresh obsolete source-contract assertions without skipping any test.
data = test_path.read_bytes()
test_replacements = (
    (b'self.assertIn("IS_PED_IN_COMBAT", MAIN)',
     b'self.assertIn("0x4859F1FC66A6278EUL", MAIN)'),
    (b'0xDC0F817884CDD856UL, 14, !insideFortZancudo',
     b'0xDC0F817884CDD856UL, 14, false'),
    (b'0x9B2BD3773123EA2FUL, 14, insideFortZancudo',
     b'0x9B2BD3773123EA2FUL, 14, true'),
    (b'0xDC0F817884CDD856UL, 2, !insideFortZancudo',
     b'0xDC0F817884CDD856UL, 2, false'),
)
for old, new in test_replacements:
    count = data.count(old)
    if count != 1:
        raise SystemExit("Expected exactly one stale regression assertion; found %d for %r" % (count, old))
    data = data.replace(old, new, 1)
test_path.write_bytes(data)

# Keep pre-build validation strict, but align it with the safe 1.4.2 implementation.
validator = validator_path.read_text(encoding="utf-8-sig")
old_vehicle = '''        if (vehicleSource.IndexOf("repairVehicleOnApply", StringComparison.Ordinal) < 0)
            Error("1.3.7 vehicle repair must be gated to initial application / God Mode activation.");
'''
new_vehicle = '''        int vehicleSettingsStart = vehicleSource.IndexOf("private void ApplyVehicleSettings", StringComparison.Ordinal);
        int vehicleSettingsEnd = vehicleSettingsStart >= 0
            ? vehicleSource.IndexOf("private void RestoreVehicleProtection", vehicleSettingsStart, StringComparison.Ordinal)
            : -1;
        string vehicleSettingsBody = vehicleSettingsStart >= 0 && vehicleSettingsEnd > vehicleSettingsStart
            ? vehicleSource.Substring(vehicleSettingsStart, vehicleSettingsEnd - vehicleSettingsStart)
            : String.Empty;
        if (vehicleSettingsBody.IndexOf("SET_VEHICLE_DEFORMATION_FIXED", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("0x772282EBEB95E682UL", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("if (godMode)", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("Function.Call(Hash.SET_VEHICLE_FIXED", StringComparison.Ordinal) >= 0)
            Error("1.4.2 vehicle God Mode must clear deformation and broken windows only while enabled, without resetting animated doors.");
'''
old_fort = '''        if (mainSource.IndexOf("ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0xDC0F817884CDD856UL, 14, !insideFortZancudo", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0x9B2BD3773123EA2FUL, 14, insideFortZancudo", StringComparison.Ordinal) < 0)
            Error("1.4.2 Fort Zancudo Army dispatch suppression is missing or uses the wrong dispatch ID.");
'''
new_fort = '''        int fortDispatchStart = mainSource.IndexOf("private void UpdateFortZancudoArmyDispatch", StringComparison.Ordinal);
        int fortDispatchEnd = fortDispatchStart >= 0
            ? mainSource.IndexOf("private void RestoreFortZancudoArmyDispatch", fortDispatchStart, StringComparison.Ordinal)
            : -1;
        string fortDispatchBody = fortDispatchStart >= 0 && fortDispatchEnd > fortDispatchStart
            ? mainSource.Substring(fortDispatchStart, fortDispatchEnd - fortDispatchStart)
            : String.Empty;
        if (fortDispatchBody.IndexOf("ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0xDC0F817884CDD856UL, 14, false", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0x9B2BD3773123EA2FUL, 14, true", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0xDC0F817884CDD856UL, 2, false", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0x9B2BD3773123EA2FUL, 2, true", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("AddMilliseconds(900)", StringComparison.Ordinal) < 0)
            Error("1.4.2 Fort Zancudo Army and helicopter dispatch must be periodically disabled and blocked while inside the base.");
'''
for label, old, new in (
    ("vehicle God Mode validator", old_vehicle, new_vehicle),
    ("Fort Zancudo dispatch validator", old_fort, new_fort),
):
    count = validator.count(old)
    if count != 1:
        raise SystemExit("Expected exactly one obsolete %s rule; found %d" % (label, count))
    validator = validator.replace(old, new, 1)
validator_path.write_text(validator, encoding="utf-8")

print("Updated four stale test assertions and two strict build-validator contracts; no tests were skipped.")
