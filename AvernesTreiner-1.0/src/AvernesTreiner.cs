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

namespace AvernesTreiner.Core
{
    public class AddonVehicle
    {
        private string _name;
        private string _model;
        private string _category;

        public string name { get { return _name; } set { _name = value; } }
        public string model { get { return _model; } set { _model = value; } }
        public string category { get { return _category; } set { _category = value; } }
    }

    public static class AddonManager
    {
        private static readonly string AddonsPath = @"scripts\AvernesTreiner\addons.json";
        private static List<AddonVehicle> _addons = new List<AddonVehicle>();

        public static List<AddonVehicle> Addons
        {
            get { return _addons; }
        }

        public static void Load()
        {
            try
            {
                _addons.Clear();
                if (File.Exists(AddonsPath))
                {
                    string json = File.ReadAllText(AddonsPath);
                    var serializer = new JavaScriptSerializer();
                    var list = serializer.Deserialize<List<AddonVehicle>>(json);
                    if (list != null)
                    {
                        _addons = list;
                    }
                }
            }
            catch { }
        }
    }
}

namespace AvernesTreiner.Core
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = @"scripts\AvernesTreiner\Config.ini";
        private static ScriptSettings _settings;

        private static string _language = "en-US";
        private static string _menuKey = "F4";
        private static string _version = "v1.0.0";
        private static string _title = "AvernesTreiner";
        private static string _speedUnit = "KM/H";

        public static string Language { get { return _language; } set { _language = value; } }
        public static string MenuKey { get { return _menuKey; } set { _menuKey = value; } }
        public static string Version { get { return _version; } set { _version = value; } }
        public static string Title { get { return _title; } set { _title = value; } }
        public static string SpeedUnit { get { return _speedUnit; } set { _speedUnit = value; } }

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    _settings = ScriptSettings.Load(ConfigPath);
                    _language = _settings.GetValue("General", "Language", "en-US");
                    _menuKey = _settings.GetValue("General", "MenuKey", "F4");
                    _version = _settings.GetValue("General", "Version", "v1.0.0");
                    _title = _settings.GetValue("General", "Title", "AvernesTreiner");
                    _speedUnit = _settings.GetValue("General", "SpeedUnit", "KM/H");
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                if (_settings == null) _settings = ScriptSettings.Load(ConfigPath);
                _settings.SetValue("General", "Language", _language);
                _settings.SetValue("General", "MenuKey", _menuKey);
                _settings.SetValue("General", "Version", _version);
                _settings.SetValue("General", "Title", _title);
                _settings.SetValue("General", "SpeedUnit", _speedUnit);
                _settings.Save();
            }
            catch { }
        }
    }

    public static class Localization
    {
        private static ScriptSettings _langSettings;

        public static void LoadLanguage(string langCode)
        {
            try
            {
                string path = string.Format(@"scripts\AvernesTreiner\Languages\{0}.ini", langCode);
                if (!File.Exists(path))
                {
                    path = @"scripts\AvernesTreiner\Languages\en-US.ini";
                }
                if (File.Exists(path))
                {
                    _langSettings = ScriptSettings.Load(path);
                }
            }
            catch { }
        }

        public static string Get(string section, string key, string fallback)
        {
            try
            {
                if (_langSettings != null)
                {
                    return _langSettings.GetValue(section, key, fallback);
                }
            }
            catch { }
            return fallback;
        }

        public static string Get(string section, string key)
        {
            return Get(section, key, "");
        }
    }
}

namespace AvernesTreiner.Core
{
    public interface IAvernesModule
    {
        string Name { get; }
        void Initialize(UIMenu mainMenu, MenuPool menuPool);
        void OnTick();
    }
}

namespace AvernesTreiner
{
    public class AvernesTreinerScript : Script
    {
        private MenuPool _menuPool;
        private UIMenu _mainMenu;
        private List<IAvernesModule> _modules;
        private Keys _toggleKey = Keys.F4;
        private int _lastToggleTime = 0;

        public AvernesTreinerScript()
        {
            try
            {
                ConfigManager.Load();
                Localization.LoadLanguage(ConfigManager.Language);

                try
                {
                    _toggleKey = (Keys)Enum.Parse(typeof(Keys), ConfigManager.MenuKey, true);
                }
                catch
                {
                    _toggleKey = Keys.F4;
                }

                _menuPool = new MenuPool();
                _menuPool.MouseEdgeEnabled = false;
                _menuPool.ResetCursorOnOpen = false;

                string title = Localization.Get("General", "MenuTitle", "AvernesTreiner");
                string subtitle = Localization.Get("General", "MenuSubtitle", "v1.0.0");
                _mainMenu = new UIMenu(title, subtitle);
                _menuPool.Add(_mainMenu);

                _modules = new List<IAvernesModule>
                {
                    new PlayerModule(),
                    new VehicleModule(),
                    new VehicleCustomizationModule(),
                    new SkinChangerModule(),
                    new BodyguardModule(),
                    new MiscPowersModule(),
                    new AnimationModule(),
                    new WeaponModule(),
                    new TeleportModule(),
                    new WorldModule(),
                    new SettingsModule()
                };

                foreach (var module in _modules)
                {
                    try
                    {
                        module.Initialize(_mainMenu, _menuPool);
                    }
                    catch { }
                }

                _menuPool.RefreshIndex();

                Tick += OnTick;
                KeyDown += OnKeyDown;

                Interval = 0;

                UI.Notify("~b~AvernesTreiner v1.0.0 ~w~loaded!\nPress ~g~" + _toggleKey.ToString() + " ~w~to open.");
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Error starting AvernesTreiner: " + ex.Message);
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_menuPool != null)
            {
                _menuPool.ProcessControl();
                _menuPool.Draw();

                if (_menuPool.IsAnyMenuOpen())
                {
                    Function.Call(Hash._SHOW_CURSOR_THIS_FRAME, false);
                }
            }

            if (_modules != null)
            {
                for (int i = 0; i < _modules.Count; i++)
                {
                    try
                    {
                        _modules[i].OnTick();
                    }
                    catch { }
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == _toggleKey)
            {
                int currentTime = Game.GameTime;
                if (currentTime - _lastToggleTime < 300) return;
                _lastToggleTime = currentTime;

                if (_menuPool != null)
                {
                    if (_menuPool.IsAnyMenuOpen())
                    {
                        _menuPool.CloseAllMenus();
                    }
                    else
                    {
                        _mainMenu.Visible = true;
                    }
                }
            }
        }
    }
}

namespace AvernesTreiner.Modules
{
    public class AnimationModule : IAvernesModule
    {
        public string Name { get { return "Animations"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var animMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuAnimations", "Animations & Scenarios"));

            var stopItem = new UIMenuItem(
                Localization.Get("Animations", "StopAnim", "Stop Animation"),
                Localization.Get("Animations", "StopAnimDesc", "Return to the normal stance")
            );
            stopItem.Activated += (sender, selected) =>
            {
                Ped p = Game.Player.Character;
                if (p != null && p.Exists())
                {
                    p.Task.ClearAllImmediately();
                    p.Task.ClearAll();
                    Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, p.Handle);
                    UI.Notify("~y~Animation cancelled.");
                }
            };
            animMenu.AddItem(stopItem);

            AddScenarioItem(animMenu, Localization.Get("Animations", "Smoke", "Smoke a Cigarette"), "WORLD_HUMAN_SMOKING");
            AddScenarioItem(animMenu, Localization.Get("Animations", "DrinkBeer", "Drink Beer"), "WORLD_HUMAN_DRINKING");
            AddScenarioItem(animMenu, Localization.Get("Animations", "Party", "Party / Dance"), "WORLD_HUMAN_PARTYING");
            AddScenarioItem(animMenu, Localization.Get("Animations", "Sit", "Sit on the Ground"), "WORLD_HUMAN_PICNIC");
            AddScenarioItem(animMenu, Localization.Get("Animations", "Pushups", "Do Push-ups"), "WORLD_HUMAN_PUSH_UPS");
            AddScenarioItem(animMenu, "Do Yoga", "WORLD_HUMAN_YOGA");
            AddScenarioItem(animMenu, "Play Guitar", "WORLD_HUMAN_MUSICIAN");
            AddScenarioItem(animMenu, "Celebrate / Cheer", "WORLD_HUMAN_CHEERING");
            AddScenarioItem(animMenu, "Flex Muscles", "WORLD_HUMAN_MUSCLE_FLEX");
            AddScenarioItem(animMenu, "Binoculars", "WORLD_HUMAN_BINOCULARS");
            AddScenarioItem(animMenu, "Sit in a Chair", "PROP_HUMAN_SEAT_CHAIR");
            AddScenarioItem(animMenu, "Guard Stance", "WORLD_HUMAN_GUARD_STAND");
        }

        private void AddScenarioItem(UIMenu menu, string label, string scenarioName)
        {
            var item = new UIMenuItem(label, "Perform scenario " + label);
            item.Activated += (sender, selected) =>
            {
                Ped p = Game.Player.Character;
                if (p != null && p.Exists())
                {
                    p.Task.ClearAllImmediately();
                    Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, p.Handle, scenarioName, 0, true);
                    UI.Notify("~g~Executando: ~w~" + label);
                }
            };
            menu.AddItem(item);
        }

        public void OnTick()
        {
        }
    }
}

namespace AvernesTreiner.Modules
{
    public class BodyguardModule : IAvernesModule
    {
        public string Name { get { return "Bodyguards"; } }

        private List<Ped> _bodyguards = new List<Ped>();
        private bool _godModeSquad = false;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var bgMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuBodyguards", "Bodyguards & Gang"));

            // 1. Single Bodyguards
            var singleMenu = menuPool.AddSubMenu(bgMenu, "Spawn Individual Bodyguard");
            AddSingleBgItem(singleMenu, "SWAT Officer", "s_m_y_swat_01", WeaponHash.CarbineRifle);
            AddSingleBgItem(singleMenu, "Military Soldier", "s_m_y_marine_01", WeaponHash.CombatMG);
            AddSingleBgItem(singleMenu, "Security Agent", "s_m_m_security_01", WeaponHash.SMG);
            AddSingleBgItem(singleMenu, "Ballas Member", "g_m_y_ballaorig_01", WeaponHash.MicroSMG);
            AddSingleBgItem(singleMenu, "Families Member (Grove Street)", "g_m_y_famdnf_01", WeaponHash.PumpShotgun);
            AddSingleBgItem(singleMenu, "Vagos Member", "g_m_y_salvagoon_01", WeaponHash.CombatPistol);

            // 2. Squads
            var squadMenu = menuPool.AddSubMenu(bgMenu, "Spawn Full Gang Squad");
            AddSquadItem(squadMenu, "Families Squad (Grove Street - 3 Members)", "g_m_y_famdnf_01", WeaponHash.CarbineRifle, 3);
            AddSquadItem(squadMenu, "Ballas Squad (3 Members)", "g_m_y_ballaorig_01", WeaponHash.MicroSMG, 3);
            AddSquadItem(squadMenu, "Los Vagos Squad (3 Members)", "g_m_y_salvagoon_01", WeaponHash.PumpShotgun, 3);
            AddSquadItem(squadMenu, "SWAT Tactical Squad (4 Members)", "s_m_y_swat_01", WeaponHash.CarbineRifle, 4);
            AddSquadItem(squadMenu, "Elite Military Squad (4 Members)", "s_m_y_marine_01", WeaponHash.CombatMG, 4);

