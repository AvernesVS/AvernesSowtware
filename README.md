# AvernesTreiner v0.9.4

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
Extract the archive to any folder and run `INSTALL.bat`. If the archive is not beside `GTA5.exe`, enter the GTA V Legacy folder path when prompted. The installer backs up current data and migrates `Config.ini`, `SavedVehicles.json`, `WeaponInventory.json` and `addons.json` into `Documents\\AvernesTreiner`. The trainer reads and writes these files in that permanent location; existing Documents data is never overwritten by older game-local files. The uninstaller does not remove user data. Run `UNINSTALL.bat` to remove only the trainer DLL and its two shipped language files; user data is retained.

Default menu key: **F10**.

## v0.9.4 fixes and changes
- Replaced the Golf Assistant completely with **Aim Bot**. While the player is aiming, it smoothly guides the gameplay camera toward visible hostile NPCs, hostile police and red-marked mission targets; it does not fire automatically and does not intentionally select neutral civilians or friendly NPCs.
- Escape is reserved for closing the trainer menu. The pause/map control is suppressed across all input groups while the menu is open and briefly after it closes.
- Reworked Max All Character Stats: the trainer now writes each of the eight Story Mode skill stats directly for all three protagonist slots, verifies the readback and repeats the writes once per second. It no longer terminates Rockstar's `stats_controller`.
- Weapon inventory snapshots now include compatible attachments, extended magazines and MK II component/camo colors. Restoration applies the weapon and attachments first, then restores total and in-clip ammunition so an extended magazine can provide its full clip capacity.
- Updated runtime, localization, configuration, CI validation and installer artifact version markers to 0.9.4.
- Automated build/resource/package validation is required. GTA V Legacy behavior still needs in-game testing; successful compilation alone does not confirm Aim Bot targeting, camera behavior, stat changes or weapon restoration in the game.

## v0.9.3 fixes and changes
- Golf advice now reports horizontal ball-to-pin distance, elevation and a clearly approximate club class. It no longer invents a shot-power percentage; the game's yellow target zone remains the source for actual power.
- Max All Character Stats now writes the actual Special skill stat separately from the Special Ability unlock stat, stops Rockstar's `stats_controller` while the feature is active, and rechecks all three protagonists every second. Restart GTA V to restore normal skill progression.
- Weapon inventory data uses settable JSON properties, recovers from a backup file, retains a previous valid inventory backup and snapshots the active protagonist's inventory more frequently.
- Installer migration now includes `WeaponInventory.json`, preserving DLC weapon inventory data from older game-local folders and backup folders.
- Installer, uninstaller, README and runtime version markers identify this release as 0.9.3.
- Automated build/resource/package checks are required; GTA V Legacy behavior still needs in-game testing.

## v0.9.2 fixes and changes
- Max All Character Stats now uses the Story Mode protagonist stat identifiers, including the Driving skill's internal `WHEELIE_ABILITY` stat, and sets the game's per-skill maxed flags for Michael, Franklin and Trevor.
- DLC weapon persistence tracks each protagonist separately in `Documents/AvernesTreiner/WeaponInventory.json`; missing owned weapons are restored after character changes and game startup.
- Normal gameplay controls are re-enabled after NativeUI processes menu navigation, while trainer navigation controls remain suppressed to avoid double-triggering.
- Saved vehicle names are trimmed, blank and duplicate names are rejected, entries are sorted and renumbered, and deletion requires typing `DELETE`.
- Spawned and saved vehicles receive radio initialization with short retries for delayed audio setup.
- Teleportation and existing vehicle-cleanup logic are preserved; their source modules are not rewritten in this release.
- CI compiles the project for .NET Framework 4.8 and packages the installer. The new behavior still requires in-game testing on GTA V Legacy 1.0.2245.0.

