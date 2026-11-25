# AvernesTreiner v0.8.3

A GTA V Legacy Story Mode trainer based on Ynix Trainer v1.6.0.0 by Carlos (carlostmj).

## Repository layout
- `src/Core` — trainer core and configuration.
- `src/Modules` — gameplay and menu modules.
- `resources/` — default runtime configuration, language and JSON data copied into the installer.
- `.github/workflows/build-avernes-treiner.yml` — the single active build and packaging workflow.
- `build/` — local compiler output (generated, not committed).

## v0.4 changes (retained)
- Added 246 additional built-in vehicle model names missing from the original trainer categories, including DLC, service, utility, industrial, aircraft and watercraft models.
- Added a dedicated **Additional Vehicles (Build 2245)** submenu; the existing nine categories and add-on JSON/manual model input remain available.
- Removed four invalid model-name entries from the old built-in list.
- The catalog targets GTA V Legacy **1.0.2245.0** (PC build 2245 / Online 1.54). It is reconstructed from the public DurtyFree GTA V data dump at build 2545 / Online 1.58, with vehicles introduced by Los Santos Tuners and The Contract excluded because those updates post-date build 2245. This is a filtered public-data reconstruction, not a direct extraction from the user's local game files.
- Every spawn still passes the existing runtime checks (`IsInCdImage`, `IsValid`, and model streaming); unavailable models are rejected gracefully.

## v0.4 saved vehicle catalog (retained)
- Added a separate **Saved Vehicles** menu and `SavedVehicles.json` storage file.
- Players can save the vehicle they are currently driving under a chosen name and spawn it later with stored model hash, mod slots and variations, toggle mods, standard paint and wheel colors, wheel type, window tint, plate, livery, neon setup, tire smoke, extras, and tire burst setting.
- Saving is manual; no per-frame disk writes or automatic save loop is introduced.
- Vehicle records are kept separately from `Config.ini` and `addons.json`; a fresh installation starts with an empty `SavedVehicles.json`.

## v0.3 changes
- Optimized per-frame trainer updates: persistent player/vehicle state is refreshed on bounded intervals, while frame-scoped gameplay natives remain per-frame.
- Limited autopilot nearby-vehicle scans and stuck-traffic checks to timed intervals; the stuck watchdog now uses elapsed time instead of frame counts.
- Limited vehicle water/auto-flip checks and bodyguard cleanup to periodic updates.
- Gameplay and camera controls remain available while the menu is open; NativeUI's mouse cursor is disabled, menu navigation uses the keyboard arrows, and only menu navigation controls are suppressed after menu input is processed.
- Pressing **Esc** from any submenu closes the entire trainer menu and returns to gameplay instead of stepping back one menu level.

## v0.2 changes
- Removed the complete Power & Vision menu section and its NoClip implementation.
- Removed the NoClip source module; no NoClip velocity or gravity handling remains.
- Corrected the Galileo Observatory roof teleport point and checked other landmark destinations.
- Added **Save Settings** and **Load Settings** controls in the Settings menu.
- `Config.ini` is loaded automatically at startup and is created with defaults on first launch.
- Player, Vehicle, Weapons, World and Bodyguard checkbox changes stay in memory until **Save Settings** is pressed; the trainer does not write to disk on each toggle. Saved values are restored at startup.
- The menu hotkey is fixed to **F10**; existing 0.1 configurations are migrated from F4 to F10.
- Vehicle spawning checks model validity and loading before creating a vehicle. Unsupported or unavailable models produce a “Vehicle not found” notification instead of being passed to the spawn call.
- Corrected the `proto` vehicle model entry to the game model name `prototipo`.

## Rebrand and compatibility
- Product, assembly, script class, namespaces, runtime folder and DLL are named **AvernesTreiner**.
- English (en-US) and Russian (ru-RU) localizations are shipped; the language can be selected in Settings and takes effect after script restart.
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
The single GitHub Actions workflow `.github/workflows/build-avernes-treiner.yml` restores pinned NuGet references, compiles on Windows against .NET Framework 4.8 reference assemblies, assembles the installable ZIP from `resources/`, and uploads it as an Actions artifact.

For a local build, open a command prompt in `src`, set `SHVDN2_DLL` and `NATIVEUI_DLL` to the full paths of the dependency DLLs, then run `build.bat`. The compiled DLL is written to the repository's `build/` directory.

## Install
Extract the archive to any folder and run `INSTALL.bat`. If the archive is not beside `GTA5.exe`, enter the GTA V Legacy folder path when prompted. The installer backs up current data and migrates `Config.ini`, `SavedVehicles.json` and `addons.json` into `Documents\\AvernesTreiner`. The trainer reads and writes these files in that permanent location; existing Documents data is never overwritten by older game-local files. The uninstaller does not remove user data. Run `UNINSTALL.bat` to remove only the trainer DLL and its two shipped language files; user data is retained.

Default menu key: **F10**.