            // 3. Gang Armory
            var armoryMenu = menuPool.AddSubMenu(bgMenu, "Gang Armory & Equipment");
            AddWeaponEquipItem(armoryMenu, "Equip Assault Rifles (Carbine Rifle)", WeaponHash.CarbineRifle);
            AddWeaponEquipItem(armoryMenu, "Equip Heavy Machine Guns (Combat MG)", WeaponHash.CombatMG);
            AddWeaponEquipItem(armoryMenu, "Equip Combat Shotguns", WeaponHash.PumpShotgun);
            AddWeaponEquipItem(armoryMenu, "Equip Submachine Guns (SMG)", WeaponHash.SMG);
            AddWeaponEquipItem(armoryMenu, "Equip Rocket Launcher (RPG)", WeaponHash.RPG);
            AddWeaponEquipItem(armoryMenu, "Equip Minigun", WeaponHash.Minigun);

            var armorAllItem = new UIMenuItem("Full Gang Armor (100% Armor)", "Restores all members' health and armor");
            armorAllItem.Activated += (sender, selected) =>
            {
                int count = 0;
                foreach (var bg in _bodyguards)
                {
                    if (bg != null && bg.Exists())
                    {
                        bg.Health = bg.MaxHealth;
                        bg.Armor = 100;
                        count++;
                    }
                }
                UI.Notify("~g~" + count + " members received full armor!");
            };
            armoryMenu.AddItem(armorAllItem);

            var godSquadItem = new UIMenuCheckboxItem("Invincible Gang (God Mode)", _godModeSquad, "Makes all members immune to bullets and explosions");
            godSquadItem.CheckboxEvent += (sender, state) =>
            {
                _godModeSquad = state;
                foreach (var bg in _bodyguards)
                {
                    if (bg != null && bg.Exists())
                    {
                        bg.IsInvincible = _godModeSquad;
                    }
                }
                UI.Notify(_godModeSquad ? "~g~The gang is now invincible!" : "~y~Gang invincibility disabled.");
            };
            armoryMenu.AddItem(godSquadItem);

            // 4. Tactical Commands
            var cmdMenu = menuPool.AddSubMenu(bgMenu, "Group Orders & Tactics");

            var attackTargetItem = new UIMenuItem("Everyone Attack Aimed Target", "Orders the gang to attack the enemy you aim at");
            attackTargetItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                Entity target = null;
                RaycastResult ray = World.Raycast(GameplayCamera.Position, GameplayCamera.Direction, 150.0f, IntersectOptions.Peds1);
                if (ray.DitHitEntity && ray.HitEntity is Ped) target = ray.HitEntity;

                if (target != null && target is Ped)
                {
                    foreach (var bg in _bodyguards)
                    {
                        if (bg != null && bg.Exists())
                        {
                            bg.Task.FightAgainst((Ped)target);
                        }
                    }
                    UI.Notify("~r~Gang attacking marked target!");
                }
                else
                {
                    UI.Notify("~y~Aim at an NPC first!");
                }
            };
            cmdMenu.AddItem(attackTargetItem);

            var holdItem = new UIMenuItem("Hold Position (Stay Put)", "Members will defend the current area");
            holdItem.Activated += (sender, selected) =>
            {
                foreach (var bg in _bodyguards)
                {
                    if (bg != null && bg.Exists())
                    {
                        bg.Task.StandStill(-1);
                    }
                }
                UI.Notify("~y~Members holding position.");
            };
            cmdMenu.AddItem(holdItem);

            var followItem = new UIMenuItem("Regroup and Follow Player", "Cancels previous orders and resumes following");
            followItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                foreach (var bg in _bodyguards)
                {
                    if (bg != null && bg.Exists())
                    {
                        bg.Task.ClearAll();
                        Function.Call(Hash.SET_PED_AS_GROUP_MEMBER, bg.Handle, player.CurrentPedGroup.Handle);
                    }
                }
                UI.Notify("~g~Gang regrouped with the player.");
            };
            cmdMenu.AddItem(followItem);

            // 5. Gang War Simulator
            var warItem = new UIMenuItem("Start Gang War (Combat Simulation)", "Spawns Ballas vs Families in a local shootout");
            warItem.Activated += (sender, selected) =>
            {
                StartGangWar();
            };
            bgMenu.AddItem(warItem);

            // Dismiss All
            var dismissItem = new UIMenuItem(
                Localization.Get("Bodyguards", "DismissAll", "Dismiss All Bodyguards"),
                Localization.Get("Bodyguards", "DismissAllDesc", "Removes all bodyguards from the group")
            );
            dismissItem.Activated += (sender, selected) =>
            {
                DismissAllBodyguards();
            };
            bgMenu.AddItem(dismissItem);
        }

        private void AddSingleBgItem(UIMenu menu, string label, string modelName, WeaponHash weapon)
        {
            var item = new UIMenuItem(label, "Spawns " + label);
            item.Activated += (sender, selected) =>
            {
                SpawnBodyguard(modelName, weapon, label);
            };
            menu.AddItem(item);
        }

        private void AddSquadItem(UIMenu menu, string label, string modelName, WeaponHash weapon, int count)
        {
            var item = new UIMenuItem(label, "Spawns " + count + " members at once");
            item.Activated += (sender, selected) =>
            {
                for (int i = 0; i < count; i++)
                {
                    SpawnBodyguard(modelName, weapon, label);
                }
                UI.Notify("~g~" + label + " spawned successfully!");
            };
            menu.AddItem(item);
        }

        private void AddWeaponEquipItem(UIMenu menu, string label, WeaponHash weapon)
        {
            var item = new UIMenuItem(label, "Gives the weapon to all active members");
            item.Activated += (sender, selected) =>
            {
                int count = 0;
                foreach (var bg in _bodyguards)
                {
                    if (bg != null && bg.Exists())
                    {
                        bg.Weapons.Give(weapon, 9999, true, true);
                        count++;
                    }
                }
                UI.Notify("~g~" + count + " members equipped with " + label + "!");
            };
            menu.AddItem(item);
        }

        private void SpawnBodyguard(string modelName, WeaponHash weapon, string displayName)
        {
            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists()) return;

                Model model = new Model(modelName);
                model.Request(2500);
                if (model.IsInCdImage && model.IsValid)
                {
                    float angle = _bodyguards.Count * 0.8f;
                    Vector3 offset = new Vector3((float)Math.Cos(angle) * 3.0f, (float)Math.Sin(angle) * 3.0f, 0f);
                    Vector3 spawnPos = player.Position + offset;
                    Ped bg = World.CreatePed(model, spawnPos, player.Heading);
                    if (bg != null && bg.Exists())
                    {
                        bg.Armor = 100;
                        bg.Health = 200;
                        bg.MaxHealth = 200;
                        bg.Weapons.Give(weapon, 9999, true, true);
                        bg.NeverLeavesGroup = true;
                        bg.Accuracy = 85;
                        bg.RelationshipGroup = player.RelationshipGroup;

                        player.CurrentPedGroup.Add(bg, false);
                        Function.Call(Hash.SET_PED_AS_GROUP_MEMBER, bg.Handle, player.CurrentPedGroup.Handle);
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, bg.Handle, 5, true);
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, bg.Handle, 46, true);
                        Function.Call(Hash.SET_PED_CAN_BE_TARGETTED, bg.Handle, false);

                        if (_godModeSquad) bg.IsInvincible = true;

                        _bodyguards.Add(bg);
                    }
                }
                model.MarkAsNoLongerNeeded();
            }
            catch { }
        }

        private void StartGangWar()
        {
            try
            {
                Ped player = Game.Player.Character;
                Model ballaModel = new Model("g_m_y_ballaorig_01");
                Model famModel = new Model("g_m_y_famdnf_01");
                ballaModel.Request(2000);
                famModel.Request(2000);

                int ballaRel = World.AddRelationshipGroup("BALLAS_GANG");
                int famRel = World.AddRelationshipGroup("FAMILIES_GANG");

                World.SetRelationshipBetweenGroups(Relationship.Hate, ballaRel, famRel);
                World.SetRelationshipBetweenGroups(Relationship.Hate, famRel, ballaRel);

                for (int i = 0; i < 4; i++)
                {
                    Vector3 bPos = player.Position + player.ForwardVector * 15.0f + player.RightVector * (i * 3.0f - 4.5f);
                    Ped b = World.CreatePed(ballaModel, bPos);
                    if (b != null)
                    {
                        b.RelationshipGroup = ballaRel;
                        b.Weapons.Give(WeaponHash.MicroSMG, 9999, true, true);
                        b.Armor = 50;
                    }

                    Vector3 fPos = player.Position + player.ForwardVector * 35.0f + player.RightVector * (i * 3.0f - 4.5f);
                    Ped f = World.CreatePed(famModel, fPos);
                    if (f != null)
                    {
                        f.RelationshipGroup = famRel;
                        f.Weapons.Give(WeaponHash.PumpShotgun, 9999, true, true);
                        f.Armor = 50;
                    }
                }

                UI.Notify("~r~Gang War started! Ballas vs Families!");
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Gang War error: " + ex.Message);
            }
        }

        private void DismissAllBodyguards()
        {
            try
            {
                int count = 0;
                for (int i = _bodyguards.Count - 1; i >= 0; i--)
                {
                    Ped bg = _bodyguards[i];
                    if (bg != null && bg.Exists())
                    {
                        bg.LeaveGroup();
                        bg.Delete();
                        count++;
                    }
                }
                _bodyguards.Clear();
                UI.Notify("~y~" + count + " members dismissed.");
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Error: " + ex.Message);
            }
        }

        public void OnTick()
        {
            for (int i = _bodyguards.Count - 1; i >= 0; i--)
            {
                if (_bodyguards[i] == null || !_bodyguards[i].Exists() || _bodyguards[i].IsDead)
                {
                    _bodyguards.RemoveAt(i);
                }
            }
        }
    }
}

namespace AvernesTreiner.Modules
{
    public class MiscPowersModule : IAvernesModule
    {
        public string Name { get { return "Powers"; } }

        private bool _noClip = false;
        private bool _nightVision = false;
        private bool _thermalVision = false;
        private bool _fallProtectionActive = false;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var powerMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuPowers", "Superpowers & Vision"));

            // NoClip
            var noClipItem = new UIMenuCheckboxItem(Localization.Get("Powers", "NoClip", "Flight Mode (No-Clip)"), _noClip, Localization.Get("Powers", "NoClipDesc", "Fly freely and pass through walls"));
            noClipItem.CheckboxEvent += (sender, state) =>
            {
                _noClip = state;
                Ped player = Game.Player.Character;
                if (player != null && player.Exists())
                {
                    player.HasCollision = !_noClip;
                    Function.Call(Hash.SET_ENTITY_COLLISION, player.Handle, !_noClip, true);
                    player.FreezePosition = _noClip;

                    if (_noClip)
                    {
                        player.IsInvincible = true;
                        _fallProtectionActive = false;
                    }
                    else
                    {
                        // Fall protection: protect player until they safely touch ground
                        _fallProtectionActive = true;
                        player.CanRagdoll = false;
                        Function.Call(Hash.SET_PED_CAN_RAGDOLL, player.Handle, false);
                        Function.Call(Hash.SET_ENTITY_PROOFS, player.Handle, true, true, true, true, true, true, true, true);
                        player.IsInvincible = true;
                    }
                }
            };
            powerMenu.AddItem(noClipItem);