## v0.9.0 fixes and changes
- Infinite Special Ability now refills after an ability use instead of forcing the meter every frame; it does not refill during Michael's aiming state.
- Max All Character Stats writes all three Story Mode stat slots and reapplies the active protagonist's eight skills when the game switches characters or models.
- Mission cleanup now captures unsaved trainer-spawned vehicles that were already present when a mission starts, not only vehicles spawned during the mission. Saved vehicles and occupied vehicles remain protected.
- Waypoint teleport settles land vehicles on the ground after placement while leaving boats and aircraft untouched.
- Vehicle God Mode continues to reapply deformation and damage-scale protection; residual cosmetic scuffs remain a known limitation pending in-game verification.

## v0.8.9 additions
- Infinite Special Ability refills the active protagonist's special meter while enabled.
- Max All Character Stats sets all eight Story Mode skills to 100% for Michael, Franklin and Trevor, using per-protagonist stat slots and persistent writes.

## v0.8.6 fixes
- Waypoint probing now checks additional ground-query modes and several navmesh reference heights before giving up, while keeping the safe-position distance limit.
- Infinite Clip is reapplied to the currently active Story Mode character and refreshed after character swaps; Infinite Ammo refreshes immediately after a character swap.
- Vehicle God Mode continues to reapply deformation protection while active.

## v0.8.5 bug fixes
- Remove the redundant Cops Ignore Player toggle and its repeated police-ignore native calls. Clear the legacy police-ignore state on startup; Never Wanted remains the supported police feature.
- Waypoint teleport captures the original player/vehicle handle when requested, so the intended vehicle is moved even if streaming takes time.
- Waypoint probing no longer waits indefinitely for NEW_LOAD_SCENE_START to succeed; collision/ground checks use a bounded fallback window and still require a valid ground or water result.
- Vehicle God Mode sets vehicle damage scale to zero while enabled and restores the normal scale when disabled, supplementing deformation repair.
- Load Settings relies on the rebuilt menu to refresh list indices, avoiding programmatic list-index changes before the rebuild.

## v0.8.4 changes
- Trainer-spawned vehicle cleanup now uses a deletion queue (one attempt per 500 ms), rechecks existence and occupancy immediately before and after deletion, and retries when the entity remains.
- Vehicles spawned from the Saved Vehicles catalog are protected from automatic abandoned-vehicle cleanup.
- Menu rebuilds no longer duplicate saved-vehicle records; the JSON serializer is reused.
- Reuse camera-control arrays and waypoint ground-probe heights instead of allocating them repeatedly.
- Frame-scoped gameplay natives remain frame-scoped; existing maintenance and nearby-vehicle scan intervals are preserved.

## v0.8.3 changes
- Apply language changes immediately: the menu is rebuilt in the selected language without restarting GTA V. Reload Settings refreshes the existing module instances and menu controls, preserving active runtime objects while applying saved checkbox states.
- Expand Russian and English localization coverage across the main menus, including Vehicle, vehicle customization, autopilot choices, bodyguards, weapons, world options and settings.
- Persist checkbox settings as they change, retain a `.bak` copy of Config.ini, and restore from that backup if the primary configuration is missing, empty or cannot be loaded.
- Resolve the installer's user-data folder through Windows' actual My Documents location so it matches the path used by the trainer, including redirected Documents folders.
- Move distant waypoint placement to the native coordinate setter after collision streaming, avoiding the ScriptHookVDotNet vehicle safe-placement setter that can reject a requested vehicle position.
- Track vehicles spawned during story missions and remove them when the mission ends, once they are unoccupied. Vehicles currently occupied by the player or NPCs are not deleted.
- Reapply vehicle deformation repair each script tick while Vehicle God Mode is active without calling the full vehicle repair native, which could reset customization.
- GitHub Actions successfully compiled the .NET Framework 4.8 DLL, validated localization key parity and packaged the installer. Target-game behavior still requires in-game testing.

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

Original source: https://github.com/carlostmj/gta5-mods/tree/main/mods/YnixTrainer
