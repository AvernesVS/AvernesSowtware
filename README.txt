AvernesSowtware Extended 1.2.7 - vehicle database and compatibility notes

Canonical database: vehicles.json
Compatibility copies: vehicle_catalog.json and ..\vehicle_catalog.json

The base vehicle_catalog.json / Vehicles\vehicles.json databases contain 480 records. addons.json contains 85 additional records; 20 model codes overlap, so the raw merged catalog contains 545 unique model codes before compatibility checks against the installed GTA V Legacy build. Models not present in the installed game build are filtered from the in-game list.
The spawner reads scripts\AvernesSowtware\Vehicles\vehicles.json first, then checks
legacy-compatible locations for vehicle_catalog.json / vehicles.json and the
extensionless legacy filename vehicle_catalog.

Editing this JSON changes the local spawner list; it does not install add-on
vehicle models into GTA V. An add-on vehicle still requires its own game assets
and the correct meta/resource installation in the game.

Saved vehicle builds are stored separately in saved_vehicles.json (10 slots). This file is managed by the trainer; do not overwrite it while the game is running.