            // Night Vision
            var nightItem = new UIMenuCheckboxItem(Localization.Get("Powers", "NightVision", "Night Vision"), _nightVision, Localization.Get("Powers", "NightVisionDesc", "Military green filter"));
            nightItem.CheckboxEvent += (sender, state) =>
            {
                _nightVision = state;
                Function.Call(Hash.SET_NIGHTVISION, _nightVision);
            };
            powerMenu.AddItem(nightItem);

            // Thermal Vision
            var thermItem = new UIMenuCheckboxItem(Localization.Get("Powers", "ThermalVision", "Thermal Vision"), _thermalVision, Localization.Get("Powers", "ThermalVisionDesc", "Infrared heat filter"));
            thermItem.CheckboxEvent += (sender, state) =>
            {
                _thermalVision = state;
                Function.Call(Hash.SET_SEETHROUGH, _thermalVision);
            };
            powerMenu.AddItem(thermItem);
        }

        public void OnTick()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            if (_noClip)
            {
                player.HasCollision = false;
                Function.Call(Hash.SET_ENTITY_COLLISION, player.Handle, false, true);
                player.FreezePosition = true;

                float speed = 0.8f;
                if (Game.IsKeyPressed(Keys.ShiftKey)) speed = 2.5f;

                Vector3 camDir = GameplayCamera.Direction;
                Vector3 rightDir = Vector3.Cross(camDir, new Vector3(0, 0, 1));
                Vector3 newPos = player.Position;

                if (Game.IsKeyPressed(Keys.W)) newPos += camDir * speed;
                if (Game.IsKeyPressed(Keys.S)) newPos -= camDir * speed;
                if (Game.IsKeyPressed(Keys.D)) newPos += rightDir * speed;
                if (Game.IsKeyPressed(Keys.A)) newPos -= rightDir * speed;
                if (Game.IsKeyPressed(Keys.Space)) newPos.Z += speed;
                if (Game.IsKeyPressed(Keys.ControlKey)) newPos.Z -= speed;

                player.Position = newPos;
                player.Heading = GameplayCamera.Rotation.Z;
            }
            else if (_fallProtectionActive)
            {
                // Deactivate fall protection only when safely on ground or in vehicle/water
                if (!player.IsInAir || player.HeightAboveGround < 1.5f || player.IsInWater || player.IsInVehicle())
                {
                    _fallProtectionActive = false;
                    player.CanRagdoll = true;
                    Function.Call(Hash.SET_PED_CAN_RAGDOLL, player.Handle, true);
                    Function.Call(Hash.SET_ENTITY_PROOFS, player.Handle, false, false, false, false, false, false, false, false);
                    if (!PlayerModule.IsGodModeActive)
                    {
                        player.IsInvincible = false;
                    }
                }
            }
        }
    }
}

namespace AvernesTreiner.Modules
{
    public class PlayerModule : IAvernesModule
    {
        public string Name { get { return "Player"; } }

        private static bool _isGodModeActive = false;
        public static bool IsGodModeActive { get { return _isGodModeActive; } }
        private bool _neverWanted = false;
        private bool _copsIgnore = false;
        private bool _superJump = false;
        private bool _fastRun = false;
        private bool _fastSwim = false;
        private bool _noRagdoll = false;
        private bool _unlimitedStamina = false;
        private bool _unlimitedOxygen = false;
        private bool _isInvisible = false;
        private bool _mobileRadio = false;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var playerMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuPlayer", "Player Options"));

            // God Mode
            var godItem = new UIMenuCheckboxItem(Localization.Get("Player", "GodMode", "God Mode (Invincibility)"), _isGodModeActive, Localization.Get("Player", "GodModeDesc", "Immune to all damage"));
            godItem.CheckboxEvent += (sender, state) =>
            {
                _isGodModeActive = state;
                if (Game.Player.Character.Exists())
                {
                    Game.Player.Character.IsInvincible = _isGodModeActive;
                }
            };
            playerMenu.AddItem(godItem);

            // Never Wanted
            var neverWantedItem = new UIMenuCheckboxItem(Localization.Get("Player", "NeverWanted", "Never Wanted"), _neverWanted, Localization.Get("Player", "NeverWantedDesc", "Police will not pursue you"));
            neverWantedItem.CheckboxEvent += (sender, state) =>
            {
                _neverWanted = state;
                if (_neverWanted) Game.Player.WantedLevel = 0;
            };
            playerMenu.AddItem(neverWantedItem);

            // Cops Ignore Player
            var copsIgnoreItem = new UIMenuCheckboxItem(Localization.Get("Player", "CopsIgnore", "Cops Ignore Player"), _copsIgnore, Localization.Get("Player", "CopsIgnoreDesc", "Police ignore your crimes"));
            copsIgnoreItem.CheckboxEvent += (sender, state) =>
            {
                _copsIgnore = state;
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, _copsIgnore);
            };
            playerMenu.AddItem(copsIgnoreItem);

            // Clear Wanted Level
            var clearWantedItem = new UIMenuItem(Localization.Get("Player", "ClearWanted", "Clear Wanted Level"), Localization.Get("Player", "ClearWantedDesc", "Instantly removes wanted stars"));
            clearWantedItem.Activated += (sender, selected) =>
            {
                Game.Player.WantedLevel = 0;
                UI.Notify("~g~Wanted level cleared!");
            };
            playerMenu.AddItem(clearWantedItem);

            // Wanted Level List
            var wantedList = new List<dynamic> { 0, 1, 2, 3, 4, 5 };
            var wantedItem = new UIMenuListItem(Localization.Get("Player", "WantedLevel", "Wanted Level"), wantedList, Game.Player.WantedLevel, Localization.Get("Player", "WantedLevelDesc", "Set wanted stars"));
            wantedItem.OnListChanged += (sender, index) =>
            {
                Game.Player.WantedLevel = (int)wantedList[index];
            };
            playerMenu.AddItem(wantedItem);

            // Heal Player
            var healItem = new UIMenuItem(Localization.Get("Player", "HealPlayer", "Restore Health & Armor"), Localization.Get("Player", "HealPlayerDesc", "Restores health and armor"));
            healItem.Activated += (sender, selected) =>
            {
                Ped p = Game.Player.Character;
                if (p.Exists())
                {
                    p.Health = p.MaxHealth;
                    p.Armor = 100;
                    UI.Notify("~g~Health and armor restored!");
                }
            };
            playerMenu.AddItem(healItem);

            // Mobile Radio (Listen on foot)
            var radioItem = new UIMenuCheckboxItem("Mobile Radio on Foot", _mobileRadio, "Allows listening to GTA V radio while walking, running, or swimming");
            radioItem.CheckboxEvent += (sender, state) =>
            {
                _mobileRadio = state;
                Function.Call(Hash.SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY, _mobileRadio);
                Function.Call(Hash.SET_USER_RADIO_CONTROL_ENABLED, _mobileRadio);
                UI.Notify(_mobileRadio ? "~g~Mobile radio enabled!" : "~y~Mobile radio disabled.");
            };
            playerMenu.AddItem(radioItem);

            // Invisibility
            var invisItem = new UIMenuCheckboxItem(Localization.Get("Player", "Invisibility", "Invisibility"), _isInvisible, Localization.Get("Player", "InvisibilityDesc", "Become invisible"));
            invisItem.CheckboxEvent += (sender, state) =>
            {
                _isInvisible = state;
                if (Game.Player.Character.Exists())
                {
                    Game.Player.Character.IsVisible = !_isInvisible;
                }
            };
            playerMenu.AddItem(invisItem);

            // Super Jump
            var jumpItem = new UIMenuCheckboxItem(Localization.Get("Player", "SuperJump", "Super Jump"), _superJump, Localization.Get("Player", "SuperJumpDesc", "Super-sized jumps"));
            jumpItem.CheckboxEvent += (sender, state) => { _superJump = state; };
            playerMenu.AddItem(jumpItem);

            // Fast Run
            var runItem = new UIMenuCheckboxItem(Localization.Get("Player", "FastRun", "Super Speed"), _fastRun, Localization.Get("Player", "FastRunDesc", "Fast running"));
            runItem.CheckboxEvent += (sender, state) => { _fastRun = state; };
            playerMenu.AddItem(runItem);

            // Fast Swim
            var swimItem = new UIMenuCheckboxItem(Localization.Get("Player", "FastSwim", "Fast Swim"), _fastSwim, Localization.Get("Player", "FastSwimDesc", "Fast swimming"));
            swimItem.CheckboxEvent += (sender, state) => { _fastSwim = state; };
            playerMenu.AddItem(swimItem);

            // No Ragdoll
            var ragItem = new UIMenuCheckboxItem(Localization.Get("Player", "NoRagdoll", "No Ragdoll"), _noRagdoll, Localization.Get("Player", "NoRagdollDesc", "Never fall to the ground"));
            ragItem.CheckboxEvent += (sender, state) =>
            {
                _noRagdoll = state;
                if (Game.Player.Character.Exists())
                {
                    Game.Player.Character.CanRagdoll = !_noRagdoll;
                }
            };
            playerMenu.AddItem(ragItem);

            // Unlimited Stamina
            var stamItem = new UIMenuCheckboxItem(Localization.Get("Player", "UnlimitedStamina", "Infinite Stamina"), _unlimitedStamina, Localization.Get("Player", "UnlimitedStaminaDesc", "Never get tired"));
            stamItem.CheckboxEvent += (sender, state) => { _unlimitedStamina = state; };
            playerMenu.AddItem(stamItem);

            // Unlimited Oxygen
            var oxyItem = new UIMenuCheckboxItem(Localization.Get("Player", "UnlimitedOxygen", "Infinite Oxygen"), _unlimitedOxygen, Localization.Get("Player", "UnlimitedOxygenDesc", "Unlimited breathing"));
            oxyItem.CheckboxEvent += (sender, state) => { _unlimitedOxygen = state; };
            playerMenu.AddItem(oxyItem);

            // Money Submenu
            var moneyMenu = menuPool.AddSubMenu(playerMenu, "Money Generator");
            AddCashOption(moneyMenu, "Add +$100,000", 100000);
            AddCashOption(moneyMenu, "Add +$1,000,000 (1 Million)", 1000000);
            AddCashOption(moneyMenu, "Add +$10,000,000 (10 Million)", 10000000);
            AddCashOption(moneyMenu, "Add +$100,000,000 (100 Million)", 100000000);

            var maxCashItem = new UIMenuItem("Maximum Money ($2,147,483,647)", "Sets your bank account to the GTA V maximum amount");
            maxCashItem.Activated += (sender, selected) =>
            {
                Game.Player.Money = 2147483647;
                UI.Notify("~g~Maximum money applied! ($2,147,483,647)");
            };
            moneyMenu.AddItem(maxCashItem);

            // Clean Clothes
            var cleanItem = new UIMenuItem(Localization.Get("Player", "CleanClothes", "Clean Clothes & Blood"), Localization.Get("Player", "CleanClothesDesc", "Cleans your character"));
            cleanItem.Activated += (sender, selected) =>
            {
                if (Game.Player.Character.Exists())
                {
                    Game.Player.Character.ClearBloodDamage();
                    UI.Notify("~b~Clothes cleaned!");
                }
            };
            playerMenu.AddItem(cleanItem);

