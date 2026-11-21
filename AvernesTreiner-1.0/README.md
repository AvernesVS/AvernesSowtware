# AvernesTreiner 1.0.0

A GTA V Legacy Story Mode trainer based on Ynix Trainer v1.6.0.0 by Carlos (carlostmj).

## Source layout
The original 15 C# files are preserved as separate source modules under `src/Core` and `src/Modules`.

## Rebrand scope
- Product, assembly, script class, namespaces, runtime folder and DLL renamed to **AvernesTreiner**.
- English (en-US) is the only shipped language and the default; Portuguese language assets and the language selector are removed.
- Hard-coded Portuguese UI labels and notifications have been translated to English.
- Build target: **.NET Framework 4.8**.
- Original MIT copyright and permission notice retained in `LICENSE-ORIGINAL-MIT.txt`.

## Requirements
- GTA V Legacy Story Mode.
- Script Hook V.
- ScriptHookVDotNet 2.10.13 (API 2).
- NativeUI 1.9.0.
- Microsoft .NET Framework 4.8.

ScriptHookVDotNet and NativeUI are external build/runtime dependencies and are not bundled in the install ZIP. Install their DLLs according to their own instructions.

## Build
The GitHub Actions workflow `.github/workflows/build-avernes-treiner.yml` restores pinned NuGet references, compiles on Windows against .NET Framework 4.8 reference assemblies, creates an installable ZIP and uploads it as an Actions artifact.

For a local build from `src`, set `SHVDN2_DLL` and `NATIVEUI_DLL` to the full paths of the dependency DLLs, then run `build.bat`.

## Install
Extract the workflow ZIP artifact into the GTA V Legacy directory. It installs:
- `scripts/AvernesTreiner.dll`
- `scripts/AvernesTreiner/Config.ini`
- `scripts/AvernesTreiner/Languages/en-US.ini`
- `scripts/AvernesTreiner/addons.json`

Default menu key: **F4**.

This 1.0.0 rebrand preserves the original trainer behavior and adds no unrelated gameplay features.
Original source: https://github.com/carlostmj/gta5-mods/tree/main/mods/YnixTrainer
