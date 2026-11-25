using AvernesTreiner.Core;
using AvernesTreiner.Modules;
using GTA.Math;
using GTA.Native;
using GTA;
using NativeUI;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System;

namespace AvernesTreiner.Modules
{
    public class VehicleModule : IAvernesModule
    {
        public string Name { get { return "Vehicle"; } }

        private MenuPool _pool;
        private bool _vehicleGodMode = ConfigManager.GetBool("Vehicle", "VehicleGodMode", false);
        private bool _speedBoost = ConfigManager.GetBool("Vehicle", "SpeedBoost", false);
        private bool _hornJump = ConfigManager.GetBool("Vehicle", "HornJump", false);
        private bool _vehicleJump = ConfigManager.GetBool("Vehicle", "VehicleJump", false);
        private bool _driveOnWater = ConfigManager.GetBool("Vehicle", "DriveOnWater", false);
        private bool _speedometer = ConfigManager.GetBool("Vehicle", "Speedometer", false);
        private bool _driftMode = ConfigManager.GetBool("Vehicle", "DriftMode", false);
        private bool _seatbelt = ConfigManager.GetBool("Vehicle", "Seatbelt", true);
        private bool _autoRepair = ConfigManager.GetBool("Vehicle", "AutoRepair", false);
        private Vehicle _driftVehicle;
        private Vehicle _godModeVehicle;
        private int _lastEnteredVehicleHandle;
        private bool _superTorque = ConfigManager.GetBool("Vehicle", "SuperTorque", false);
        private bool _autoFlip = ConfigManager.GetBool("Vehicle", "AutoFlip", false);

        // Autopilot State
        private bool _autopilotActive = false;
        private int _autopilotStartTime = 0;
        private int _autopilotStyle = 786603; // Safe / Legal by default
        private float _autopilotAggressiveness = 0.0f;
        private float _autopilotSpeed = 28.0f; // ~100 km/h
        private Vector3 _autopilotDestination = Vector3.Zero;
        private int _brakeHoldCounter = 0;
        private int _lastMaintenanceTime = 0;
        private int _lastAutoFlipTime = 0;
        private int _lastWaterCheckTime = 0;
        private int _lastTrafficRammerCheckTime = 0;
        private int _stuckSinceTime = 0;
        private int _lastStuckTrafficCheckTime = 0;
        private int _lastAutopilotReissueTime = 0;
        private bool _trafficRammer = ConfigManager.GetBool("Vehicle", "TrafficRammer", false);
        private readonly List<Prop> _spawnedRamps = new List<Prop>();
        private static readonly Dictionary<int, Vehicle> _trackedSpawnedVehicles = new Dictionary<int, Vehicle>();
        private static readonly Dictionary<int, int> _abandonedSince = new Dictionary<int, int>();
        private static readonly HashSet<int> _missionSpawnedVehicles = new HashSet<int>();
        private static readonly HashSet<int> _protectedSavedVehicles = new HashSet<int>();
        private bool _missionCleanupTrackingActive = false;
        private readonly Queue<int> _vehicleDeletionQueue = new Queue<int>();
        private readonly HashSet<int> _pendingVehicleDeletion = new HashSet<int>();
        private readonly List<int> _trackedVehicleRemovalBuffer = new List<int>();
        private readonly List<int> _explodedVehicleRemovalBuffer = new List<int>();
        private int _lastSpawnCleanupTime = 0;
        private int _lastVehicleDeletionTime = 0;
        private int _lastExplodedVehicleCheckTime = 0;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            _pool = menuPool;
            var vehMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuVehicle", "Vehicle Options"));

            // Fix & Wash
            var fixItem = new UIMenuItem(Localization.Get("Vehicle", "FixVehicle", "Repair & Wash Vehicle"), Localization.Get("Vehicle", "FixVehicleDesc", "Repairs the bodywork and cleans the vehicle"));
            fixItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                if (player.IsInVehicle())
                {
                    Vehicle v = player.CurrentVehicle;
                    v.Repair();
                    v.Wash();
                    UI.Notify("~g~Vehicle repaired and cleaned!");
                }
                else
                {
                    UI.Notify("~r~You must be inside a vehicle!");
                }
            };
            vehMenu.AddItem(fixItem);

