from pathlib import Path
import os

root = Path(os.environ["AVERNE_ROOT"])
test_path = root / "tests" / "test_1_4_2_regressions.py"
if not test_path.is_file():
    raise SystemExit("Required v1.4.2 regression test file is missing.")

data = test_path.read_bytes()
replacements = (
    (b'self.assertIn("IS_PED_IN_COMBAT", MAIN)',
     b'self.assertIn("0x4859F1FC66A6278EUL", MAIN)'),
    (b'0xDC0F817884CDD856UL, 14, !insideFortZancudo',
     b'0xDC0F817884CDD856UL, 14, false'),
    (b'0x9B2BD3773123EA2FUL, 14, insideFortZancudo',
     b'0x9B2BD3773123EA2FUL, 14, true'),
    (b'0xDC0F817884CDD856UL, 2, !insideFortZancudo',
     b'0xDC0F817884CDD856UL, 2, false'),
)

for old, new in replacements:
    count = data.count(old)
    if count != 1:
        raise SystemExit("Expected exactly one stale regression assertion; found %d for %r" % (count, old))
    data = data.replace(old, new, 1)

test_path.write_bytes(data)
print("Updated three stale 1.4.2 regression assertions; no tests were skipped.")