            // Suicide
            var suicideItem = new UIMenuItem(Localization.Get("Player", "Suicide", "Suicide"), Localization.Get("Player", "SuicideDesc", "Kills your character"));
            suicideItem.Activated += (sender, selected) =>
            {
                if (Game.Player.Character.Exists())
                {
                    Game.Player.Character.Kill();
                }
            };
            playerMenu.AddItem(suicideItem);
        }

        private void AddCashOption(UIMenu menu, string label, int amount)
        {
            var item = new UIMenuItem(label, "Deposited " + label + " immediately");
            item.Activated += (sender, selected) =>
            {
                Game.Player.Money += amount;
                UI.Notify("~g~+" + amount.ToString("C0") + " added successfully!");
            };
            menu.AddItem(item);
        }

        public void OnTick()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            if (IsGodModeActive)
            {
                player.IsInvincible = true;
            }
            if (_neverWanted)
            {
                if (Game.Player.WantedLevel > 0)
                {
                    Game.Player.WantedLevel = 0;
                }
            }
            if (_copsIgnore)
            {
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, true);
            }
            if (_superJump)
            {
                Game.Player.SetSuperJumpThisFrame();
            }
            if (_fastRun)
            {
                Game.Player.SetRunSpeedMultThisFrame(1.49f);
            }
            if (_fastSwim)
            {
                Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, Game.Player, 1.49f);
            }
            if (_noRagdoll)
            {
                player.CanRagdoll = false;
            }
            if (_unlimitedStamina)
            {
                Function.Call(Hash.RESET_PLAYER_STAMINA, Game.Player);
            }
            if (_unlimitedOxygen)
            {
                Function.Call(Hash.SET_PED_MAX_TIME_UNDERWATER, player, 9999.0f);
            }
            if (_mobileRadio)
            {
                Function.Call(Hash.SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY, true);
            }
        }
    }
}

namespace AvernesTreiner.Modules
{
    public class SettingsModule : IAvernesModule
    {
        public string Name { get { return "Settings"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var settingsMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuSettings", "Settings"));
// Speedometer Unit selector
            var unitList = new List<dynamic> { "KM/H (Metric)", "MPH (Imperial)" };
            int currentUnitIdx = string.Equals(ConfigManager.SpeedUnit, "MPH", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var unitItem = new UIMenuListItem(Localization.Get("Settings", "SpeedUnit", "Speed Unit"), unitList, currentUnitIdx, "Switch between KM/H and MPH");
            unitItem.OnListChanged += (sender, index) =>
            {
                ConfigManager.SpeedUnit = (index == 1) ? "MPH" : "KM/H";
                ConfigManager.Save();
                UI.Notify("~g~Speedometer: " + ConfigManager.SpeedUnit);
            };
            settingsMenu.AddItem(unitItem);

            // Reload addons.json
            var reloadAddonsItem = new UIMenuItem(Localization.Get("Settings", "ReloadAddons", "Reload addons.json"), "Refreshes the vehicle list from scripts/AvernesTreiner/addons.json");
            reloadAddonsItem.Activated += (sender, selected) =>
            {
                AddonManager.Load();
                UI.Notify("~g~addons.json reloaded! " + AddonManager.Addons.Count + " vehicles found.");
            };
            settingsMenu.AddItem(reloadAddonsItem);

            // Version info
            var verItem = new UIMenuItem(Localization.Get("Settings", "VersionLabel", "Version: v1.0.0"), "Official AvernesTreiner version");
            settingsMenu.AddItem(verItem);

            // Creator info
            var authorItem = new UIMenuItem(Localization.Get("Settings", "CreatorLabel", "Created by Avernes"), "Official AvernesTreiner");
            settingsMenu.AddItem(authorItem);
        }

        public void OnTick() { }
    }
}

namespace AvernesTreiner.Modules
{
    public class SkinChangerModule : IAvernesModule
    {
        public string Name { get { return "SkinChanger"; } }

        private int _originalModelHash = 0;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var skinMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuSkin", "Character Changer (Skins)"));

            if (Game.Player.Character != null && Game.Player.Character.Exists())
            {
                _originalModelHash = Game.Player.Character.Model.Hash;
            }

            var restoreItem = new UIMenuItem(
                Localization.Get("Skins", "RestoreOriginal", "Restore Original Character"),
                Localization.Get("Skins", "RestoreOriginalDesc", "Returns to the original story character")
            );
            restoreItem.Activated += (sender, selected) =>
            {
                if (_originalModelHash != 0)
                {
                    ChangeSkinByHash(_originalModelHash, "Original");
                }
                else
                {
                    ChangeSkin("player_zero", "Michael");
                }
            };
            skinMenu.AddItem(restoreItem);

            var mainCharsMenu = menuPool.AddSubMenu(skinMenu, Localization.Get("Skins", "MainCharacters", "Main Characters"));
            AddSkinOption(mainCharsMenu, "Michael De Santa", "player_zero");
            AddSkinOption(mainCharsMenu, "Franklin Clinton", "player_one");
            AddSkinOption(mainCharsMenu, "Trevor Philips", "player_two");
            AddSkinOption(mainCharsMenu, "Lamar Davis", "ig_lamardavis");
            AddSkinOption(mainCharsMenu, "Lester Crest", "ig_lestercrest");
            AddSkinOption(mainCharsMenu, "Amanda De Santa", "ig_amanda_townley");
            AddSkinOption(mainCharsMenu, "Jimmy De Santa", "ig_jimmydisanto");
            AddSkinOption(mainCharsMenu, "Tracey De Santa", "ig_tracie");
            AddSkinOption(mainCharsMenu, "Dave Norton", "ig_davenorton");
            AddSkinOption(mainCharsMenu, "Solomon Richards", "ig_solomon");

            var authMenu = menuPool.AddSubMenu(skinMenu, Localization.Get("Skins", "Authorities", "Authorities & Military"));
            AddSkinOption(authMenu, "SWAT Officer", "s_m_y_swat_01");
            AddSkinOption(authMenu, "Marine / Army", "s_m_y_marine_01");
            AddSkinOption(authMenu, "Air Force Pilot", "s_m_m_pilot_02");
            AddSkinOption(authMenu, "Agente FIB", "s_m_m_fbi_01");
            AddSkinOption(authMenu, "Paramedic", "s_m_m_paramedic_01");
            AddSkinOption(authMenu, "Bombeiro", "s_m_y_fireman_01");
            AddSkinOption(authMenu, "Private Security", "s_m_m_security_01");
            AddSkinOption(authMenu, "Guarda Prisional", "s_m_m_prisguard_01");

            var animalsMenu = menuPool.AddSubMenu(skinMenu, Localization.Get("Skins", "Animals", "Animals"));
            AddSkinOption(animalsMenu, "Chop (Rottweiler)", "a_c_chop");
            AddSkinOption(animalsMenu, "Gato", "a_c_cat_01");
            AddSkinOption(animalsMenu, "Chimpanzee", "a_c_chimp");
            AddSkinOption(animalsMenu, "Gaivota", "a_c_seagull");
            AddSkinOption(animalsMenu, "Pombo", "a_c_pigeon");
            AddSkinOption(animalsMenu, "Corvo", "a_c_crow");
            AddSkinOption(animalsMenu, "Galinha", "a_c_hen");
            AddSkinOption(animalsMenu, "Veado", "a_c_deer");
            AddSkinOption(animalsMenu, "Husky", "a_c_husky");
            AddSkinOption(animalsMenu, "Golden Retriever", "a_c_retriever");
            AddSkinOption(animalsMenu, "German Shepherd", "a_c_shepherd");
            AddSkinOption(animalsMenu, "Poodle", "a_c_poodle");
            AddSkinOption(animalsMenu, "Pug", "a_c_pug");
            AddSkinOption(animalsMenu, "Coiote", "a_c_coyote");
            AddSkinOption(animalsMenu, "Javali", "a_c_boar");
            AddSkinOption(animalsMenu, "Coelho", "a_c_rabbit_01");
            AddSkinOption(animalsMenu, "Tiger Shark (Immune out of water)", "a_c_sharktiger");
            AddSkinOption(animalsMenu, "Dolphin (Immune out of water)", "a_c_dolphin");
            AddSkinOption(animalsMenu, "Orca / Whale (Immune out of water)", "a_c_killerwhale");
        }

        private void AddSkinOption(UIMenu parent, string displayName, string modelName)
        {
            var item = new UIMenuItem(displayName, "Transform into " + displayName);
            item.Activated += (sender, selected) =>
            {
                ChangeSkin(modelName, displayName);
            };
            parent.AddItem(item);
        }

        private void ChangeSkin(string modelName, string displayName)
        {
            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists()) return;

                // Safety check: Cannot change skin while dead!
                if (player.IsDead || player.Health <= 0)
                {
                    UI.Notify("~r~You cannot change your skin while dead!");
                    return;
                }

                Model model = new Model(modelName);
                model.Request(2500);
                if (model.IsInCdImage && model.IsValid)
                {
                    // Heal before changing
                    player.Health = player.MaxHealth;
                    player.ClearBloodDamage();

                    Function.Call(Hash.SET_PLAYER_MODEL, Game.Player.Handle, model.Hash);
                    Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, Game.Player.Character.Handle);

                    // Aquatic animals protection
                    bool isAquatic = modelName.Contains("shark") || modelName.Contains("dolphin") || modelName.Contains("killerwhale");
                    if (isAquatic)
                    {
                        Ped newPed = Game.Player.Character;
                        Function.Call(Hash.SET_PED_DIES_IN_WATER, newPed.Handle, false);
                        Function.Call(Hash.SET_PED_DIES_INSTANTLY_IN_WATER, newPed.Handle, false);
                        Function.Call(Hash.SET_PED_MAX_TIME_UNDERWATER, newPed.Handle, 999999.0f);
                        newPed.IsInvincible = true;
                        UI.Notify("~b~Aquatic mode: ~w~" + displayName + " is immune to suffocation on land!");
                    }
                    else
                    {
                        UI.Notify("~g~Skin changed to ~w~" + displayName);
                    }
                }
                else
                {
                    UI.Notify("~r~Failed to load model: " + modelName);
                }
                model.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Error changing skin: " + ex.Message);
            }
        }

        private void ChangeSkinByHash(int modelHash, string displayName)
        {
            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists()) return;

                if (player.IsDead || player.Health <= 0)
                {
                    UI.Notify("~r~You cannot change your skin while dead!");
                    return;
                }

                Model model = new Model(modelHash);
                model.Request(2500);
                if (model.IsInCdImage && model.IsValid)
                {
                    player.Health = player.MaxHealth;
                    player.ClearBloodDamage();
                    Function.Call(Hash.SET_PLAYER_MODEL, Game.Player.Handle, model.Hash);
                    Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, Game.Player.Character.Handle);
                    UI.Notify("~g~Skin changed to ~w~" + displayName);
                }
                else
                {
                    UI.Notify("~r~Failed to restore original model.");
                }
                model.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Error: " + ex.Message);
            }
        }

        public void OnTick() { }
    }
}