## v0.8.3 changes
- Use `Documents/AvernesTreiner` as persistent storage for configuration, saved vehicles and add-on lists; migrate existing game-local files without overwriting an existing Documents copy.
- Parse localization INI files explicitly as UTF-8 so Russian text is not silently lost by the runtime INI reader.
- Strengthen Auto Repair on Entry with direct vehicle repair, deformation, dirt, engine, body and petrol-tank repair natives.
- Reapply deformation repair while Vehicle God Mode is enabled. In-game validation is still required.

## v0.8 fixes
- Nitro accepts **Right Shift** as the primary trigger and **X** as a fallback, restoring the previously working alternate key without bringing back Left Shift. The horizontal boost force and speed cap remain unchanged.
- Waypoint teleport now waits for `IS_NEW_LOAD_SCENE_LOADED` before querying ground height; it no longer falls back to probing after an arbitrary 2.5-second delay. If the scene does not finish streaming within 20 seconds, teleport is cancelled safely.
- Vehicle God Mode no longer disables vehicle-glass collision processing, which can make bullets pass through glass. The original invincibility, damage proofs, visible-damage suppression and burst-proof tyres remain.
- Automatic cleanup now pauses during story missions and resets the abandonment timer. Trainer-spawned vehicles are deleted through `DELETE_VEHICLE` without first marking them as no longer needed. Both vehicle creation paths (normal spawner and Saved Vehicles) are tracked.
- **Still needs in-game confirmation:** Oppressor Mk II dismounting at a mission marker. No trainer instruction that blocks vehicle exit was found in the current source.
- **Not addressed in code:** Windows sleep/resume performance and sound problems. The current evidence does not show that the trainer causes them; they require a separate Windows/driver/ScriptHookVDotNet diagnosis.

## v0.7 changes
- Nitro activates while holding **Right Shift**; Left Shift is not captured. The original force strength (`1.8`) and speed cap (`400`) are retained, while the boost force is constrained to the horizontal plane to reduce unwanted lift.
- Vehicle God Mode disables vehicle-glass damage processing while active and restores normal glass processing when disabled.
- Trainer-spawned vehicles, including vehicles spawned from Saved Vehicles, are tracked for cleanup. A vehicle is eligible only after the player has exited it, moved more than 150 metres away, and left it empty and stationary for at least 60 seconds. Ambient traffic and vehicles spawned by other scripts are not tracked.
- Installer layout places `AvernesTreiner.dll` directly in `scripts/` and keeps configuration/data in `scripts/AvernesTreiner/`.

## v0.6 fixes
- **Never Wanted:** sets the maximum wanted level to zero immediately and enforces it every frame. Wanted stars are blocked at the source rather than cleared after a visible delay; disabling the option restores the previous wanted-level cap (or the standard five-star cap if the stored value is invalid).
- **Waypoint Teleport:** requests distant map streaming, moves the streaming focus to the target, waits for the scene/collision, probes ground from multiple heights, and falls back only to a nearby navmesh-safe location or a detected water surface. Streaming focus and the scene request are cleaned up after completion or cancellation.
- **Config.ini:** resolves the correct runtime folder whether ScriptHookVDotNet exposes the game root or the `scripts` folder, avoiding the duplicate `scripts\\scripts` path. If the GTA installation folder is not writable, it uses the current Windows user's AppData folder and copies existing settings there.
- Settings changes remain in memory until **Save Settings** is pressed. Speed unit selection no longer triggers an immediate write.
- NativeUI's mouse cursor remains disabled; keyboard arrows control the menu while mouse camera-look and ordinary gameplay controls remain available.
- The repository keeps source under `src/` and runtime defaults under `resources/`; generated installer ZIPs and obsolete workflows are not stored in the repository root.

## v0.5 fixes
- Settings persistence now uses an absolute GTA installation path for Config.ini; Save Settings flushes the current settings and reports the underlying error if saving fails.
- Camera look axes are explicitly re-enabled after NativeUI processes menu navigation.
- Escape closes the entire trainer and suppresses the pause/map action.
- Waypoint teleport streams collision and retries ground-height detection at multiple probe heights. If safe ground cannot be found, teleport is cancelled instead of using an unsafe waypoint Z value. Water waypoints use the detected water surface.
- Drift Mode restores normal grip when disabled, when changing vehicles, and when leaving a vehicle.
- Vehicle God Mode applies invincibility, entity damage proofs, hidden visible damage and burst-proof tires; normal damage behavior is restored when disabled or leaving/changing vehicles.
- Auto Repair is an entry-time repair: a damaged vehicle is repaired once when entered, not continuously.
- Give All Weapons grants every available weapon and immediately adds every attachment accepted by that weapon's compatibility check.

This release is version **0.8.3**. The source and runtime resources are validated by GitHub Actions; in-game behavior still requires testing on the target GTA V Legacy installation.
Original source: https://github.com/carlostmj/gta5-mods/tree/main/mods/YnixTrainer