            // Auto Repair
            var autoRepItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "AutoRepair", "Auto Repair on Entry"), _autoRepair, Localization.Get("Vehicle", "AutoRepairDesc", "Repairs a damaged vehicle once when you enter it"));
            autoRepItem.CheckboxEvent += (sender, state) => { _autoRepair = state;
 ConfigManager.SetBool("Vehicle", "AutoRepair", _autoRepair); };
            vehMenu.AddItem(autoRepItem);

            // Vehicle God Mode
            var godItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "VehicleGodMode", "Invincible Vehicle"), _vehicleGodMode, Localization.Get("Vehicle", "VehicleGodModeDesc", "Indestructible"));
            godItem.CheckboxEvent += (sender, state) =>
            {
                _vehicleGodMode = state;

                ConfigManager.SetBool("Vehicle", "VehicleGodMode", _vehicleGodMode);
                Ped player = Game.Player.Character;
                if (_vehicleGodMode && player.IsInVehicle()) ApplyVehicleGodMode(player.CurrentVehicle, true);
                else RestoreVehicleGodMode();
            };
            vehMenu.AddItem(godItem);

            // Seatbelt (Anti-Fly through windshield & anti-fall from bikes)
            var seatItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "Seatbelt", "Seatbelt (Anti-Ejection)"), _seatbelt, Localization.Get("Vehicle", "SeatbeltDesc", "Prevents ejection in crashes"));
            seatItem.CheckboxEvent += (sender, state) =>
            {
                _seatbelt = state;

                ConfigManager.SetBool("Vehicle", "Seatbelt", _seatbelt);
                Ped player = Game.Player.Character;
                if (player != null && player.Exists())
                {
                    Function.Call(Hash.SET_PED_CONFIG_FLAG, player.Handle, 32, !_seatbelt);
                    Function.Call(Hash.SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE, player.Handle, _seatbelt ? 1 : 0);
                }
            };
            vehMenu.AddItem(seatItem);

            // Max Tuning
            var tuneItem = new UIMenuItem(Localization.Get("Vehicle", "MaxTuning", "Fully Upgrade Vehicle"), Localization.Get("Vehicle", "MaxTuningDesc", "Maximum performance upgrades"));
            tuneItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                if (player.IsInVehicle())
                {
                    Vehicle v = player.CurrentVehicle;
                    v.InstallModKit();
                    foreach (VehicleMod mod in Enum.GetValues(typeof(VehicleMod)))
                    {
                        try
                        {
                            int count = v.GetModCount(mod);
                            if (count > 0) v.SetMod(mod, count - 1, false);
                        }
                        catch { }
                    }
                    foreach (VehicleToggleMod toggle in Enum.GetValues(typeof(VehicleToggleMod)))
                    {
                        try { v.ToggleMod(toggle, true); } catch { }
                    }
                    v.WindowTint = VehicleWindowTint.PureBlack;
                    UI.Notify("~g~Vehicle fully upgraded!");
                }
                else
                {
                    UI.Notify("~r~Get in a vehicle first!");
                }
            };
            vehMenu.AddItem(tuneItem);

            // Speed Boost / NOS
            var boostItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "Nitro", "Nitro Boost / NOS (NUMPAD 0)"), _speedBoost, Localization.Get("Vehicle", "NitroDesc", "Hold NUMPAD 0 to activate nitro"));
            boostItem.CheckboxEvent += (sender, state) => { _speedBoost = state;
 ConfigManager.SetBool("Vehicle", "SpeedBoost", _speedBoost); };
            vehMenu.AddItem(boostItem);

            // Super Torque
            var torqueItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "SuperEngine", "Super Engine (3x Torque)"), _superTorque, Localization.Get("Vehicle", "SuperEngineDesc", "Multiplies engine torque"));
            torqueItem.CheckboxEvent += (sender, state) => { _superTorque = state;
 ConfigManager.SetBool("Vehicle", "SuperTorque", _superTorque); };
            vehMenu.AddItem(torqueItem);

            // Vehicle Jump (Ruiner 2000 style)
            var jumpItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "VehicleJump", "Vehicle Jump (Space)"), _vehicleJump, Localization.Get("Vehicle", "VehicleJumpDesc", "Press SPACE to jump"));
            jumpItem.CheckboxEvent += (sender, state) => { _vehicleJump = state;
 ConfigManager.SetBool("Vehicle", "VehicleJump", _vehicleJump); };
            vehMenu.AddItem(jumpItem);

            // Horn Jump
            var hornItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "HornJump", "Horn Jump"), _hornJump, Localization.Get("Vehicle", "HornJumpDesc", "Honk to perform a jump"));
            hornItem.CheckboxEvent += (sender, state) => { _hornJump = state;
 ConfigManager.SetBool("Vehicle", "HornJump", _hornJump); };
            vehMenu.AddItem(hornItem);

            // Auto-Flip
            var autoFlipItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "AutoFlip", "Auto-Flip (Anti-Rollover)"), _autoFlip, Localization.Get("Vehicle", "AutoFlipDesc", "Automatically rights the vehicle"));
            autoFlipItem.CheckboxEvent += (sender, state) => { _autoFlip = state;
 ConfigManager.SetBool("Vehicle", "AutoFlip", _autoFlip); };
            vehMenu.AddItem(autoFlipItem);

            // Drift Mode
            var driftItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "DriftMode", "Drift Mode"), _driftMode, Localization.Get("Vehicle", "DriftModeDesc", "Reduces rear grip for drifting"));
            driftItem.CheckboxEvent += (sender, state) =>
            {
                _driftMode = state;
                ConfigManager.SetBool("Vehicle", "DriftMode", _driftMode);
                if (!_driftMode) RestoreDriftMode();
                else if (Game.Player.Character != null && Game.Player.Character.Exists() && Game.Player.Character.IsInVehicle())
                    ApplyDriftMode(Game.Player.Character.CurrentVehicle);
            };
            vehMenu.AddItem(driftItem);

            // Drive on Water (Jesus Car)
            var waterItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "DriveOnWater", "Drive on Water (Jesus Car)"), _driveOnWater, Localization.Get("Vehicle", "DriveOnWaterDesc", "The car travels over water without sinking or damaging the engine"));
            waterItem.CheckboxEvent += (sender, state) => { _driveOnWater = state;
 ConfigManager.SetBool("Vehicle", "DriveOnWater", _driveOnWater); };
            vehMenu.AddItem(waterItem);

            // Spawn Instant Stunt Ramp
            var rampItem = new UIMenuItem(Localization.Get("Vehicle", "CreateRamp", "Create Stunt Ramp Ahead"), Localization.Get("Vehicle", "CreateRampDesc", "Spawns a jump ramp ahead"));
            rampItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                if (!player.IsInVehicle())
                {
                    UI.Notify("~r~You must be in a vehicle!");
                    return;
                }
                Vehicle v = player.CurrentVehicle;
                Vector3 rampPos = v.Position + v.ForwardVector * 16.0f;
                Model rampModel = new Model("prop_mp_ramp_01");
                rampModel.Request(1500);
                if (rampModel.IsInCdImage && rampModel.IsValid)
                {
                    Prop ramp = World.CreateProp(rampModel, rampPos, new Vector3(0, 0, v.Heading), false, false);
                    if (ramp != null && ramp.Exists())
                    {
                        Function.Call(Hash.PLACE_OBJECT_ON_GROUND_PROPERLY, ramp.Handle);
                        ramp.HasCollision = true;
                        _spawnedRamps.Add(ramp);
                        UI.Notify("~g~Jump ramp created! Accelerate!");
                    }
                }
                rampModel.MarkAsNoLongerNeeded();
            };
            vehMenu.AddItem(rampItem);

            var clearRamps = new UIMenuItem(Localization.Get("Vehicle", "RemoveRamps", "Remove Created Ramps"), Localization.Get("Vehicle", "RemoveRampsDesc", "Removes all created ramps"));
            clearRamps.Activated += (sender, selected) =>
            {
                int count = 0;
                foreach (var r in _spawnedRamps)
                {
                    if (r != null && r.Exists()) { r.Delete(); count++; }
                }
                _spawnedRamps.Clear();
                UI.Notify("~y~" + count + " ramp(s) removed.");
            };
            vehMenu.AddItem(clearRamps);

            // -------------------------------------------------------------
            // AUTOPILOT SUBMENU
            // -------------------------------------------------------------
            var autoMenu = menuPool.AddSubMenu(vehMenu, Localization.Get("Vehicle", "AutopilotMenu", "Smart Autopilot (Waypoint)"));

            var autoToggle = new UIMenuItem(Localization.Get("Vehicle", "StartAutopilot", "Start Autopilot"), Localization.Get("Vehicle", "StartAutopilotDesc", "Drives to the GPS waypoint"));
            autoToggle.Activated += (sender, selected) =>
            {
                StartAutopilot();
            };
            autoMenu.AddItem(autoToggle);

            var autoCancel = new UIMenuItem(Localization.Get("Vehicle", "CancelAutopilot", "Cancel Autopilot"), Localization.Get("Vehicle", "CancelAutopilotDesc", "Returns manual control"));
            autoCancel.Activated += (sender, selected) =>
            {
                CancelAutopilot(true, "~g~Autopilot cancelled manually.");
            };
            autoMenu.AddItem(autoCancel);

            var styleList = new List<dynamic> { Localization.Get("Vehicle", "StyleSafe", "Safe / Legal (Follows Rules)"), Localization.Get("Vehicle", "StyleFast", "Fast / Avoids Traffic"), Localization.Get("Vehicle", "StyleAggressive", "Escape / Aggressive (Insane / No Stops)") };
            var styleItem = new UIMenuListItem(Localization.Get("Vehicle", "DrivingStyle", "Driving Style"), styleList, 0, Localization.Get("Vehicle", "DrivingStyleDesc", "Changes AI traffic behavior"));
            styleItem.OnListChanged += (sender, index) =>
            {
                if (index == 0)
                {
                    _autopilotStyle = 786603;          // Safe, obeys traffic lights, polite
                    _autopilotAggressiveness = 0.0f;
                }
                else if (index == 1)
                {
                    _autopilotStyle = 1074528293;      // Fast, weaves through traffic
                    _autopilotAggressiveness = 0.6f;
                }
                else
                {
                    _autopilotStyle = 262143;          // 0x3FFFF: Ignore lights, wrong way, overtake, reckless
                    _autopilotAggressiveness = 1.0f;
                }

                Ped p = Game.Player.Character;
                if (_autopilotActive && p != null && p.Exists())
                {
                    Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, p.Handle, _autopilotAggressiveness);
                    Function.Call(Hash.SET_DRIVE_TASK_DRIVING_STYLE, p.Handle, _autopilotStyle);
                }
                UI.Notify("~b~Autopilot Style: ~w~" + styleList[index]);
            };
            autoMenu.AddItem(styleItem);

            var rammerItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "RammingMode", "Ramming Mode (Push Traffic)"), _trafficRammer, Localization.Get("Vehicle", "RammingModeDesc", "Pushes vehicles blocking your way"));
            rammerItem.CheckboxEvent += (sender, state) => { _trafficRammer = state;
 ConfigManager.SetBool("Vehicle", "TrafficRammer", _trafficRammer); };
            autoMenu.AddItem(rammerItem);

            var speedList = new List<dynamic> { Localization.Get("Vehicle", "Cruise60", "60 KM/H (Cruise)"), Localization.Get("Vehicle", "Cruise90", "90 KM/H (Default)"), Localization.Get("Vehicle", "Cruise130", "130 KM/H (Fast)"), Localization.Get("Vehicle", "Cruise180", "180 KM/H (Maximum)") };
            var speedVals = new[] { 17.0f, 25.0f, 36.0f, 50.0f };
            var speedItem = new UIMenuListItem(Localization.Get("Vehicle", "CruiseSpeed", "Cruise Speed"), speedList, 1, Localization.Get("Vehicle", "CruiseSpeedDesc", "Sets the AI target speed"));
            speedItem.OnListChanged += (sender, index) =>
            {
                _autopilotSpeed = speedVals[index];
                UI.Notify("~b~Autopilot Speed: ~w~" + speedList[index]);
            };
            autoMenu.AddItem(speedItem);

            // Speedometer Toggle
            var speedoItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "Speedometer", "Digital Speedometer"), _speedometer, Localization.Get("Vehicle", "SpeedometerDesc", "Displays speed and gear on screen"));
            speedoItem.CheckboxEvent += (sender, state) => { _speedometer = state;
 ConfigManager.SetBool("Vehicle", "Speedometer", _speedometer); };
            vehMenu.AddItem(speedoItem);

            // Speedometer Unit Selector (KM/H vs MPH)
            var unitList = new List<dynamic> { "KM/H", "MPH" };
            int initialUnitIdx = string.Equals(ConfigManager.SpeedUnit, "MPH", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var speedUnitItem = new UIMenuListItem(Localization.Get("Vehicle", "SpeedUnit", "Speedometer Unit"), unitList, initialUnitIdx, Localization.Get("Vehicle", "SpeedUnitDesc", "Switch between KM/H and MPH"));
            speedUnitItem.OnListChanged += (sender, index) =>
            {
                ConfigManager.SpeedUnit = (index == 1) ? "MPH" : "KM/H";
                ConfigManager.Save();
                UI.Notify(string.Format(Localization.Get("Vehicle", "SpeedUnitChanged", "~b~Speedometer unit: ~w~{0}"), ConfigManager.SpeedUnit));
            };
            vehMenu.AddItem(speedUnitItem);

            // Flip Vehicle
            var flipItem = new UIMenuItem(Localization.Get("Vehicle", "FlipVehicle", "Manually Flip Vehicle"), Localization.Get("Vehicle", "FlipVehicleDesc", "Rights the vehicle"));
            flipItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                if (player.IsInVehicle())
                {
                    Vehicle v = player.CurrentVehicle;
                    v.Rotation = new Vector3(0.0f, 0.0f, v.Rotation.Z);
                    v.PlaceOnGround();
                    UI.Notify("~b~Vehicle flipped upright!");
                }
            };
            vehMenu.AddItem(flipItem);

            // Open/Close Doors
            var openDoors = new UIMenuItem(Localization.Get("Vehicle", "OpenAllDoors", "Open All Doors"), Localization.Get("Vehicle", "OpenAllDoorsDesc", "Opens all doors, hood, and trunk"));
            openDoors.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                if (player.IsInVehicle())
                {
                    Vehicle v = player.CurrentVehicle;
                    for (int i = 0; i < 7; i++) v.OpenDoor((VehicleDoor)i, false, false);
                }
            };
            vehMenu.AddItem(openDoors);

            var closeDoors = new UIMenuItem(Localization.Get("Vehicle", "CloseAllDoors", "Close All Doors"), Localization.Get("Vehicle", "CloseAllDoorsDesc", "Closes all doors"));
            closeDoors.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                if (player.IsInVehicle())
                {
                    Vehicle v = player.CurrentVehicle;
                    for (int i = 0; i < 7; i++) v.CloseDoor((VehicleDoor)i, false);
                }
            };
            vehMenu.AddItem(closeDoors);

            // Delete Vehicle
            var delItem = new UIMenuItem(Localization.Get("Vehicle", "DeleteVehicle", "Delete Current Vehicle"), Localization.Get("Vehicle", "DeleteVehicleDesc", "Deletes the vehicle"));
            delItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                if (player.IsInVehicle())
                {
                    CancelAutopilot(true);
                    Vehicle v = player.CurrentVehicle;
                    v.Delete();
                    UI.Notify("~y~Vehicle deleted.");
                }
            };
            vehMenu.AddItem(delItem);

            // -------------------------------------------------------------
            // SPAWNER MENU
            // -------------------------------------------------------------
            var spawnerMenu = menuPool.AddSubMenu(vehMenu, Localization.Get("Vehicle", "SpawnVehicle", "Spawn Vehicle"));

            AddonManager.Load();
            var addonMenu = menuPool.AddSubMenu(spawnerMenu, Localization.Get("Vehicle", "SpawnAddonMenu", "My Add-On Vehicles (JSON)"));
            if (AddonManager.Addons.Count > 0)
            {
                foreach (var addon in AddonManager.Addons)
                {
                    string m = addon.model;
                    string label = string.IsNullOrEmpty(addon.name) ? m.ToUpper() : addon.name;
                    var item = new UIMenuItem(label, Localization.Get("Vehicle", "AddonModel", "Model: ") + m + " (" + addon.category + ")");
                    item.Activated += (sender, selected) => { SpawnVehicle(m); };
                    addonMenu.AddItem(item);
                }
            }
            else
            {
                addonMenu.AddItem(new UIMenuItem(Localization.Get("Vehicle", "NoVehicles", "No vehicles in addons.json"), Localization.Get("Vehicle", "NoVehiclesDesc", "Add models to Documents/AvernesTreiner/addons.json")));
            }

            var addonInputItem = new UIMenuItem(Localization.Get("Vehicle", "SpawnAddonInput", "Enter Add-On Model"), Localization.Get("Vehicle", "SpawnAddonInputDesc", "Enter any model name"));
            addonInputItem.Activated += (sender, selected) =>
            {
                string input = Game.GetUserInput(30);
                if (!string.IsNullOrEmpty(input))
                {
                    SpawnVehicle(input.Trim());
                }
            };
            spawnerMenu.AddItem(addonInputItem);

            var origCatsMenu = menuPool.AddSubMenu(spawnerMenu, Localization.Get("Vehicle", "SpawnCategories", "Original GTA V Categories"));

            AddCategoryMenu(origCatsMenu, menuPool, Localization.Get("Vehicle", "CategorySuper", "Super"), new[]
            {
                "adder", "autarch", "banshee2", "bullet", "cheetah", "cyclone", "deveste", "entity2", "entityxf",
                "emerus", "fmj", "furia", "gp1", "infernus", "italigtb", "italigtb2", "krieger", "le7b", "nero",
                "nero2", "osiris", "penetrator", "pfister811", "prototipo", "reaper", "sc1", "sheava", "sultanrs",
                "t20", "taipan", "tempesta", "tezeract", "thrax", "tigon", "turismor", "tyrant", "tyrus",
                "vacca", "vagner", "visione", "voltic", "xa21", "zentorno", "zorrusso"
            });

            AddCategoryMenu(origCatsMenu, menuPool, Localization.Get("Vehicle", "CategorySports", "Sports Cars"), new[]
            {
                "alpha", "banshee", "bestiagts", "blista2", "buffalo", "buffalo2", "buffalo3", "carbonizzare", "comet2",
                "comet3", "comet4", "comet5", "coquette", "coquette4", "drafter", "elegy", "elegy2", "feltzer2", "flashgt",
                "furoregt", "fusilade", "futo", "gb200", "hotring", "infernus2", "issi7", "italigto", "jester", "jester2",
                "jugular", "khamelion", "komoda", "kuruma", "locust", "lynx", "massacro", "massacro2", "neo", "neon",
                "ninef", "ninef2", "omnis", "paragon", "pariah", "penumbra", "raiden", "rapidgt", "raptor", "revolter",
                "ruston", "schafter2", "schafter3", "schafter4", "schlagen", "schwarzer", "sentinel3", "seven70", "specter",
                "streiter", "sugoi", "sultan", "surano", "tropos", "verlierer2"
            });

            AddCategoryMenu(origCatsMenu, menuPool, Localization.Get("Vehicle", "CategoryClassics", "Sports Classics"), new[]
            {
                "casco", "cheetah2", "coquette2", "coquette3", "deluxo", "dynasty", "gt500", "jb700", "jb7002", "mamba", "manana", "manana2", "michelli", "monroe", "nebula", "peyote", "peyote2",
                "pigalle", "retinue", "retinue2", "savestra", "stinger", "stingergt", "stromberg", "swinger", "torero",
                "turismo2", "viseris", "ztype", "zion3"
            });

            AddCategoryMenu(origCatsMenu, menuPool, Localization.Get("Vehicle", "CategoryMuscle", "Muscle Cars"), new[]
            {
                "blade", "buccaneer", "buccaneer2", "chino", "chino2", "clique", "coquette3", "deviant", "dominator",
                "dominator2", "dominator3", "dukes", "dukes2", "ellie", "faction", "faction2", "faction3", "gauntlet",
                "gauntlet2", "gauntlet3", "gauntlet4", "hermes", "hotknife", "hustler", "impaler", "imperator", "imperator2",
                "imperator3", "lurcher", "moonbeam", "moonbeam2", "nightshade", "phoenix", "picador", "ratloader", "ratloader2",
                "ruiner", "ruiner2", "sabregt", "sabregt2", "slamvan", "slamvan2", "slamvan3", "stalion", "tampa", "tampa3",
                "tulip", "vamos", "vigilante", "virgo", "virgo2", "virgo3", "voodoo", "voodoo2", "yosemite", "yosemite2"
            });

            AddCategoryMenu(origCatsMenu, menuPool, Localization.Get("Vehicle", "CategorySedans", "Sedans, Coupes & SUVs"), new[]
            {
                "asea", "asterope", "cog55", "cognoscenti", "emperor", "fugitive", "glendale", "ingot", "intruder",
                "premier", "primo", "primo2", "regina", "romero", "stanier", "stratum", "surge", "tailgater",
                "warrener", "washington", "exemplar", "f620", "felon", "felon2", "jackal", "oracle", "oracle2", "sentinel",
                "sentinel2", "windsor", "windsor2", "zion", "zion2", "baller", "baller2", "baller3", "baller4", "baller5",
                "baller6", "bjxl", "cavalcade", "cavalcade2", "contender", "dubsta", "dubsta2", "dubsta3", "fq2", "granger",
                "gresley", "habanero", "huntley", "landstalker", "landstalker2", "mesa", "mesa2", "mesa3", "novak", "patriot",
                "patriot2", "radi", "rebla", "rocoto", "seminole", "serrano", "toros", "xls"
            });

            AddCategoryMenu(origCatsMenu, menuPool, Localization.Get("Vehicle", "CategoryMotorcycles", "Motorcycles & Quad Bikes"), new[]
            {
                "akuma", "avarus", "bagger", "bati", "bati2", "bf400", "carbonrs", "chimera", "cliffhanger", "daemon",
                "daemon2", "defiler", "deathbike", "deathbike2", "deathbike3", "diablous", "diablous2", "double", "enduro",
                "esskey", "faggio", "faggio2", "faggio3", "fcr", "fcr2", "gargoyle", "hakuchou", "hakuchou2", "hexer",
                "innovation", "lectro", "manchez", "nemesis", "nightblade", "oppressor", "oppressor2", "pcj", "ratbike",
                "rrocket", "ruffian", "sanchez", "sanchez2", "sanctus", "shotaro", "sovereign", "stryder", "thrust", "vader",
                "vindicator", "vortex", "wolfsbane", "zombiea", "zombieb", "bmx", "cruiser", "fixter", "scorcher", "tribike",
                "tribike2", "tribike3"
            });

            AddCategoryMenu(origCatsMenu, menuPool, Localization.Get("Vehicle", "CategoryOffroad", "Off-Road"), new[]
            {
                "bfinjection", "bifta", "blazer", "blazer2", "blazer3", "blazer4", "blazer5", "bodhi2", "brawler", "bruiser",
                "bruiser2", "bruiser3", "caracara", "caracara2", "dloader", "dune", "dune2", "dune3", "dune4", "dune5",
                "everon", "freecrawler", "hellion", "insurgent", "insurgent2", "insurgent3", "kamacho", "kalahari", "marshall",
                "menacer", "monster", "nightshark", "outlaw", "rancherxl", "rebel", "rebel2", "riata", "sandking", "sandking2",
                "technical", "technical2", "technical3", "trophytruck", "trophytruck2", "vagrant", "wastelander", "zhaba"
            });

            AddCategoryMenu(origCatsMenu, menuPool, Localization.Get("Vehicle", "CategoryAircraft", "Helicopters & Planes"), new[]
            {
                "akula", "annihilator", "buzzard", "buzzard2", "cargobob", "cargobob2", "cargobob3", "cargobob4", "frogger",
                "frogger2", "havok", "hunter", "maverick", "savage", "seasparrow", "skylift", "supervolito", "supervolito2",
                "swift", "swift2", "valkyrie", "valkyrie2", "volatus", "alphaz1", "avenger", "avenger2", "besra", "blimp",
                "blimp2", "blimp3", "bombushka", "cargoplane", "cuban800", "dodo", "duster", "howard", "hydra", "jet",
                "lazer", "luxor", "luxor2", "mammatus", "microlight", "miljet", "mogul", "molotok", "nimbus", "nokota",
                "pyro", "rogue", "seabreeze", "shamal", "starling", "strikeforce", "titan", "tula", "velum", "velum2",
                "vestra", "volatol"
            });

            AddCategoryMenu(origCatsMenu, menuPool, Localization.Get("Vehicle", "CategorySpecial", "Boats, Military & Emergency"), new[]
            {
                "dinghy", "dinghy2", "dinghy3", "dinghy4", "jetmax", "marquis", "seashark", "seashark2", "seashark3",
                "speeder", "speeder2", "squalo", "submersible", "submersible2", "suntrap", "toro", "toro2", "tropic",
                "tropic2", "tug", "apc", "barracks", "barracks2", "barracks3", "chernobog", "crusader", "halftrack",
                "khanjali", "minitank", "rhino", "scarab", "scarab2", "scarab3", "thruster", "trailersmall2", "vetir",
                "ambulance", "fbi", "fbi2", "firetruk", "pbus", "police", "police2", "police3", "police4", "policeb",
                "policet", "pranger", "predator", "riot", "riot2", "sheriff", "sheriff2"
            });

            AddCategoryMenu(origCatsMenu, menuPool, Localization.Get("Vehicle", "CategoryAdditional", "Additional Vehicles (Build 2245)"), VehicleDatabase.AdditionalModels);
        }

        private void StartAutopilot()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                UI.Notify("~r~You must be in a vehicle to activate autopilot!");
                return;
            }

            if (!Game.IsWaypointActive)
            {
                UI.Notify("~r~Set a destination waypoint on the map first!");
                return;
            }

            Vehicle v = player.CurrentVehicle;
            if (v == null || !v.Exists()) return;

            Vector3 wp = World.GetWaypointPosition();
            OutputArgument groundZ = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, wp.X, wp.Y, 500.0f, groundZ, false))
            {
                wp.Z = groundZ.GetResult<float>();
            }
            else
            {
                wp.Z = player.Position.Z;
            }

            _autopilotDestination = wp;
            _autopilotStartTime = Game.GameTime;
            _brakeHoldCounter = 0;
            _stuckSinceTime = 0;
            _lastStuckTrafficCheckTime = 0;
            _lastAutopilotReissueTime = 0;
            _autopilotActive = true;

            // Configure AI driver capability
            Function.Call(Hash.SET_DRIVER_ABILITY, player.Handle, 1.0f);
            Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, player.Handle, _autopilotAggressiveness);
            Function.Call(Hash.SET_PED_KEEP_TASK, player.Handle, true);

            // Execute long range navigation native
            Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
                player.Handle,
                v.Handle,
                wp.X, wp.Y, wp.Z,
                _autopilotSpeed,
                _autopilotStyle,
                20.0f
            );

            // Close all trainer menus so controls don't clash
            if (_pool != null)
            {
                _pool.CloseAllMenus();
            }

            int dist = (int)World.GetDistance(v.Position, wp);
            UI.Notify("~g~Autopilot activated!\n~w~Destination: ~y~" + dist + "m\n~w~Hold ~r~BRAKE (S) ~w~or honk to cancel.");
        }

        private void CancelAutopilot(bool clearTask, string notifyMsg = null)
        {
            if (!_autopilotActive) return;
            _autopilotActive = false;
            _brakeHoldCounter = 0;
            _stuckSinceTime = 0;
            _lastStuckTrafficCheckTime = 0;
            _lastAutopilotReissueTime = 0;

            Ped player = Game.Player.Character;
            if (clearTask && player != null && player.Exists())
            {
                player.Task.ClearAll();
                if (player.IsInVehicle())
                {
                    player.CurrentVehicle.HandbrakeOn = false;
                }
            }

            if (!string.IsNullOrEmpty(notifyMsg))
            {
                UI.Notify(notifyMsg);
            }
        }

        private void AddCategoryMenu(UIMenu parent, MenuPool pool, string title, string[] models)
        {
            var catMenu = pool.AddSubMenu(parent, title);
            foreach (var modelName in models)
            {
                string m = modelName;
                var item = new UIMenuItem(m.ToUpper(), Localization.Get("Vehicle", "SpawnModel", "Spawn ") + m);
                item.Activated += (sender, selected) => { SpawnVehicle(m); };
                catMenu.AddItem(item);
            }
        }

        private void SpawnVehicle(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                UI.Notify("~r~Vehicle not found: empty model name.");
                return;
            }

            Ped player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                UI.Notify("~r~Vehicle spawn unavailable: player not found.");
                return;
            }

            Model model = new Model(modelName.Trim());
            try
            {
                if (!model.IsInCdImage || !model.IsValid)
                {
                    UI.Notify("~r~Vehicle not found: " + modelName);
                    return;
                }

                // A valid model must also finish streaming before CreateVehicle is called.
                if (!model.Request(2500) || !model.IsLoaded)
                {
                    UI.Notify("~r~Vehicle not found or could not be loaded: " + modelName);
                    return;
                }

                Vector3 spawnPos = player.Position + player.ForwardVector * 4.0f;
                Vehicle v = World.CreateVehicle(model, spawnPos, player.Heading);
                if (v == null || !v.Exists())
                {
                    UI.Notify("~r~Vehicle not found or could not be created: " + modelName);
                    return;
                }

                Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, v.Handle, true, true);
                v.IsPersistent = true;
                TrackSpawnedVehicle(v);
                Function.Call(Hash.SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER, v.Handle, true);
                v.NeedsToBeHotwired = false;

                if (!Function.Call<bool>(Hash.DECOR_IS_REGISTERED_AS_TYPE, "Player_Vehicle", 3))
                {
                    Function.Call(Hash.DECOR_REGISTER, "Player_Vehicle", 3);
                }
                Function.Call(Hash.DECOR_SET_INT, v.Handle, "Player_Vehicle", Game.Player.Handle);

                player.SetIntoVehicle(v, VehicleSeat.Driver);
                v.PlaceOnGround();
                if (_vehicleGodMode)
                    ApplyVehicleGodMode(v, true);
                UI.Notify("~g~Vehicle " + modelName.ToUpper() + " created!");
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Vehicle not found or spawn failed: " + modelName + " (" + ex.Message + ")");
            }
            finally
            {
                try { model.MarkAsNoLongerNeeded(); } catch { }
            }
        }

        public static void TrackSpawnedVehicle(Vehicle vehicle, bool protectFromCleanup = false)
        {
            if (vehicle == null || !vehicle.Exists()) return;
            int handle = vehicle.Handle;
            _trackedSpawnedVehicles[handle] = vehicle;
            if (protectFromCleanup) _protectedSavedVehicles.Add(handle);
            else _protectedSavedVehicles.Remove(handle);
            if (Function.Call<bool>(Hash.GET_MISSION_FLAG))
                _missionSpawnedVehicles.Add(handle);
            _abandonedSince.Remove(handle);
        }

        private void CleanupExplodedSpawnedVehicles(int now)
        {
            if (now - _lastExplodedVehicleCheckTime < 250) return;
            _lastExplodedVehicleCheckTime = now;
            _explodedVehicleRemovalBuffer.Clear();

            foreach (var entry in _trackedSpawnedVehicles)
            {
                int handle = entry.Key;
                Vehicle vehicle = entry.Value;
                if (vehicle == null || !vehicle.Exists() ||
                    !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle))
                {
                    _explodedVehicleRemovalBuffer.Add(handle);
                    continue;
                }

                // Only inspect entities explicitly registered as trainer-spawned.
                bool exploded = Function.Call<bool>(Hash.IS_ENTITY_DEAD, handle) ||
                    Function.Call<float>(Hash.GET_VEHICLE_ENGINE_HEALTH, handle) <= -4000.0f;
                if (!exploded) continue;

                try
                {
                    Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, handle, true, true);
                    using (OutputArgument vehicleHandle = new OutputArgument(handle))
                        Function.Call(Hash.DELETE_VEHICLE, vehicleHandle);
                }
                catch { }

                if (Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle))
                {
                    try { if (vehicle.Exists()) vehicle.Delete(); } catch { }
                }

                _explodedVehicleRemovalBuffer.Add(handle);
            }

            for (int i = 0; i < _explodedVehicleRemovalBuffer.Count; i++)
                ForgetTrackedVehicle(_explodedVehicleRemovalBuffer[i]);
        }

        private void CleanupAbandonedSpawnedVehicles(Ped player, int now)
        {
            if (player == null || !player.Exists()) return;

            // Capture every currently tracked, unsaved trainer vehicle when a
            // mission starts, not only vehicles spawned from inside the mission.
            // This also cleans up a trainer-spawned car left behind before driving
            // away to begin a mission. Occupancy is rechecked before deletion.
            if (Function.Call<bool>(Hash.GET_MISSION_FLAG))
            {
                if (!_missionCleanupTrackingActive)
                {
                    _missionCleanupTrackingActive = true;
                    foreach (var trackedEntry in _trackedSpawnedVehicles)
                    {
                        if (!_protectedSavedVehicles.Contains(trackedEntry.Key))
                            _missionSpawnedVehicles.Add(trackedEntry.Key);
                    }
                }
                _abandonedSince.Clear();
                return;
            }
            _missionCleanupTrackingActive = false;

            _trackedVehicleRemovalBuffer.Clear();
            foreach (var entry in _trackedSpawnedVehicles)
            {
                int handle = entry.Key;
                Vehicle vehicle = entry.Value;
                if (vehicle == null || !vehicle.Exists() ||
                    !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle))
                {
                    _trackedVehicleRemovalBuffer.Add(handle);
                    continue;
                }

                // Vehicles spawned from the Saved Vehicles catalog are user-managed
                // and must never be removed by automatic abandoned-vehicle cleanup.
                if (_protectedSavedVehicles.Contains(handle))
                {
                    _abandonedSince.Remove(handle);
                    continue;
                }

                if (_pendingVehicleDeletion.Contains(handle)) continue;

                if (player.IsInVehicle() && player.CurrentVehicle.Handle == handle)
                {
                    _abandonedSince.Remove(handle);
                    continue;
                }

                int driverHandle = Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, handle, -1);
                int passengerCount = Function.Call<int>(Hash.GET_VEHICLE_NUMBER_OF_PASSENGERS, handle);
                bool occupied = driverHandle != 0 || passengerCount > 0;

                // Mission vehicles are queued as soon as the mission ends, but only
                // after confirming that no player or NPC is inside.
                if (_missionSpawnedVehicles.Contains(handle))
                {
                    if (occupied)
                    {
                        _abandonedSince.Remove(handle);
                        continue;
                    }

                    QueueVehicleDeletion(handle);
                    continue;
                }

                if (World.GetDistance(player.Position, vehicle.Position) <= 150.0f)
                {
                    _abandonedSince.Remove(handle);
                    continue;
                }

                // Never remove a trainer vehicle that is occupied or still moving.
                if (occupied || vehicle.Speed > 0.5f)
                {
                    _abandonedSince.Remove(handle);
                    continue;
                }

                int abandonedAt;
                if (!_abandonedSince.TryGetValue(handle, out abandonedAt))
                {
                    _abandonedSince[handle] = now;
                    continue;
                }

                if (now - abandonedAt >= 60000)
                    QueueVehicleDeletion(handle);
            }

            for (int i = 0; i < _trackedVehicleRemovalBuffer.Count; i++)
                ForgetTrackedVehicle(_trackedVehicleRemovalBuffer[i]);
        }

        private void QueueVehicleDeletion(int handle)
        {
            if (_pendingVehicleDeletion.Add(handle))
                _vehicleDeletionQueue.Enqueue(handle);
        }

        private void ProcessNextVehicleDeletion(Ped player, int now)
        {
            if (_vehicleDeletionQueue.Count == 0) return;

            int handle = _vehicleDeletionQueue.Dequeue();
            Vehicle vehicle;
            if (!_trackedSpawnedVehicles.TryGetValue(handle, out vehicle) ||
                vehicle == null || !vehicle.Exists() ||
                !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle))
            {
                ForgetTrackedVehicle(handle);
                return;
            }

            // Revalidate all safety conditions immediately before deletion. The
            // player or an NPC may have entered the vehicle while it was queued.
            if (Function.Call<bool>(Hash.GET_MISSION_FLAG))
            {
                _pendingVehicleDeletion.Remove(handle);
                return;
            }

            if (player == null || !player.Exists())
            {
                _pendingVehicleDeletion.Remove(handle);
                return;
            }

            if (player.IsInVehicle() && player.CurrentVehicle.Handle == handle)
            {
                _abandonedSince.Remove(handle);
                _pendingVehicleDeletion.Remove(handle);
                return;
            }

            int driverHandle = Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, handle, -1);
            int passengerCount = Function.Call<int>(Hash.GET_VEHICLE_NUMBER_OF_PASSENGERS, handle);
            if (driverHandle != 0 || passengerCount > 0)
            {
                _abandonedSince.Remove(handle);
                _pendingVehicleDeletion.Remove(handle);
                return;
            }

            bool missionVehicle = _missionSpawnedVehicles.Contains(handle);
            if (!missionVehicle)
            {
                if (_protectedSavedVehicles.Contains(handle) ||
                    World.GetDistance(player.Position, vehicle.Position) <= 150.0f ||
                    vehicle.Speed > 0.5f)
                {
                    _abandonedSince.Remove(handle);
                    _pendingVehicleDeletion.Remove(handle);
                    return;
                }

                int abandonedAt;
                if (!_abandonedSince.TryGetValue(handle, out abandonedAt) ||
                    now - abandonedAt < 60000)
                {
                    _pendingVehicleDeletion.Remove(handle);
                    return;
                }
            }

            try
            {
                vehicle.IsPersistent = true;
                using (OutputArgument vehicleHandle = new OutputArgument(vehicle))
                    Function.Call(Hash.DELETE_VEHICLE, vehicleHandle);
            }
            catch { }

            // Some entity wrappers/native calls can fail without deleting the
            // entity. Verify the actual game handle, then try the wrapper fallback.
            if (Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle))
            {
                try { if (vehicle.Exists()) vehicle.Delete(); } catch { }
            }

            if (!Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle))
            {
                ForgetTrackedVehicle(handle);
                return;
            }

            // Keep the handle tracked and retry on the next queue interval.
            // The original abandonment timestamp is retained, so retries do not
            // restart the 60-second eligibility period.
            _vehicleDeletionQueue.Enqueue(handle);
        }

        private void ForgetTrackedVehicle(int handle)
        {
            _trackedSpawnedVehicles.Remove(handle);
            _abandonedSince.Remove(handle);
            _missionSpawnedVehicles.Remove(handle);
            _protectedSavedVehicles.Remove(handle);
            _pendingVehicleDeletion.Remove(handle);
        }

        private void ApplyVehicleGodMode(Vehicle vehicle, bool enabled)
        {
            if (vehicle == null || !vehicle.Exists()) return;
            if (_godModeVehicle != null && _godModeVehicle.Exists() && _godModeVehicle.Handle != vehicle.Handle)
                RestoreVehicleGodMode();

            bool newTarget = _godModeVehicle == null || !_godModeVehicle.Exists() ||
                _godModeVehicle.Handle != vehicle.Handle;
            if (enabled && newTarget)
                Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicle.Handle);

            vehicle.IsInvincible = enabled;
            vehicle.CanBeVisiblyDamaged = !enabled;
            Function.Call(Hash.SET_ENTITY_PROOFS, vehicle.Handle,
                enabled, enabled, enabled, enabled, enabled, enabled, enabled, enabled);
            Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, vehicle.Handle, !enabled);
            // Zero the vehicle damage scale while protected; deformation repair alone
            // removes dents but does not suppress every cosmetic collision mark.
            Function.Call((Hash)0x4E20D2A627011E8E, vehicle.Handle, enabled ? 0.0f : 1.0f);
            Function.Call(Hash.SET_VEHICLE_STRONG, vehicle.Handle, enabled);
            _godModeVehicle = enabled ? vehicle : null;
        }

        private void RestoreVehicleGodMode()
        {
            if (_godModeVehicle != null && _godModeVehicle.Exists())
            {
                _godModeVehicle.IsInvincible = false;
                _godModeVehicle.CanBeVisiblyDamaged = true;
                Function.Call(Hash.SET_ENTITY_PROOFS, _godModeVehicle.Handle,
                    false, false, false, false, false, false, false, false);
                Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, _godModeVehicle.Handle, true);
                Function.Call((Hash)0x4E20D2A627011E8E, _godModeVehicle.Handle, 1.0f);
                Function.Call(Hash.SET_VEHICLE_STRONG, _godModeVehicle.Handle, false);
            }
            _godModeVehicle = null;
        }

        private void ApplyDriftMode(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return;
            if (_driftVehicle != null && _driftVehicle.Exists() && _driftVehicle.Handle != vehicle.Handle)
                RestoreDriftMode();
            Function.Call(Hash.SET_VEHICLE_REDUCE_GRIP, vehicle.Handle, true);
            _driftVehicle = vehicle;
        }

        private void RestoreDriftMode()
        {
            if (_driftVehicle != null && _driftVehicle.Exists())
                Function.Call(Hash.SET_VEHICLE_REDUCE_GRIP, _driftVehicle.Handle, false);
            _driftVehicle = null;
        }

        public void ReloadSettings()
        {
            bool wasGodMode = _vehicleGodMode;
            bool wasDriftMode = _driftMode;
            bool wasSeatbelt = _seatbelt;
            bool wasSuperTorque = _superTorque;

            _vehicleGodMode = ConfigManager.GetBool("Vehicle", "VehicleGodMode", false);
            _speedBoost = ConfigManager.GetBool("Vehicle", "SpeedBoost", false);
            _hornJump = ConfigManager.GetBool("Vehicle", "HornJump", false);
            _vehicleJump = ConfigManager.GetBool("Vehicle", "VehicleJump", false);
            _driveOnWater = ConfigManager.GetBool("Vehicle", "DriveOnWater", false);
            _speedometer = ConfigManager.GetBool("Vehicle", "Speedometer", false);
            _driftMode = ConfigManager.GetBool("Vehicle", "DriftMode", false);
            _seatbelt = ConfigManager.GetBool("Vehicle", "Seatbelt", true);
            _autoRepair = ConfigManager.GetBool("Vehicle", "AutoRepair", false);
            _superTorque = ConfigManager.GetBool("Vehicle", "SuperTorque", false);
            _autoFlip = ConfigManager.GetBool("Vehicle", "AutoFlip", false);
            _trafficRammer = ConfigManager.GetBool("Vehicle", "TrafficRammer", false);

            if (wasGodMode && !_vehicleGodMode) RestoreVehicleGodMode();
            if (wasDriftMode && !_driftMode) RestoreDriftMode();

            Ped player = Game.Player.Character;
            if (wasSeatbelt && !_seatbelt && player != null && player.Exists())
            {
                Function.Call(Hash.SET_PED_CONFIG_FLAG, player.Handle, 32, true);
                Function.Call(Hash.SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE, player.Handle, 0);
            }
            if (wasSuperTorque && !_superTorque && player != null && player.Exists() && player.IsInVehicle())
                player.CurrentVehicle.EnginePowerMultiplier = 1.0f;
            _lastEnteredVehicleHandle = 0;
        }

        public void OnTick()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            int currentTime = Game.GameTime;
            CleanupExplodedSpawnedVehicles(currentTime);
            if (currentTime - _lastSpawnCleanupTime >= 1000)
            {
                _lastSpawnCleanupTime = currentTime;
                CleanupAbandonedSpawnedVehicles(player, currentTime);
            }
            if (_vehicleDeletionQueue.Count > 0 && currentTime - _lastVehicleDeletionTime >= 500)
            {
                _lastVehicleDeletionTime = currentTime;
                ProcessNextVehicleDeletion(player, currentTime);
            }
            bool maintenanceTick = currentTime - _lastMaintenanceTime >= 250;
            if (maintenanceTick) _lastMaintenanceTime = currentTime;

            // Seatbelt
            if (_seatbelt && maintenanceTick)
            {
                Function.Call(Hash.SET_PED_CONFIG_FLAG, player.Handle, 32, false);
                Function.Call(Hash.SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE, player.Handle, 1);
            }

            if (player.IsInVehicle())
            {
                Vehicle v = player.CurrentVehicle;
                if (v != null && v.Exists())
                {
                    if (_vehicleGodMode)
                    {
                        if (maintenanceTick)
                        {
                            ApplyVehicleGodMode(v, true);
                            // Periodic full repair clears minor collision scuffs that
                            // deformation-only repair does not remove. Vehicle mods,
                            // paint and saved-vehicle data are not rewritten.
                            Function.Call(Hash.SET_VEHICLE_FIXED, v.Handle);
                        }
                        // Collision deformation can be applied between maintenance ticks.
                        // Reapply the deformation fix each script tick without using
                        // SET_VEHICLE_FIXED, which can reset vehicle customization.
                        Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, v.Handle);
                        Function.Call((Hash)0x4E20D2A627011E8E, v.Handle, 0.0f);
                    }
                    else if (_godModeVehicle != null)
                        RestoreVehicleGodMode();

                    // Auto repair runs once per vehicle entry, never as a continuous repair loop.
                    if (_autoRepair && v.Handle != _lastEnteredVehicleHandle)
                    {
                        _lastEnteredVehicleHandle = v.Handle;
                        // Use the game repair natives directly; Vehicle.Repair() alone can
                        // leave deformation/body state behind on some vehicle classes.
                        Function.Call(Hash.SET_VEHICLE_FIXED, v.Handle);
                        Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, v.Handle);
                        Function.Call(Hash.SET_VEHICLE_DIRT_LEVEL, v.Handle, 0.0f);
                        Function.Call(Hash.SET_VEHICLE_ENGINE_HEALTH, v.Handle, 1000.0f);
                        Function.Call(Hash.SET_VEHICLE_BODY_HEALTH, v.Handle, 1000.0f);
                        Function.Call(Hash.SET_VEHICLE_PETROL_TANK_HEALTH, v.Handle, 1000.0f);
                        UI.Notify("~g~Vehicle repaired on entry.");
                    }

                    // Nitro is bound to the dedicated numeric keypad 0 key.
                    // Keep the original force and speed cap, with horizontal force to avoid unwanted lift.
                    if (_speedBoost && Game.IsKeyPressed(Keys.NumPad0))
                    {
                        Vector3 forward = v.ForwardVector;
                        float horizontalLength = (float)Math.Sqrt(forward.X * forward.X + forward.Y * forward.Y);
                        if (horizontalLength > 0.001f)
                        {
                            Vector3 horizontalForce = new Vector3(
                                forward.X / horizontalLength * 1.8f,
                                forward.Y / horizontalLength * 1.8f,
                                0.0f);
                            v.ApplyForce(horizontalForce);
                        }
                        v.MaxSpeed = 400.0f;
                    }

                    if (_superTorque && Game.IsControlPressed(0, GTA.Control.VehicleAccelerate))
                    {
                        v.EnginePowerMultiplier = 2.5f;
                    }

                    if (_driftMode && maintenanceTick)
                        ApplyDriftMode(v);
                    else if (!_driftMode && _driftVehicle != null)
                        RestoreDriftMode();

                    // Horn Jump
                    if (_hornJump && Game.IsControlJustPressed(0, GTA.Control.VehicleHorn))
                    {
                        v.ApplyForce(new Vector3(0.0f, 0.0f, 9.0f));
                    }

                    // Vehicle Jump (Space)
                    if (_vehicleJump && Game.IsControlJustPressed(0, GTA.Control.VehicleHandbrake))
                    {
                        if (v.IsOnAllWheels || v.HeightAboveGround < 1.5f)
                        {
                            v.ApplyForce(new Vector3(0.0f, 0.0f, 9.5f));
                        }
                    }

                    // Auto-Flip (if upside down)
                    if (_autoFlip && currentTime - _lastAutoFlipTime >= 100)
                    {
                        _lastAutoFlipTime = currentTime;
                        if (v.UpVector.Z < -0.1f || Math.Abs(v.Rotation.Y) > 75.0f || Math.Abs(v.Rotation.X) > 75.0f)
                        {
                            v.Rotation = new Vector3(0f, 0f, v.Rotation.Z);
                            v.PlaceOnGround();
                        }
                    }

                    // Drive on Water (Jesus Car)
                    if (_driveOnWater && currentTime - _lastWaterCheckTime >= 100)
                    {
                        _lastWaterCheckTime = currentTime;
                        OutputArgument outHeight = new OutputArgument();
                        bool hasWater = Function.Call<bool>(Hash.GET_WATER_HEIGHT, v.Position.X, v.Position.Y, v.Position.Z, outHeight);
                        if (hasWater)
                        {
                            float waterHeight = outHeight.GetResult<float>();
                            if (v.Position.Z <= waterHeight + 1.2f && v.Position.Z >= waterHeight - 4.0f)
                            {
                                Vector3 vel = v.Velocity;
                                v.Velocity = new Vector3(vel.X, vel.Y, 0f);
                                v.Position = new Vector3(v.Position.X, v.Position.Y, waterHeight + 0.2f);
                                v.Rotation = new Vector3(0f, 0f, v.Rotation.Z);
                                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, v.Handle, true, true, false);
                                Function.Call(Hash.SET_VEHICLE_UNDRIVEABLE, v.Handle, false);
                            }
                        }
                    }

                    // Digital Speedometer HUD (KM/H & MPH Support)
                    if (_speedometer)
                    {
                        bool isMph = string.Equals(ConfigManager.SpeedUnit, "MPH", StringComparison.OrdinalIgnoreCase);
                        float speedVal = isMph ? (v.Speed * 2.236936f) : (v.Speed * 3.6f);
                        string unitStr = isMph ? "MPH" : "KM/H";
                        int gear = v.CurrentGear;
                        string gearText = (gear == 0) ? "R" : gear.ToString();
                        string gearLabel = Localization.Get("Vehicle", "GearLabel", isMph ? "G" : "M");
                        string text = string.Format("{0:0} {1}  |  {2}: {3}", speedVal, unitStr, gearLabel, gearText);
                        new UIText(text, new System.Drawing.Point(UI.WIDTH - 200, UI.HEIGHT - 70), 0.6f, System.Drawing.Color.FromArgb(255, 0, 230, 255), GTA.Font.ChaletComprimeCologne, false, true, true).Draw();
                    }

                    // ---------------------------------------------------------
                    // Robust Autopilot Loop
                    // ---------------------------------------------------------
                    if (_autopilotActive)
                    {
                        // Check if player erased waypoint
                        if (!Game.IsWaypointActive)
                        {
                            CancelAutopilot(true, "~y~Autopilot ended: Waypoint removed.");
                            return;
                        }

                        // Check arrival distance
                        float dist = World.GetDistance(v.Position, _autopilotDestination);
                        if (dist < 22.0f)
                        {
                            CancelAutopilot(true, "~g~Destination reached successfully!");
                            return;
                        }

                        float currentKmh = v.Speed * 3.6f;

                        // Traffic Rammer (Plow through blocking traffic)
                        if ((_trafficRammer || _autopilotAggressiveness >= 1.0f) &&
                            currentTime - _lastTrafficRammerCheckTime >= 350)
                        {
                            _lastTrafficRammerCheckTime = currentTime;
                            Vehicle[] ahead = World.GetNearbyVehicles(v.Position + v.ForwardVector * 4.5f, 3.5f);
                            foreach (var b in ahead)
                            {
                                if (b != null && b.Exists() && b != v)
                                {
                                    Vector3 pushDir = (b.Position - v.Position).Normalized;
                                    b.ApplyForce(pushDir * 2.5f + new Vector3(0, 0, 0.4f));
                                }
                            }
                        }

                        // Anti-Stuck & Overtake Watchdog (time-based, not frame-count based)
                        if (currentKmh < 3.0f && (currentTime - _autopilotStartTime > 2000))
                        {
                            if (_stuckSinceTime == 0) _stuckSinceTime = currentTime;
                            int stuckDuration = currentTime - _stuckSinceTime;

                            if (_autopilotAggressiveness > 0.3f)
                            {
                                // Nearby traffic scans are limited to once per second.
                                if (stuckDuration > 1000 && currentTime - _lastStuckTrafficCheckTime >= 1000)
                                {
                                    _lastStuckTrafficCheckTime = currentTime;
                                    v.SoundHorn(800);

                                    Vehicle[] blockingVehs = World.GetNearbyVehicles(v.Position + v.ForwardVector * 8.0f, 7.0f);
                                    foreach (var b in blockingVehs)
                                    {
                                        if (b != null && b.Exists() && b != v)
                                        {
                                            Ped d = b.GetPedOnSeat(VehicleSeat.Driver);
                                            if (d != null && d.Exists() && !d.IsPlayer)
                                                d.Task.FleeFrom(player);
                                        }
                                    }
                                }

                                // Re-issue the driving task at a bounded interval.
                                if (stuckDuration > 2500 && currentTime - _lastAutopilotReissueTime >= 2500)
                                {
                                    _lastAutopilotReissueTime = currentTime;
                                    v.Speed = 8.0f;
                                    v.ApplyForce(v.ForwardVector * 2.0f);

                                    Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, player.Handle, 1.0f);
                                    Function.Call(Hash.SET_DRIVE_TASK_DRIVING_STYLE, player.Handle, 262143);
                                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
                                        player.Handle,
                                        v.Handle,
                                        _autopilotDestination.X, _autopilotDestination.Y, _autopilotDestination.Z,
                                        _autopilotSpeed,
                                        262143,
                                        20.0f
                                    );
                                }
                            }
                        }
                        else
                        {
                            _stuckSinceTime = 0;
                        }

                        // Draw Autopilot HUD status
                        string hudText = string.Format("~b~AUTOPILOT ~w~| Distance: ~y~{0:0}m ~w~| Speed: ~g~{1:0} km/h ~w~| Hold ~r~[S] ~w~or honk", dist, currentKmh);
                        new UIText(hudText, new System.Drawing.Point(UI.WIDTH / 2, UI.HEIGHT - 35), 0.45f, System.Drawing.Color.White, GTA.Font.ChaletComprimeCologne, true, false, true).Draw();

                        // Cancellation check (only after 1.5s grace period)
                        if (Game.GameTime - _autopilotStartTime > 1500)
                        {
                            // 1. Horn key
                            if (Game.IsControlJustPressed(0, GTA.Control.VehicleHorn) || Game.IsKeyPressed(Keys.E))
                            {
                                CancelAutopilot(true, "~g~Autopilot cancelled by horn.");
                                return;
                            }

                            // 2. Handbrake
                            if (Game.IsControlJustPressed(0, GTA.Control.VehicleHandbrake) || Game.IsKeyPressed(Keys.Space))
                            {
                                CancelAutopilot(true, "~g~Handbrake engaged: Manual control restored.");
                                return;
                            }

                            // 3. Holding brake / reverse
                            if (Game.IsControlPressed(0, GTA.Control.VehicleBrake) || Game.IsKeyPressed(Keys.S))
                            {
                                _brakeHoldCounter++;
                                if (_brakeHoldCounter > 15) // ~250ms hold
                                {
                                    CancelAutopilot(true, "~g~Brake pressed: Manual control restored.");
                                    return;
                                }
                            }
                            else
                            {
                                _brakeHoldCounter = 0;
                            }
                        }
                    }
                }
            }
            else
            {
                _lastEnteredVehicleHandle = 0;
                if (_driftVehicle != null) RestoreDriftMode();
                if (_godModeVehicle != null) RestoreVehicleGodMode();
                if (_autopilotActive)
                {
                    CancelAutopilot(false);
                }
            }
        }
    }
}