namespace AvernesTreiner.Modules
{
    public class TeleportModule : IAvernesModule
    {
        public string Name { get { return "Teleport"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var teleMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuTeleport", "Teleport"));

            // Waypoint
            var wpItem = new UIMenuItem(Localization.Get("Teleport", "TeleportWaypoint", "Teleport to Waypoint"), Localization.Get("Teleport", "TeleportWaypointDesc", "Teleports to the marked map location"));
            wpItem.Activated += (sender, selected) =>
            {
                Vector3 wp = World.GetWaypointPosition();
                if (wp != Vector3.Zero)
                {
                    float gz = World.GetGroundHeight(new Vector2(wp.X, wp.Y));
                    if (gz <= 0.0f) gz = wp.Z;
                    if (gz <= 0.0f) gz = 40.0f;
                    Vector3 dest = new Vector3(wp.X, wp.Y, gz + 1.2f);
                    Teleport(dest);
                    UI.Notify("~g~Teleported to Waypoint!");
                }
                else
                {
                    UI.Notify("~r~Set a waypoint on the map first!");
                }
            };
            teleMenu.AddItem(wpItem);

            // Forward Teleport
            var fwdItem = new UIMenuItem(Localization.Get("Teleport", "ForwardTeleport", "Teleport 3m Forward"), Localization.Get("Teleport", "ForwardTeleportDesc", "Moves forward through doors"));
            fwdItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                Teleport(player.Position + player.ForwardVector * 3.0f);
            };
            teleMenu.AddItem(fwdItem);

            // Famous Locations
            AddLocation(teleMenu, Localization.Get("Teleport", "MazeBank", "Maze Bank Summit"), new Vector3(-75.0f, -818.0f, 326.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "VinewoodSign", "Vinewood Sign"), new Vector3(711.0f, 1198.0f, 348.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "MountChiliad", "Mount Chiliad Summit"), new Vector3(501.0f, 5604.0f, 797.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "Airport", "Los Santos International Airport"), new Vector3(-1037.0f, -2737.0f, 20.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "DelPerroPier", "Del Perro Pier"), new Vector3(-1600.0f, -1041.0f, 13.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "FortZancudo", "Fort Zancudo Military Base"), new Vector3(-2047.0f, 3132.0f, 33.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "Prison", "Bolingbroke Penitentiary"), new Vector3(1846.0f, 2585.0f, 46.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "SandyShoresAirfield", "Sandy Shores Airfield"), new Vector3(1734.0f, 3710.0f, 34.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "HumaneLabs", "Humane Labs"), new Vector3(3615.0f, 3740.0f, 28.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "GalileoObservatory", "Galileo Observatory"), new Vector3(-438.0f, 1076.0f, 328.0f));
        }

        private void AddLocation(UIMenu menu, string label, Vector3 coords)
        {
            var item = new UIMenuItem(label, "Teleport to " + label);
            item.Activated += (sender, selected) =>
            {
                Teleport(coords);
                UI.Notify("~b~Teleported: " + label);
            };
            menu.AddItem(item);
        }

        private void Teleport(Vector3 pos)
        {
            Ped player = Game.Player.Character;
            if (player.IsInVehicle())
            {
                player.CurrentVehicle.Position = pos;
            }
            else
            {
                player.Position = pos;
            }
        }

        public void OnTick() { }
    }
}

namespace AvernesTreiner.Modules
{
    public class VehicleCustomizationModule : IAvernesModule
    {
        public string Name { get { return "Customization"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var customMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuCustomization", "Workshop & Customization (LSC)"));

            // 1. Performance
            var perfMenu = menuPool.AddSubMenu(customMenu, Localization.Get("Customization", "PerformanceMenu", "Performance & Engine"));
            AddModListItem(perfMenu, Localization.Get("Customization", "Engine", "Engine Level"), VehicleMod.Engine);
            AddModListItem(perfMenu, Localization.Get("Customization", "Brakes", "Brakes"), VehicleMod.Brakes);
            AddModListItem(perfMenu, Localization.Get("Customization", "Transmission", "Transmission"), VehicleMod.Transmission);
            AddModListItem(perfMenu, Localization.Get("Customization", "Suspension", "Suspension"), VehicleMod.Suspension);
            AddModListItem(perfMenu, Localization.Get("Customization", "Armor", "Armor"), VehicleMod.Armor);
            AddToggleModItem(perfMenu, Localization.Get("Customization", "Turbo", "Turbo Tuning"), VehicleToggleMod.Turbo);
            AddToggleModItem(perfMenu, Localization.Get("Customization", "XenonHeadlights", "Xenon Headlights"), VehicleToggleMod.XenonHeadlights);

            // 2. Bodywork
            var bodyMenu = menuPool.AddSubMenu(customMenu, Localization.Get("Customization", "BodyworkMenu", "Bodywork & Visual Parts"));
            AddModListItem(bodyMenu, Localization.Get("Customization", "Spoilers", "Spoiler"), VehicleMod.Spoilers);
            AddModListItem(bodyMenu, Localization.Get("Customization", "FrontBumper", "Front Bumper"), VehicleMod.FrontBumper);
            AddModListItem(bodyMenu, Localization.Get("Customization", "RearBumper", "Rear Bumper"), VehicleMod.RearBumper);
            AddModListItem(bodyMenu, Localization.Get("Customization", "SideSkirt", "Side Skirts"), VehicleMod.SideSkirt);
            AddModListItem(bodyMenu, Localization.Get("Customization", "Exhaust", "Exhaust"), VehicleMod.Exhaust);
            AddModListItem(bodyMenu, Localization.Get("Customization", "Grille", "Front Grille"), VehicleMod.Grille);
            AddModListItem(bodyMenu, Localization.Get("Customization", "Hood", "Hood"), VehicleMod.Hood);
            AddModListItem(bodyMenu, Localization.Get("Customization", "Fender", "Fender"), VehicleMod.Fender);
            AddModListItem(bodyMenu, Localization.Get("Customization", "Roof", "Roof"), VehicleMod.Roof);
            AddModListItem(bodyMenu, Localization.Get("Customization", "Livery", "Custom Paint / Livery"), VehicleMod.Livery);

            // 3. Paint & Colors
            var paintMenu = menuPool.AddSubMenu(customMenu, Localization.Get("Customization", "PaintMenu", "Paint & Colors"));
            AddColorSelector(paintMenu, Localization.Get("Customization", "PrimaryColor", "Primary Color"), true);
            AddColorSelector(paintMenu, Localization.Get("Customization", "SecondaryColor", "Secondary Color"), false);

            // 4. Neons
            var neonMenu = menuPool.AddSubMenu(customMenu, Localization.Get("Customization", "NeonsMenu", "Neon Lights & Kits"));
            var toggleNeons = new UIMenuCheckboxItem(Localization.Get("Customization", "ToggleAllNeons", "Toggle Neons"), false, "Enables underbody lights");
            toggleNeons.CheckboxEvent += (sender, state) =>
            {
                Vehicle v = GetPlayerVehicle();
                if (v != null)
                {
                    v.SetNeonLightsOn(VehicleNeonLight.Left, state);
                    v.SetNeonLightsOn(VehicleNeonLight.Right, state);
                    v.SetNeonLightsOn(VehicleNeonLight.Front, state);
                    v.SetNeonLightsOn(VehicleNeonLight.Back, state);
                }
            };
            neonMenu.AddItem(toggleNeons);

            var neonColorList = new List<dynamic> { "Blue", "Red", "Green", "Yellow", "Purple", "Orange", "White", "Pink", "Cyan" };
            var neonColors = new[] { Color.DeepSkyBlue, Color.Red, Color.LimeGreen, Color.Yellow, Color.Purple, Color.Orange, Color.White, Color.HotPink, Color.Cyan };
            var neonColorItem = new UIMenuListItem(Localization.Get("Customization", "NeonColor", "Neon Color"), neonColorList, 0, "Changes the neon color");
            neonColorItem.OnListChanged += (sender, index) =>
            {
                Vehicle v = GetPlayerVehicle();
                if (v != null)
                {
                    v.NeonLightsColor = neonColors[index];
                }
            };
            neonMenu.AddItem(neonColorItem);

            // 5. Window Tint
            var tintMenu = menuPool.AddSubMenu(customMenu, Localization.Get("Customization", "WindowTintMenu", "Window Tint"));
            var tintList = new List<dynamic> { "None", "Light Smoke", "Dark Smoke", "Limo", "Pure Black", "Green" };
            var tintValues = new[] { VehicleWindowTint.None, VehicleWindowTint.LightSmoke, VehicleWindowTint.DarkSmoke, VehicleWindowTint.Limo, VehicleWindowTint.PureBlack, VehicleWindowTint.Green };
            var tintItem = new UIMenuListItem(Localization.Get("Customization", "WindowTint", "Tint Shade"), tintList, 0, "Changes the window tint");
            tintItem.OnListChanged += (sender, index) =>
            {
                Vehicle v = GetPlayerVehicle();
                if (v != null)
                {
                    v.WindowTint = tintValues[index];
                }
            };
            tintMenu.AddItem(tintItem);

            // 6. Wheels & Tires
            var wheelsMenu = menuPool.AddSubMenu(customMenu, Localization.Get("Customization", "WheelsMenu", "Wheels & Tires"));
            var bpTires = new UIMenuCheckboxItem(Localization.Get("Customization", "BulletproofTires", "Bulletproof Tires"), false, "Never puncture");
            bpTires.CheckboxEvent += (sender, state) =>
            {
                Vehicle v = GetPlayerVehicle();
                if (v != null)
                {
                    v.CanTiresBurst = !state;
                }
            };
            wheelsMenu.AddItem(bpTires);

            // 7. License Plates
            var plateMenu = menuPool.AddSubMenu(customMenu, Localization.Get("Customization", "PlateMenu", "Vehicle License Plate"));
            var plateStyles = new List<dynamic> { "Blue on White", "Yellow on Black", "Yellow on Blue", "Blue on White 2", "North Yankton", "San Andreas Black" };
            var plateItem = new UIMenuListItem(Localization.Get("Customization", "PlateStyle", "Plate Style"), plateStyles, 0, "Changes the license plate style");
            plateItem.OnListChanged += (sender, index) =>
            {
                Vehicle v = GetPlayerVehicle();
                if (v != null)
                {
                    v.NumberPlateType = (NumberPlateType)index;
                }
            };
            plateMenu.AddItem(plateItem);

            var plateTextItem = new UIMenuItem(Localization.Get("Customization", "CustomPlateText", "Enter Plate Text"), Localization.Get("Customization", "CustomPlateTextDesc", "Changes the license plate text"));
            plateTextItem.Activated += (sender, selected) =>
            {
                Vehicle v = GetPlayerVehicle();
                if (v != null)
                {
                    string input = Game.GetUserInput(8);
                    if (!string.IsNullOrEmpty(input))
                    {
                        v.NumberPlate = input.ToUpper();
                        UI.Notify("~g~Plate changed to: ~w~" + input.ToUpper());
                    }
                }
            };
            plateMenu.AddItem(plateTextItem);
        }

        private Vehicle GetPlayerVehicle()
        {
            Ped player = Game.Player.Character;
            if (player.IsInVehicle())
            {
                Vehicle v = player.CurrentVehicle;
                v.InstallModKit();
                return v;
            }
            UI.Notify("~r~" + Localization.Get("Customization", "NoVehicleMsg", "You must be inside a vehicle!"));
            return null;
        }

        private void AddModListItem(UIMenu menu, string label, VehicleMod mod)
        {
            // Clean numbers: Stock, 1, 2, 3, 4, 5...
            var options = new List<dynamic>();
            options.Add("Stock");
            for (int i = 1; i <= 25; i++)
            {
                options.Add(i.ToString());
            }

            var item = new UIMenuListItem(label, options, 0, "Changes " + label);
            item.OnListChanged += (sender, index) =>
            {
                Vehicle v = GetPlayerVehicle();
                if (v != null)
                {
                    int modIndex = index - 1; // 0 -> Stock (-1 in GTA)
                    v.SetMod(mod, modIndex, false);
                }
            };
            menu.AddItem(item);
        }

        private void AddToggleModItem(UIMenu menu, string label, VehicleToggleMod toggleMod)
        {
            var item = new UIMenuCheckboxItem(label, false, "Enables or disables " + label);
            item.CheckboxEvent += (sender, state) =>
            {
                Vehicle v = GetPlayerVehicle();
                if (v != null)
                {
                    v.ToggleMod(toggleMod, state);
                }
            };
            menu.AddItem(item);
        }

        private void AddColorSelector(UIMenu menu, string label, bool isPrimary)
        {
            var colorNames = new List<dynamic> { "Black", "White", "Red", "Blue", "Yellow", "Verde Lima", "Orange", "Pure Gold", "Silver", "Purple" };
            var colorValues = new[] { VehicleColor.MetallicBlack, VehicleColor.PureWhite, VehicleColor.MetallicFormulaRed, VehicleColor.MetallicBlue, VehicleColor.MetallicTaxiYellow, VehicleColor.MatteLimeGreen, VehicleColor.MetallicOrange, VehicleColor.PureGold, VehicleColor.MetallicSilver, VehicleColor.MetallicSpinnakerBlue };

            var item = new UIMenuListItem(label, colorNames, 0, "Changes " + label);
            item.OnListChanged += (sender, index) =>
            {
                Vehicle v = GetPlayerVehicle();
                if (v != null)
                {
                    if (isPrimary) v.PrimaryColor = colorValues[index];
                    else v.SecondaryColor = colorValues[index];
                }
            };
            menu.AddItem(item);
        }

        public void OnTick() { }
    }
}

namespace AvernesTreiner.Modules
{
    public class VehicleModule : IAvernesModule
    {
        public string Name { get { return "Vehicle"; } }

