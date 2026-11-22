# AvernesTreiner v0.3

A GTA V Legacy Story Mode trainer based on Ynix Trainer v1.6.0.0 by Carlos (carlostmj).

## Source layout
The retained C# modules are kept as separate source files under `src/Core` and `src/Modules`.

## v0.3 changes
- Optimized per-frame trainer updates: persistent player/vehicle state is refreshed on bounded intervals, while frame-scoped gameplay natives remain per-frame.
- Limited autopilot nearby-vehicle scans and stuck-traffic checks to timed intervals; the stuck watchdog now uses elapsed time instead of frame counts.
- Limited vehicle water/auto-flip checks and bodyguard cleanup to periodic updates.
- Gameplay and camera controls remain available while the menu is open; only NativeUI navigation controls are suppressed after menu input is processed.
- Pressing **Esc** from any submenu closes the entire trainer menu and returns to gameplay instead of stepping back one menu level.

## v0.2 changes
- Removed the complete Power & Vision menu section and its NoClip implementation.
- Removed the NoClip source module; no NoClip velocity or gravity handling remains.
- Corrected the Galileo Observatory roof teleport point and checked other landmark destinations.
- Added **Save Settings** and **Load Settings** controls in the Settings menu.
- `Config.ini` is loaded automatically at startup and is created with defaults on first launch.
- Active Player, Vehicle, Weapons, World and Bodyguard checkbox states are saved to `Config.ini` automatically when changed and restored at startup. The Save/Load buttons are also available in Settings; after using Load Settings, restart the trainer script to apply the reloaded toggle states.
- The menu hotkey is fixed to **F10**; existing 0.1 configurations are migrated from F4 to F10.
- Vehicle spawning checks model validity and loading before creating a vehicle. Unsupported or unavailable models produce a “Vehicle not found” notification instead of being passed to the spawn call.
- Corrected the `proto` vehicle model entry to the game model name `prototipo`.

## Rebrand and compatibility
- Product, assembly, script class, namespaces, runtime folder and DLL are named **AvernesTreiner**.
- English (en-US) is the only shipped language.
- Build target: **.NET Framework 4.8**.
- Original MIT copyright and permission notice retained in `LICENSE-ORIGINAL-MIT.txt`.

## Requirements
- GTA V Legacy Story Mode.
- Script Hook V.
- ScriptHookVDotNet 2.10.13 (API 2).
- NativeUI 1.9.0.
- Microsoft .NET Framework 4.8.

ScriptHookVDotNet and NativeUI are external build/runtime dependencies and are not bundled in the install ZIP. Keep their DLLs installed according to their own instructions.

## Build
The GitHub Actions workflow `.github/workflows/build-avernes-treiner.yml` restores pinned NuGet references, compiles on Windows against .NET Framework 4.8 reference assemblies, creates an installable ZIP and uploads it as an Actions artifact.

For a local build from `src`, set `SHVDN2_DLL` and `NATIVEUI_DLL` to the full paths of the dependency DLLs, then run `build.bat`.

## Install
Extract `AvernesTreiner-0.3-GTA5-Legacy.zip` into the GTA V Legacy directory. It installs:
- `scripts/AvernesTreiner.dll`
- `scripts/AvernesTreiner/Config.ini`
- `scripts/AvernesTreiner/Languages/en-US.ini`
- `scripts/AvernesTreiner/addons.json`

Default menu key: **F10**.

This release is version **0.3**. It includes only the requested fixes and removals; in-game runtime testing is still required.
Original source: https://github.com/carlostmj/gta5-mods/tree/main/mods/YnixTrainer
