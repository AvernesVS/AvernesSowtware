# AvernesTreiner 6.1 — Experimental Rolls-Royce Edition

An isolated experimental branch based on AvernesTreiner v0.6. It does not change the v0.6 or v0.7 work.

## Experimental vehicle
- Menu entry: **Rolls-Royce Phantom VIII ORIBE 2021**
- GTA model name: `ph8oribe`
- Type: Add-On DLC vehicle
- The trainer entry and spawn handling are included in this branch.

## Important installation limitation
The third-party vehicle's binary DLC package is not included in this repository or trainer archive. The model must already be installed and registered in GTA V Legacy before it can spawn. Its required DLC folder is `update/x64/dlcpacks/ph8oribe`, and the game's `dlclist.xml` must contain:

```xml
<Item>dlcpacks:/ph8oribe/</Item>
```

AvernesTreiner cannot create the vehicle from its model name alone. If the DLC is absent, the trainer will show a specific message instead of pretending the car spawned. No game archives are modified by this trainer.

## Build requirements
- Windows build runner
- .NET Framework 4.8 reference assemblies
- ScriptHookVDotNet2 2.10.13
- NativeUI 1.9.0

The experimental GitHub Actions workflow builds the trainer and packages its runtime files. In-game compatibility and the vehicle model itself remain unverified on GTA V Legacy 1.0.2245.0.

## Runtime
- GTA V Legacy Story Mode
- Script Hook V
- ScriptHookVDotNet 2.10.13 (API 2)
- NativeUI 1.9.0
- .NET Framework 4.8

Menu key: **F10**.


The dedicated 6.1 experimental validation workflow compiles this branch on every source update.