        private MenuPool _pool;
        private bool _vehicleGodMode = false;
        private bool _speedBoost = false;
        private bool _hornJump = false;
        private bool _vehicleJump = false;
        private bool _driveOnWater = false;
        private bool _speedometer = false;
        private bool _driftMode = false;
        private bool _seatbelt = true;
        private bool _autoRepair = false;
        private bool _superTorque = false;
        private bool _autoFlip = false;

        // Autopilot State
        private bool _autopilotActive = false;
        private int _autopilotStartTime = 0;
        private int _autopilotStyle = 786603; // Safe / Legal by default
        private float _autopilotAggressiveness = 0.0f;
        private float _autopilotSpeed = 28.0f; // ~100 km/h
        private Vector3 _autopilotDestination = Vector3.Zero;
        private int _brakeHoldCounter = 0;
        private int _stuckCounter = 0;
        private bool _trafficRammer = false;
        private readonly List<Prop> _spawnedRamps = new List<Prop>();

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
            var autoRepItem = new UIMenuCheckboxItem("Continuous Auto-Repair", _autoRepair, "Instantly repairs the vehicle whenever it takes damage");
            autoRepItem.CheckboxEvent += (sender, state) => { _autoRepair = state; };
            vehMenu.AddItem(autoRepItem);

            // Vehicle God Mode
            var godItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "VehicleGodMode", "Invincible Vehicle"), _vehicleGodMode, Localization.Get("Vehicle", "VehicleGodModeDesc", "Indestructible"));
            godItem.CheckboxEvent += (sender, state) =>
            {
                _vehicleGodMode = state;
                Ped player = Game.Player.Character;
                if (player.IsInVehicle())
                {
                    player.CurrentVehicle.IsInvincible = _vehicleGodMode;
                    player.CurrentVehicle.CanBeVisiblyDamaged = !_vehicleGodMode;
                }
            };
            vehMenu.AddItem(godItem);

