from pathlib import Path
import sys

source = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("src/Core/AvernesSowtwareScript.cs")
text = source.read_text(encoding="utf-8-sig")
old = """            try
            {
                Function.Call((Hash)0x55E1D2758F34E437UL, handle);
                Function.Call((Hash)0x5FFBDEEC3E8E2009UL, handle);
                Function.Call((Hash)0x4F1D4BE3A7F24601UL, handle, saved.PrimaryColor, saved.SecondaryColor);
                Function.Call((Hash)0x43FEB945EE7F85B8UL, handle, saved.ModColor1Type, saved.ModColor1Index, saved.ModColor1Pearlescent);
                Function.Call((Hash)0x816562BADFDEC83EUL, handle, saved.ModColor2Type, saved.ModColor2Index);
                Function.Call((Hash)0x2036F561ADD12E33UL, handle, saved.PearlescentColor, saved.WheelColor);
                if (saved.PrimaryCustom) Function.Call((Hash)0x7141766F91D15BEAUL, handle, saved.PrimaryRed, saved.PrimaryGreen, saved.PrimaryBlue);
                if (saved.SecondaryCustom) Function.Call((Hash)0x36CED73BFED89754UL, handle, saved.SecondaryRed, saved.SecondaryGreen, saved.SecondaryBlue);
            }
            catch (Exception ex) { LogException("Restore saved vehicle colours", ex); }
"""
new = """            // Keep each paint operation isolated: one unsupported native or vehicle-specific
            // paint failure must not prevent the remaining saved colours from being restored.
            try { Function.Call((Hash)0x55E1D2758F34E437UL, handle); }
            catch (Exception ex) { LogException("Prepare saved vehicle paint", ex); }
            try { Function.Call((Hash)0x5FFBDEEC3E8E2009UL, handle); }
            catch (Exception ex) { LogException("Prepare saved vehicle paint variation", ex); }
            try { Function.Call((Hash)0x4F1D4BE3A7F24601UL, handle, saved.PrimaryColor, saved.SecondaryColor); }
            catch (Exception ex) { LogException("Restore indexed vehicle colours", ex); }
            try { Function.Call((Hash)0x43FEB945EE7F85B8UL, handle, saved.ModColor1Type, saved.ModColor1Index, saved.ModColor1Pearlescent); }
            catch (Exception ex) { LogException("Restore primary mod paint", ex); }
            try { Function.Call((Hash)0x816562BADFDEC83EUL, handle, saved.ModColor2Type, saved.ModColor2Index); }
            catch (Exception ex) { LogException("Restore secondary mod paint", ex); }
            try { Function.Call((Hash)0x2036F561ADD12E33UL, handle, saved.PearlescentColor, saved.WheelColor); }
            catch (Exception ex) { LogException("Restore pearlescent and wheel colours", ex); }
            if (saved.PrimaryCustom)
            {
                try { Function.Call((Hash)0x7141766F91D15BEAUL, handle, saved.PrimaryRed, saved.PrimaryGreen, saved.PrimaryBlue); }
                catch (Exception ex) { LogException("Restore custom primary RGB", ex); }
            }
            if (saved.SecondaryCustom)
            {
                try { Function.Call((Hash)0x36CED73BFED89754UL, handle, saved.SecondaryRed, saved.SecondaryGreen, saved.SecondaryBlue); }
                catch (Exception ex) { LogException("Restore custom secondary RGB", ex); }
            }
"""
if old not in text:
    if "Keep each paint operation isolated" in text:
        print("Already patched")
        raise SystemExit(0)
    raise SystemExit("Expected v1.4.4 colour block not found; refusing to patch.")
source.write_text(text.replace(old, new, 1), encoding="utf-8-sig")
print("Patched saved vehicle colour restoration.")
