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
    public class BodyguardModule : IAvernesModule
    {
        public string Name { get { return "Bodyguards"; } }
        private int _lastCleanupTime = 0;
        private int _lastFollowMaintenanceTime = 0;
        private bool _followOrdersActive = true;

        private List<Ped> _bodyguards = new List<Ped>();
        private bool _godModeSquad = ConfigManager.GetBool("Bodyguards", "GodModeSquad", false);

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var bgMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuBodyguards", "Bodyguards & Gang"));

            // 1. Single Bodyguards
            var singleMenu = menuPool.AddSubMenu(bgMenu, Localization.Get("Bodyguards", "SpawnIndividual", "Spawn Individual Bodyguard"));
            AddSingleBgItem(singleMenu, "SWAT Officer", "s_m_y_swat_01", WeaponHash.CarbineRifle);
            AddSingleBgItem(singleMenu, "Military Soldier", "s_m_y_marine_01", WeaponHash.CombatMG);
            AddSingleBgItem(singleMenu, "Security Agent", "s_m_m_security_01", WeaponHash.SMG);
            AddSingleBgItem(singleMenu, "Ballas Member", "g_m_y_ballaorig_01", WeaponHash.MicroSMG);
            AddSingleBgItem(singleMenu, "Families Member (Grove Street)", "g_m_y_famdnf_01", WeaponHash.PumpShotgun);
            AddSingleBgItem(singleMenu, "Vagos Member", "g_m_y_salvagoon_01", WeaponHash.CombatPistol);
            AddSingleBgItem(singleMenu, Localization.Get("Bodyguards", "SpawnSniper", "Sniper Bodyguard"), "s_m_y_blackops_01", WeaponHash.SniperRifle);

            // 2. Squads
            var squadMenu = menuPool.AddSubMenu(bgMenu, Localization.Get("Bodyguards", "SpawnSquad", "Spawn Full Gang Squad"));
            AddSquadItem(squadMenu, "Families Squad (Grove Street - 3 Members)", "g_m_y_famdnf_01", WeaponHash.CarbineRifle, 3);
            AddSquadItem(squadMenu, "Ballas Squad (3 Members)", "g_m_y_ballaorig_01", WeaponHash.MicroSMG, 3);
            AddSquadItem(squadMenu, "Los Vagos Squad (3 Members)", "g_m_y_salvagoon_01", WeaponHash.PumpShotgun, 3);
            AddSquadItem(squadMenu, "SWAT Tactical Squad (4 Members)", "s_m_y_swat_01", WeaponHash.CarbineRifle, 4);
            AddSquadItem(squadMenu, "Elite Military Squad (4 Members)", "s_m_y_marine_01", WeaponHash.CombatMG, 4);

            // 3. Gang Armory
            var armoryMenu = menuPool.AddSubMenu(bgMenu, Localization.Get("Bodyguards", "GangArmory", "Gang Armory & Equipment"));
            AddWeaponEquipItem(armoryMenu, "Equip Assault Rifles (Carbine Rifle)", WeaponHash.CarbineRifle);
            AddWeaponEquipItem(armoryMenu, "Equip Heavy Machine Guns (Combat MG)", WeaponHash.CombatMG);
            AddWeaponEquipItem(armoryMenu, "Equip Combat Shotguns", WeaponHash.PumpShotgun);
            AddWeaponEquipItem(armoryMenu, "Equip Submachine Guns (SMG)", WeaponHash.SMG);
            AddWeaponEquipItem(armoryMenu, "Equip Rocket Launcher (RPG)", WeaponHash.RPG);
            AddWeaponEquipItem(armoryMenu, "Equip Minigun", WeaponHash.Minigun);
            AddWeaponEquipItem(armoryMenu,
                Localization.Get("Bodyguards", "EquipSniperRifles", "Equip Sniper Rifles"),
                WeaponHash.SniperRifle);

            var armorAllItem = new UIMenuItem(Localization.Get("Bodyguards", "FullGangArmor", "Full Gang Armor (100% Armor)"), Localization.Get("Bodyguards", "FullGangArmorDesc", "Restores all members health and armor"));
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

            var godSquadItem = new UIMenuCheckboxItem(Localization.Get("Bodyguards", "InvincibleGang", "Invincible Gang (God Mode)"), _godModeSquad, Localization.Get("Bodyguards", "InvincibleGangDesc", "Makes all members immune to bullets and explosions"));
            godSquadItem.CheckboxEvent += (sender, state) =>
            {
                _godModeSquad = state;

                ConfigManager.SetBool("Bodyguards", "GodModeSquad", _godModeSquad);
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
            var cmdMenu = menuPool.AddSubMenu(bgMenu, Localization.Get("Bodyguards", "GroupOrders", "Group Orders & Tactics"));

            var attackTargetItem = new UIMenuItem(Localization.Get("Bodyguards", "AttackTarget", "Everyone Attack Aimed Target"), Localization.Get("Bodyguards", "AttackTargetDesc", "Orders the gang to attack your aimed target"));
            attackTargetItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                Entity target = null;
                RaycastResult ray = World.Raycast(GameplayCamera.Position, GameplayCamera.Direction, 150.0f, IntersectOptions.Peds1);
                if (ray.DitHitEntity && ray.HitEntity is Ped) target = ray.HitEntity;

                if (target != null && target is Ped)
                {
                    _followOrdersActive = false;
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

            var holdItem = new UIMenuItem(Localization.Get("Bodyguards", "HoldPosition", "Hold Position (Stay Put)"), Localization.Get("Bodyguards", "HoldPositionDesc", "Members defend the current area"));
            holdItem.Activated += (sender, selected) =>
            {
                _followOrdersActive = false;
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

            var followItem = new UIMenuItem(Localization.Get("Bodyguards", "FollowPlayer", "Regroup and Follow Player"), Localization.Get("Bodyguards", "FollowPlayerDesc", "Cancels previous orders and resumes following"));
            followItem.Activated += (sender, selected) =>
            {
                _followOrdersActive = true;
                Ped player = Game.Player.Character;
                foreach (var bg in _bodyguards)
                {
                    if (bg != null && bg.Exists())
                    {
                        bg.Task.ClearAll();
                        Function.Call(Hash.SET_PED_AS_GROUP_MEMBER, bg.Handle, player.CurrentPedGroup.Handle);
                    }
                }
                MaintainBodyguardFollow(player, true);
                UI.Notify("~g~Gang regrouped with the player.");
            };
            cmdMenu.AddItem(followItem);

            // 5. Gang War Simulator
            var warItem = new UIMenuItem(Localization.Get("Bodyguards", "GangWar", "Start Gang War (Combat Simulation)"), Localization.Get("Bodyguards", "GangWarDesc", "Starts a local gang shootout"));
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
                int failed = 0;
                foreach (var bg in _bodyguards)
                {
                    if (bg != null && bg.Exists() && !bg.IsDead)
                    {
                        try
                        {
                            bg.Weapons.Give(weapon, 9999, true, true);
                            Function.Call((Hash)0xADF692B254977C0C, bg.Handle, (uint)weapon, true);
                            if (Function.Call<bool>((Hash)0x8DECB02F88F428BC, bg.Handle, (uint)weapon, false))
                                count++;
                            else
                                failed++;
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            DiagnosticsManager.LogException("Bodyguards.EquipWeapon", ex);
                        }
                    }
                }
                DiagnosticsManager.LogInfo("Bodyguards", "Armory command " + label +
                    " completed. Equipped: " + count + "; failed: " + failed + ".");
                UI.Notify("~g~" + count + " members equipped; ~r~" + failed + " failed.");
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
                        bool sniper = weapon == WeaponHash.SniperRifle;
                        bg.MaxHealth = sniper ? 400 : 200;
                        bg.Health = sniper ? 400 : 200;
                        bg.Armor = 100;
                        bg.Weapons.Give(weapon, 9999, true, true);
                        if (sniper)
                            Function.Call((Hash)0xADF692B254977C0C, bg.Handle, (uint)weapon, true);
                        bg.NeverLeavesGroup = true;
                        bg.Accuracy = sniper ? 95 : 85;
                        bg.RelationshipGroup = player.RelationshipGroup;

                        player.CurrentPedGroup.Add(bg, false);
                        Function.Call(Hash.SET_PED_AS_GROUP_MEMBER, bg.Handle, player.CurrentPedGroup.Handle);
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, bg.Handle, 5, true);
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, bg.Handle, 46, true);
                        Function.Call(Hash.SET_PED_CAN_BE_TARGETTED, bg.Handle, false);

                        if (_godModeSquad) bg.IsInvincible = true;

                        _bodyguards.Add(bg);
                        DiagnosticsManager.LogInfo("Bodyguards", (sniper ? "Sniper bodyguard" : "Bodyguard") +
                            " spawned. Entity handle: " + bg.Handle + "; active count: " + _bodyguards.Count +
                            "; health: " + bg.MaxHealth + "; armor: " + bg.Armor + ".");
                        if (_followOrdersActive) MaintainBodyguardFollow(player, true);
                    }
                }
                model.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                DiagnosticsManager.LogException("Bodyguards.Spawn", ex);
                UI.Notify("~r~Could not spawn bodyguard: " + ex.Message);
            }
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
                _followOrdersActive = false;
                int count = 0;
                for (int i = _bodyguards.Count - 1; i >= 0; i--)
                {
                    Ped bg = _bodyguards[i];
                    if (bg != null && bg.Exists())
                    {
                        int handle = bg.Handle;
                        bg.LeaveGroup();
                        bg.Delete();
                        count++;
                        DiagnosticsManager.LogInfo("Bodyguards", "Bodyguard dismissed/deleted. Entity handle: " + handle + ".");
                    }
                }
                _bodyguards.Clear();
                DiagnosticsManager.LogInfo("Bodyguards", "Dismiss all completed. Removed: " + count + "; active count: 0.");
                UI.Notify("~y~" + count + " members dismissed.");
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Error: " + ex.Message);
            }
        }

        public void ReloadSettings()
        {
            _godModeSquad = ConfigManager.GetBool("Bodyguards", "GodModeSquad", false);
            for (int i = 0; i < _bodyguards.Count; i++)
            {
                Ped guard = _bodyguards[i];
                if (guard != null && guard.Exists()) guard.IsInvincible = _godModeSquad;
            }
        }

        private void MaintainBodyguardFollow(Ped player, bool force)
        {
            if (!_followOrdersActive || player == null || !player.Exists() || player.IsInVehicle()) return;
            int now = Game.GameTime;
            if (!force && now - _lastFollowMaintenanceTime < 3000) return;
            _lastFollowMaintenanceTime = now;

            for (int i = 0; i < _bodyguards.Count; i++)
            {
                Ped guard = _bodyguards[i];
                if (guard == null || !guard.Exists() || guard.IsDead ||
                    Function.Call<bool>((Hash)0x4859F1FC66A6278E, guard.Handle, 0)) continue;
                float dx = guard.Position.X - player.Position.X;
                float dy = guard.Position.Y - player.Position.Y;
                float dz = guard.Position.Z - player.Position.Z;
                if (!force && dx * dx + dy * dy + dz * dz < 100.0f) continue;

                float angle = i * 0.9f;
                float offsetX = (float)Math.Cos(angle) * 2.5f;
                float offsetY = -3.0f - (float)Math.Sin(angle) * 2.5f;
                Function.Call((Hash)0x304AE42E357B8C7E, guard.Handle, player.Handle,
                    offsetX, offsetY, 0.0f, 2.5f, -1, 5.0f, true);
            }
        }

        public void OnTick()
        {
            int now = Game.GameTime;
            if (now - _lastCleanupTime < 1000) return;
            _lastCleanupTime = now;

            Ped player = Game.Player.Character;
            MaintainBodyguardFollow(player, false);

            for (int i = _bodyguards.Count - 1; i >= 0; i--)
            {
                Ped guard = _bodyguards[i];
                if (guard == null || !guard.Exists() || guard.IsDead)
                {
                    int handle = guard == null ? 0 : guard.Handle;
                    string reason = guard == null || !guard.Exists() ? "entity no longer exists" : "ped died";
                    _bodyguards.RemoveAt(i);
                    DiagnosticsManager.LogInfo("Bodyguards", "Bodyguard removed from active tracking. Entity handle: " +
                        handle + "; reason: " + reason + "; active count: " + _bodyguards.Count + ".");
                }
            }
        }
    }
}