            // Seatbelt (Anti-Fly through windshield & anti-fall from bikes)
            var seatItem = new UIMenuCheckboxItem("Seatbelt (Anti-Ejection)", _seatbelt, "Prevents windshield ejection in crashes and keeps you on motorcycles");
            seatItem.CheckboxEvent += (sender, state) =>
            {
                _seatbelt = state;
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
            var boostItem = new UIMenuCheckboxItem("Nitro Boost / NOS (Hold SHIFT or X)", _speedBoost, "Extreme acceleration while holding SHIFT or X");
            boostItem.CheckboxEvent += (sender, state) => { _speedBoost = state; };
            vehMenu.AddItem(boostItem);

            // Super Torque
            var torqueItem = new UIMenuCheckboxItem("Super Engine (3x Torque)", _superTorque, "Multiplies engine torque for brutal acceleration");
            torqueItem.CheckboxEvent += (sender, state) => { _superTorque = state; };
            vehMenu.AddItem(torqueItem);

            // Vehicle Jump (Ruiner 2000 style)
            var jumpItem = new UIMenuCheckboxItem("Vehicle Jump (Space)", _vehicleJump, "Press SPACE to jump over obstacles and other cars");
            jumpItem.CheckboxEvent += (sender, state) => { _vehicleJump = state; };
            vehMenu.AddItem(jumpItem);

            // Horn Jump
            var hornItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "HornJump", "Horn Jump"), _hornJump, Localization.Get("Vehicle", "HornJumpDesc", "Honk to perform a jump"));
            hornItem.CheckboxEvent += (sender, state) => { _hornJump = state; };
            vehMenu.AddItem(hornItem);

            // Auto-Flip
            var autoFlipItem = new UIMenuCheckboxItem("Auto-Flip (Anti-Rollover)", _autoFlip, "Automatically rights the vehicle if it rolls over");
            autoFlipItem.CheckboxEvent += (sender, state) => { _autoFlip = state; };
            vehMenu.AddItem(autoFlipItem);

            // Drift Mode
            var driftItem = new UIMenuCheckboxItem("Drift Mode", _driftMode, "Reduces rear grip for smooth drifting");
            driftItem.CheckboxEvent += (sender, state) => { _driftMode = state; };
            vehMenu.AddItem(driftItem);

            // Drive on Water (Jesus Car)
            var waterItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "DriveOnWater", "Drive on Water (Jesus Car)"), _driveOnWater, Localization.Get("Vehicle", "DriveOnWaterDesc", "The car travels over water without sinking or damaging the engine"));
            waterItem.CheckboxEvent += (sender, state) => { _driveOnWater = state; };
            vehMenu.AddItem(waterItem);

            // Spawn Instant Stunt Ramp
            var rampItem = new UIMenuItem("Create Stunt Ramp Ahead", "Spawns a perfect jump ramp 15 meters in front of the car");
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

            var clearRamps = new UIMenuItem("Remove Created Ramps", "Removes all stunt ramps from the map");
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
            var autoMenu = menuPool.AddSubMenu(vehMenu, "Smart Autopilot (Waypoint)");

            var autoToggle = new UIMenuItem("Start Autopilot", "Drives autonomously to the GPS waypoint using advanced AI");
            autoToggle.Activated += (sender, selected) =>
            {
                StartAutopilot();
            };
            autoMenu.AddItem(autoToggle);

            var autoCancel = new UIMenuItem("Cancel Autopilot", "Immediately returns manual control of the car");
            autoCancel.Activated += (sender, selected) =>
            {
                CancelAutopilot(true, "~g~Autopilot cancelled manually.");
            };
            autoMenu.AddItem(autoCancel);

            var styleList = new List<dynamic> { "Safe / Legal (Follows Rules)", "Fast / Avoids Traffic", "Escape / Aggressive (Insane / No Stops)" };
            var styleItem = new UIMenuListItem("Driving Style", styleList, 0, "Changes the AI's traffic behavior");
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

            var rammerItem = new UIMenuCheckboxItem("Ramming Mode (Push Traffic)", _trafficRammer, "Forces a path by pushing vehicles blocking your way");
            rammerItem.CheckboxEvent += (sender, state) => { _trafficRammer = state; };
            autoMenu.AddItem(rammerItem);

            var speedList = new List<dynamic> { "60 KM/H (Cruise)", "90 KM/H (Default)", "130 KM/H (Fast)", "180 KM/H (Maximum)" };
            var speedVals = new[] { 17.0f, 25.0f, 36.0f, 50.0f };
            var speedItem = new UIMenuListItem("Cruise Speed", speedList, 1, "Sets the AI target speed");
            speedItem.OnListChanged += (sender, index) =>
            {
                _autopilotSpeed = speedVals[index];
                UI.Notify("~b~Autopilot Speed: ~w~" + speedList[index]);
            };
            autoMenu.AddItem(speedItem);

            // Speedometer Toggle
            var speedoItem = new UIMenuCheckboxItem(Localization.Get("Vehicle", "Speedometer", "Digital Speedometer"), _speedometer, Localization.Get("Vehicle", "SpeedometerDesc", "Displays speed and gear on screen"));
            speedoItem.CheckboxEvent += (sender, state) => { _speedometer = state; };
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
            var openDoors = new UIMenuItem(Localization.Get("Vehicle", "OpenAllDoors", "Open All Doors"), "Opens all doors, hood, and trunk");
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

            var closeDoors = new UIMenuItem(Localization.Get("Vehicle", "CloseAllDoors", "Close All Doors"), "Closes all doors");
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
                    var item = new UIMenuItem(label, "Model: " + m + " (" + addon.category + ")");
                    item.Activated += (sender, selected) => { SpawnVehicle(m); };
                    addonMenu.AddItem(item);
                }
            }
            else
            {
                addonMenu.AddItem(new UIMenuItem("No vehicles in addons.json", "Edit scripts/AvernesTreiner/addons.json"));
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

            AddCategoryMenu(origCatsMenu, menuPool, "Super", new[]
            {
                "adder", "autarch", "banshee2", "bullet", "cheetah", "cyclone", "deveste", "entity2", "entityxf",
                "emerus", "fmj", "furia", "gp1", "infernus", "italigtb", "italigtb2", "krieger", "le7b", "nero",
                "nero2", "osiris", "penetrator", "pfister811", "proto", "reaper", "sc1", "sheava", "sultanrs",
                "t20", "taipan", "tempesta", "tezeract", "thrax", "tigon", "turismor", "tyrant", "tyrus",
                "vacca", "vagner", "visione", "voltic", "xa21", "zentorno", "zorrusso"
            });

            AddCategoryMenu(origCatsMenu, menuPool, "Sports Cars", new[]
            {
                "alpha", "banshee", "bestiagts", "blista2", "buffalo", "buffalo2", "buffalo3", "carbonizzare", "comet2",
                "comet3", "comet4", "comet5", "coquette", "coquette4", "drafter", "elegy", "elegy2", "feltzer2", "flashgt",
                "furoregt", "fusilade", "futo", "gb200", "hotring", "infernus2", "issi7", "italigto", "jester", "jester2",
                "jugular", "khamelion", "komoda", "kuruma", "locust", "lynx", "massacro", "massacro2", "neo", "neon",
                "ninef", "ninef2", "omnis", "paragon", "pariah", "penumbra", "raiden", "rapidgt", "raptor", "revolter",
                "ruston", "schafter2", "schafter3", "schafter4", "schlagen", "schwarzer", "sentinel3", "seven70", "specter",
                "streiter", "sugoi", "sultan", "surano", "tropos", "verlierer2"
            });

            AddCategoryMenu(origCatsMenu, menuPool, "Sports Classics", new[]
            {
                "arwing", "casco", "cheetah2", "coquette2", "coquette3", "deluxo", "dynasty", "gt500", "infernusclassic",
                "jb700", "jb7002", "mamba", "manana", "manana2", "michelli", "monroe", "nebula", "peyote", "peyote2",
                "pigalle", "retinue", "retinue2", "savestra", "stinger", "stingergt", "stromberg", "swinger", "torero",
                "tornadob", "turismo2", "viseris", "ztype", "zion3"
            });

            AddCategoryMenu(origCatsMenu, menuPool, "Muscle Cars", new[]
            {
                "blade", "buccaneer", "buccaneer2", "chino", "chino2", "clique", "coquette3", "deviant", "dominator",
                "dominator2", "dominator3", "dukes", "dukes2", "ellie", "faction", "faction2", "faction3", "gauntlet",
                "gauntlet2", "gauntlet3", "gauntlet4", "hermes", "hotknife", "hustler", "impaler", "imperator", "imperator2",
                "imperator3", "lurcher", "moonbeam", "moonbeam2", "nightshade", "phoenix", "picador", "ratloader", "ratloader2",
                "ruiner", "ruiner2", "sabregt", "sabregt2", "slamvan", "slamvan2", "slamvan3", "stalion", "tampa", "tampa3",
                "tulip", "vamos", "vigilante", "virgo", "virgo2", "virgo3", "voodoo", "voodoo2", "yosemite", "yosemite2"
            });

            AddCategoryMenu(origCatsMenu, menuPool, "Sedans, Coupes & SUVs", new[]
            {
                "asea", "asterope", "cog55", "cognoscenti", "emperor", "fugitive", "glendale", "ingot", "intruder",
                "premier", "primo", "primo2", "regina", "romero", "schafter", "stanier", "stratum", "surge", "tailgater",
                "warrener", "washington", "exemplar", "f620", "felon", "felon2", "jackal", "oracle", "oracle2", "sentinel",
                "sentinel2", "windsor", "windsor2", "zion", "zion2", "baller", "baller2", "baller3", "baller4", "baller5",
                "baller6", "bjxl", "cavalcade", "cavalcade2", "contender", "dubsta", "dubsta2", "dubsta3", "fq2", "granger",
                "gresley", "habanero", "huntley", "landstalker", "landstalker2", "mesa", "mesa2", "mesa3", "novak", "patriot",
                "patriot2", "radi", "rebla", "rocoto", "seminole", "serrano", "toros", "xls"
            });

            AddCategoryMenu(origCatsMenu, menuPool, "Motorcycles & Quad Bikes", new[]
            {
                "akuma", "avarus", "bagger", "bati", "bati2", "bf400", "carbonrs", "chimera", "cliffhanger", "daemon",
                "daemon2", "defiler", "deathbike", "deathbike2", "deathbike3", "diablous", "diablous2", "double", "enduro",
                "esskey", "faggio", "faggio2", "faggio3", "fcr", "fcr2", "gargoyle", "hakuchou", "hakuchou2", "hexer",
                "innovation", "lectro", "manchez", "nemesis", "nightblade", "oppressor", "oppressor2", "pcj", "ratbike",
                "rrocket", "ruffian", "sanchez", "sanchez2", "sanctus", "shotaro", "sovereign", "stryder", "thrust", "vader",
                "vindicator", "vortex", "wolfsbane", "zombiea", "zombieb", "bmx", "cruiser", "fixter", "scorcher", "tribike",
                "tribike2", "tribike3"
            });

            AddCategoryMenu(origCatsMenu, menuPool, "Off-Road", new[]
            {
                "bfinjection", "bifta", "blazer", "blazer2", "blazer3", "blazer4", "blazer5", "bodhi2", "brawler", "bruiser",
                "bruiser2", "bruiser3", "caracara", "caracara2", "dloader", "dune", "dune2", "dune3", "dune4", "dune5",
                "everon", "freecrawler", "hellion", "insurgent", "insurgent2", "insurgent3", "kamacho", "kalahari", "marshall",
                "menacer", "monster", "nightshark", "outlaw", "rancherxl", "rebel", "rebel2", "riata", "sandking", "sandking2",
                "technical", "technical2", "technical3", "trophytruck", "trophytruck2", "vagrant", "wastelander", "zhaba"
            });

            AddCategoryMenu(origCatsMenu, menuPool, "Helicopters & Planes", new[]
            {
                "akula", "annihilator", "buzzard", "buzzard2", "cargobob", "cargobob2", "cargobob3", "cargobob4", "frogger",
                "frogger2", "havok", "hunter", "maverick", "savage", "seasparrow", "skylift", "supervolito", "supervolito2",
                "swift", "swift2", "valkyrie", "valkyrie2", "volatus", "alphaz1", "avenger", "avenger2", "besra", "blimp",
                "blimp2", "blimp3", "bombushka", "cargoplane", "cuban800", "dodo", "duster", "howard", "hydra", "jet",
                "lazer", "luxor", "luxor2", "mammatus", "microlight", "miljet", "mogul", "molotok", "nimbus", "nokota",
                "pyro", "rogue", "seabreeze", "shamal", "starling", "strikeforce", "titan", "tula", "velum", "velum2",
                "vestra", "volatol"
            });

            AddCategoryMenu(origCatsMenu, menuPool, "Boats, Military & Emergency", new[]
            {
                "dinghy", "dinghy2", "dinghy3", "dinghy4", "jetmax", "marquis", "seashark", "seashark2", "seashark3",
                "speeder", "speeder2", "squalo", "submersible", "submersible2", "suntrap", "toro", "toro2", "tropic",
                "tropic2", "tug", "apc", "barracks", "barracks2", "barracks3", "chernobog", "crusader", "halftrack",
                "khanjali", "minitank", "rhino", "scarab", "scarab2", "scarab3", "thruster", "trailersmall2", "vetir",
                "ambulance", "fbi", "fbi2", "firetruk", "pbus", "police", "police2", "police3", "police4", "policeb",
                "policet", "pranger", "predator", "riot", "riot2", "sheriff", "sheriff2"
            });
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
            _stuckCounter = 0;
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
                var item = new UIMenuItem(m.ToUpper(), "Spawn " + m);
                item.Activated += (sender, selected) => { SpawnVehicle(m); };
                catMenu.AddItem(item);
            }
        }

        private void SpawnVehicle(string modelName)
        {
            try
            {
                Ped player = Game.Player.Character;
                Model model = new Model(modelName);
                model.Request(2500);
                if (model.IsInCdImage && model.IsValid)
                {
                    Vector3 spawnPos = player.Position + player.ForwardVector * 4.0f;
                    Vehicle v = World.CreateVehicle(model, spawnPos, player.Heading);
                    if (v != null && v.Exists())
                    {
                        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, v.Handle, true, true);
                        v.IsPersistent = true;
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
                        {
                            v.IsInvincible = true;
                            v.CanBeVisiblyDamaged = false;
                        }
                        UI.Notify("~g~Vehicle " + modelName.ToUpper() + " created!");
                    }
                }
                else
                {
                    UI.Notify("~r~Invalid or unloaded model: " + modelName);
                }
                model.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Spawn error: " + ex.Message);
            }
        }

        public void OnTick()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            // Seatbelt
            if (_seatbelt)
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
                        v.IsInvincible = true;
                        v.CanBeVisiblyDamaged = false;
                        v.Health = 1000;
                    }

                    if (_autoRepair && (v.Health < 1000 || v.EngineHealth < 1000))
                    {
                        v.Repair();
                    }

                    // Nitro Boost / NOS (SHIFT or X)
                    if (_speedBoost && (Game.IsKeyPressed(Keys.ShiftKey) || Game.IsKeyPressed(Keys.X)))
                    {
                        v.ApplyForce(v.ForwardVector * 1.8f);
                        v.MaxSpeed = 400.0f;
                    }

                    if (_superTorque && Game.IsControlPressed(0, GTA.Control.VehicleAccelerate))
                    {
                        v.EnginePowerMultiplier = 2.5f;
                    }

                    if (_driftMode)
                    {
                        Function.Call(Hash.SET_VEHICLE_REDUCE_GRIP, v.Handle, true);
                    }

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
                    if (_autoFlip)
                    {
                        if (v.UpVector.Z < -0.1f || Math.Abs(v.Rotation.Y) > 75.0f || Math.Abs(v.Rotation.X) > 75.0f)
                        {
                            v.Rotation = new Vector3(0f, 0f, v.Rotation.Z);
                            v.PlaceOnGround();
                        }
                    }

                    // Drive on Water (Jesus Car)
                    if (_driveOnWater)
                    {
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
                        if (_trafficRammer || _autopilotAggressiveness >= 1.0f)
                        {
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

                        // Anti-Stuck & Overtake Watchdog
                        if (currentKmh < 3.0f && (Game.GameTime - _autopilotStartTime > 2000))
                        {
                            _stuckCounter++;

                            // If in Fast or Aggressive mode and blocked by traffic:
                            if (_autopilotAggressiveness > 0.3f)
                            {
                                // Step 1: Honk and scare blocking drivers ahead (~1s stuck)
                                if (_stuckCounter > 30)
                                {
                                    v.SoundHorn(800);

                                    Vehicle[] blockingVehs = World.GetNearbyVehicles(v.Position + v.ForwardVector * 8.0f, 7.0f);
                                    foreach (var b in blockingVehs)
                                    {
                                        if (b != null && b.Exists() && b != v)
                                        {
                                            Ped d = b.GetPedOnSeat(VehicleSeat.Driver);
                                            if (d != null && d.Exists() && !d.IsPlayer)
                                            {
                                                d.Task.FleeFrom(player); // Panics and steers out of the way!
                                            }
                                        }
                                    }
                                }

                                // Step 2: Push forward & re-issue overtake (~2.5s stuck)
                                if (_stuckCounter > 90)
                                {
                                    _stuckCounter = 30; // reset
                                    v.Speed = 8.0f; // Break out of dead stop
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
                            _stuckCounter = 0;
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
                if (_autopilotActive)
                {
                    CancelAutopilot(false);
                }
            }
        }
    }
}

namespace AvernesTreiner.Modules
{
    public class WeaponModule : IAvernesModule
    {
        public string Name { get { return "Weapon"; } }

        private bool _infiniteAmmo = false;
        private bool _explosiveAmmo = false;
        private bool _fireAmmo = false;
        private bool _superDamage = false;
        private bool _teleportGun = false;
        private bool _forceGun = false;
        private bool _rpgAmmo = false;
        private bool _infiniteClip = false;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var weapMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuWeapons", "Weapons & Ammo"));

            // Give All Weapons
            var giveAll = new UIMenuItem(Localization.Get("Weapons", "GiveAllWeapons", "Get All Weapons"), Localization.Get("Weapons", "GiveAllWeaponsDesc", "Gives you all weapons"));
            giveAll.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                foreach (WeaponHash w in Enum.GetValues(typeof(WeaponHash)))
                {
                    try { player.Weapons.Give(w, 9999, false, true); } catch { }
                }
                UI.Notify("~g~All weapons granted with maximum ammo!");
            };
            weapMenu.AddItem(giveAll);

            // Remove All Weapons
            var removeAll = new UIMenuItem(Localization.Get("Weapons", "RemoveAllWeapons", "Remove All Weapons"), Localization.Get("Weapons", "RemoveAllWeaponsDesc", "Removes your inventory"));
            removeAll.Activated += (sender, selected) =>
            {
                Game.Player.Character.Weapons.RemoveAll();
                UI.Notify("~y~All weapons removed.");
            };
            weapMenu.AddItem(removeAll);

            // Infinite Ammo
            var infAmmo = new UIMenuCheckboxItem(Localization.Get("Weapons", "InfiniteAmmo", "Infinite Ammo"), _infiniteAmmo, Localization.Get("Weapons", "InfiniteAmmoDesc", "Infinite ammo"));
            infAmmo.CheckboxEvent += (sender, state) => { _infiniteAmmo = state; };
            weapMenu.AddItem(infAmmo);

            // Bottomless Clip (No reload)
            var clipItem = new UIMenuCheckboxItem("Infinite Clip (Never Reload)", _infiniteClip, "Fire continuously without changing magazines");
            clipItem.CheckboxEvent += (sender, state) =>
            {
                _infiniteClip = state;
                Ped player = Game.Player.Character;
                if (player != null && player.Exists())
                {
                    Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, player.Handle, _infiniteClip);
                }
            };
            weapMenu.AddItem(clipItem);

            // Teleport Gun
            var teleGunItem = new UIMenuCheckboxItem("Teleport Gun", _teleportGun, "Teleports the player to wherever the shot hits");
            teleGunItem.CheckboxEvent += (sender, state) => { _teleportGun = state; };
            weapMenu.AddItem(teleGunItem);

            // Force Gun
            var forceGunItem = new UIMenuCheckboxItem("Force / Gravity Gun", _forceGun, "Blasts vehicles and people hit by the shot away");
            forceGunItem.CheckboxEvent += (sender, state) => { _forceGun = state; };
            weapMenu.AddItem(forceGunItem);

            // RPG Ammo
            var rpgAmmoItem = new UIMenuCheckboxItem("RPG Ammo (Real Explosive Rounds)", _rpgAmmo, "Each shot causes a real missile explosion");
            rpgAmmoItem.CheckboxEvent += (sender, state) => { _rpgAmmo = state; };
            weapMenu.AddItem(rpgAmmoItem);

            // Explosive Ammo
            var expAmmo = new UIMenuCheckboxItem(Localization.Get("Weapons", "ExplosiveAmmo", "Explosive Ammo"), _explosiveAmmo, Localization.Get("Weapons", "ExplosiveAmmoDesc", "Shots explode"));
            expAmmo.CheckboxEvent += (sender, state) => { _explosiveAmmo = state; };
            weapMenu.AddItem(expAmmo);

            // Fire Ammo
            var fireAmmo = new UIMenuCheckboxItem(Localization.Get("Weapons", "FireAmmo", "Incendiary Ammo"), _fireAmmo, Localization.Get("Weapons", "FireAmmoDesc", "Shots set targets on fire"));
            fireAmmo.CheckboxEvent += (sender, state) => { _fireAmmo = state; };
            weapMenu.AddItem(fireAmmo);

            // Super Damage
            var superDmg = new UIMenuCheckboxItem(Localization.Get("Weapons", "SuperDamage", "Super Damage (One-Hit Kill)"), _superDamage, Localization.Get("Weapons", "SuperDamageDesc", "Kills in one shot"));
            superDmg.CheckboxEvent += (sender, state) => { _superDamage = state; };
            weapMenu.AddItem(superDmg);
        }

        public void OnTick()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            if (_infiniteAmmo && player.Weapons.Current != null)
            {
                player.Weapons.Current.Ammo = player.Weapons.Current.MaxAmmo;
            }

            if (_infiniteClip)
            {
                Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, player.Handle, true);
            }

            if (_explosiveAmmo)
            {
                Game.Player.SetExplosiveAmmoThisFrame();
            }

            if (_fireAmmo)
            {
                Game.Player.SetFireAmmoThisFrame();
            }

            if (_superDamage)
            {
                Game.Player.SetSuperJumpThisFrame();
                Function.Call(Hash.SET_PLAYER_WEAPON_DAMAGE_MODIFIER, Game.Player.Handle, 100.0f);
            }

            // Shooting event checks
            if (player.IsShooting)
            {
                OutputArgument outCoord = new OutputArgument();
                bool hit = Function.Call<bool>(Hash.GET_PED_LAST_WEAPON_IMPACT_COORD, player.Handle, outCoord);
                if (hit)
                {
                    Vector3 hitPos = outCoord.GetResult<Vector3>();
                    if (hitPos != Vector3.Zero)
                    {
                        if (_teleportGun)
                        {
                            player.Position = hitPos + new Vector3(0, 0, 1.0f);
                        }
                        if (_forceGun)
                        {
                            World.AddExplosion(hitPos, ExplosionType.Extinguisher, 6.0f, 0.0f, false, true);
                        }
                        if (_rpgAmmo)
                        {
                            World.AddExplosion(hitPos, ExplosionType.GrenadeL, 3.5f, 1.0f, true, false);
                        }
                    }
                }
            }
        }
    }
}

namespace AvernesTreiner.Modules
{
    public class WorldModule : IAvernesModule
    {
        public string Name { get { return "World"; } }

        private bool _freezeTime = false;
        private bool _moonGravity = false;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var worldMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuWorld", "World & Time"));

            // Bullet Time / Slow Motion
            var bulletMenu = menuPool.AddSubMenu(worldMenu, "Slow Motion / Bullet Time (Matrix Mode)");
            AddBulletTimeOption(bulletMenu, "Normal Speed (1.0x)", 1.0f);
            AddBulletTimeOption(bulletMenu, "Light Slow Motion (0.7x)", 0.7f);
            AddBulletTimeOption(bulletMenu, "Medium Slow Motion (0.4x)", 0.4f);
            AddBulletTimeOption(bulletMenu, "Extreme Matrix Mode (0.15x)", 0.15f);

            // Weather
            var weatherMenu = menuPool.AddSubMenu(worldMenu, Localization.Get("World", "Weather", "Change Weather"));
            AddWeatherOption(weatherMenu, Localization.Get("World", "SetClear", "Clear"), Weather.Clear);
            AddWeatherOption(weatherMenu, Localization.Get("World", "SetExtraSunny", "Extra Sunny"), Weather.ExtraSunny);
            AddWeatherOption(weatherMenu, Localization.Get("World", "SetClouds", "Clouds"), Weather.Clouds);
            AddWeatherOption(weatherMenu, Localization.Get("World", "SetRain", "Rain"), Weather.Raining);
            AddWeatherOption(weatherMenu, Localization.Get("World", "SetThunder", "Thunderstorm"), Weather.ThunderStorm);
            AddWeatherOption(weatherMenu, Localization.Get("World", "SetSnow", "Snow"), Weather.Snowing);
            AddWeatherOption(weatherMenu, Localization.Get("World", "SetBlizzard", "Blizzard"), Weather.Blizzard);
            AddWeatherOption(weatherMenu, Localization.Get("World", "SetHalloween", "Halloween"), Weather.Halloween);

            // Visual Filters
            var filterMenu = menuPool.AddSubMenu(worldMenu, "Camera Visual Filters (Timecycle)");
            AddFilterItem(filterMenu, "Matrix Effect (Futuristic Green)", "spectator5");
            AddFilterItem(filterMenu, "Black & White (Film Noir)", "cinema");
            AddFilterItem(filterMenu, "Dramatic Sunset (Vivid Colors)", "v_sundown");
            AddFilterItem(filterMenu, "Eerie Fog (Silent Hill)", "prologue_fog");
            AddFilterItem(filterMenu, "Sepia / Old West Filter", "hud_def_desat_cold");

            var resetFilterItem = new UIMenuItem("Restore Normal Visuals", "Disables all camera filters");
            resetFilterItem.Activated += (sender, selected) =>
            {
                Function.Call(Hash.CLEAR_TIMECYCLE_MODIFIER);
                UI.Notify("~g~Normal visuals restored.");
            };
            filterMenu.AddItem(resetFilterItem);

            // Time
            var timeMenu = menuPool.AddSubMenu(worldMenu, Localization.Get("World", "Time", "Change Time"));
            AddTimeOption(timeMenu, Localization.Get("World", "Morning", "Morning (06:00)"), 6, 0);
            AddTimeOption(timeMenu, Localization.Get("World", "Noon", "Noon (12:00)"), 12, 0);
            AddTimeOption(timeMenu, Localization.Get("World", "Evening", "Evening (18:00)"), 18, 0);
            AddTimeOption(timeMenu, Localization.Get("World", "Midnight", "Midnight (00:00)"), 0, 0);

            // Freeze Time
            var freezeItem = new UIMenuCheckboxItem(Localization.Get("World", "FreezeTime", "Freeze Time"), _freezeTime, Localization.Get("World", "FreezeTimeDesc", "Freezes the clock"));
            freezeItem.CheckboxEvent += (sender, state) =>
            {
                _freezeTime = state;
                Function.Call(Hash.PAUSE_CLOCK, _freezeTime);
            };
            worldMenu.AddItem(freezeItem);

            // Clear Area
            var clearItem = new UIMenuItem(Localization.Get("World", "ClearArea", "Clear Traffic & Pedestrians"), Localization.Get("World", "ClearAreaDesc", "Removes NPCs and vehicles"));
            clearItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                Function.Call(Hash.CLEAR_AREA_OF_PEDS, player.Position.X, player.Position.Y, player.Position.Z, 150.0f, 1);
                Function.Call(Hash.CLEAR_AREA_OF_VEHICLES, player.Position.X, player.Position.Y, player.Position.Z, 150.0f, false, false, false, false, false);
                UI.Notify("~g~Nearby area cleared!");
            };
            worldMenu.AddItem(clearItem);

            // Moon Gravity
            var moonItem = new UIMenuCheckboxItem(Localization.Get("World", "MoonGravity", "Moon Gravity"), _moonGravity, Localization.Get("World", "MoonGravityDesc", "Low gravity"));
            moonItem.CheckboxEvent += (sender, state) =>
            {
                _moonGravity = state;
                World.GravityLevel = _moonGravity ? 1 : 0;
            };
            worldMenu.AddItem(moonItem);
        }

        private void AddBulletTimeOption(UIMenu menu, string label, float scale)
        {
            var item = new UIMenuItem(label, "Sets game speed to " + scale + "x");
            item.Activated += (sender, selected) =>
            {
                Game.TimeScale = scale;
                Function.Call(Hash.SET_TIME_SCALE, scale);
                UI.Notify("~b~Slow Motion: ~w~" + label);
            };
            menu.AddItem(item);
        }

        private void AddFilterItem(UIMenu menu, string label, string modifierName)
        {
            var item = new UIMenuItem(label, "Applies camera filter " + label);
            item.Activated += (sender, selected) =>
            {
                Function.Call(Hash.SET_TIMECYCLE_MODIFIER, modifierName);
                UI.Notify("~b~Filter applied: ~w~" + label);
            };
            menu.AddItem(item);
        }

        private void AddWeatherOption(UIMenu menu, string label, Weather weather)
        {
            var item = new UIMenuItem(label, "Sets weather to " + label);
            item.Activated += (sender, selected) =>
            {
                World.Weather = weather;
                UI.Notify("~b~Weather changed to: ~w~" + label);
            };
            menu.AddItem(item);
        }

        private void AddTimeOption(UIMenu menu, string label, int hours, int minutes)
        {
            var item = new UIMenuItem(label, "Sets time to " + label);
            item.Activated += (sender, selected) =>
            {
                World.CurrentDayTime = new TimeSpan(hours, minutes, 0);
                UI.Notify("~b~Time set to: ~w~" + label);
            };
            menu.AddItem(item);
        }

        public void OnTick() { }
    }
}