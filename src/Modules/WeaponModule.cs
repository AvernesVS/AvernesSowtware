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
    public class WeaponModule : IAvernesModule
    {
        public string Name { get { return "Weapon"; } }

        private bool _infiniteAmmo = ConfigManager.GetBool("Weapons", "InfiniteAmmo", false);
        private bool _explosiveAmmo = ConfigManager.GetBool("Weapons", "ExplosiveAmmo", false);
        private bool _fireAmmo = ConfigManager.GetBool("Weapons", "FireAmmo", false);
        private bool _superDamage = ConfigManager.GetBool("Weapons", "SuperDamage", false);
        private bool _teleportGun = ConfigManager.GetBool("Weapons", "TeleportGun", false);
        private bool _forceGun = ConfigManager.GetBool("Weapons", "ForceGun", false);
        private bool _rpgAmmo = ConfigManager.GetBool("Weapons", "RpgAmmo", false);
        private bool _infiniteClip = ConfigManager.GetBool("Weapons", "InfiniteClip", false);
        private int _lastAmmoRefreshTime = 0;
        private int _lastClipRefreshTime = 0;
        private int _lastWeaponPedHandle = 0;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var weapMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuWeapons", "Weapons & Ammo"));

            // Give All Weapons
            var giveAll = new UIMenuItem(Localization.Get("Weapons", "GiveAllWeapons", "Get All Weapons"), Localization.Get("Weapons", "GiveAllWeaponsDesc", "Gives you all weapons"));
            giveAll.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                int attachedComponents = 0;
                foreach (WeaponHash w in Enum.GetValues(typeof(WeaponHash)))
                {
                    try
                    {
                        player.Weapons.Give(w, 9999, false, true);
                        foreach (WeaponComponent component in Enum.GetValues(typeof(WeaponComponent)))
                        {
                            try
                            {
                                if (Function.Call<bool>(Hash.DOES_WEAPON_TAKE_WEAPON_COMPONENT,
                                    (uint)w, (uint)component))
                                {
                                    Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED,
                                        player.Handle, (uint)w, (uint)component);
                                    attachedComponents++;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                UI.Notify("~g~All available weapons and compatible attachments granted! (" +
                    attachedComponents + " attachments)");
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
            infAmmo.CheckboxEvent += (sender, state) => { _infiniteAmmo = state;
 ConfigManager.SetBool("Weapons", "InfiniteAmmo", _infiniteAmmo); };
            weapMenu.AddItem(infAmmo);

            // Bottomless Clip (No reload)
            var clipItem = new UIMenuCheckboxItem(Localization.Get("Weapons", "InfiniteClip", "Infinite Clip (Never Reload)"), _infiniteClip, Localization.Get("Weapons", "InfiniteClipDesc", "Fire without changing magazines"));
            clipItem.CheckboxEvent += (sender, state) =>
            {
                _infiniteClip = state;

                ConfigManager.SetBool("Weapons", "InfiniteClip", _infiniteClip);
                Ped player = Game.Player.Character;
                if (player != null && player.Exists())
                {
                    Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, player.Handle, _infiniteClip);
                }
            };
            weapMenu.AddItem(clipItem);
            if (_infiniteClip)
            {
                Ped currentPlayer = Game.Player.Character;
                if (currentPlayer != null && currentPlayer.Exists())
                    Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, currentPlayer.Handle, true);
            }

            // Teleport Gun
            var teleGunItem = new UIMenuCheckboxItem(Localization.Get("Weapons", "TeleportGun", "Teleport Gun"), _teleportGun, Localization.Get("Weapons", "TeleportGunDesc", "Teleports the player to the shot impact"));
            teleGunItem.CheckboxEvent += (sender, state) => { _teleportGun = state;
 ConfigManager.SetBool("Weapons", "TeleportGun", _teleportGun); };
            weapMenu.AddItem(teleGunItem);

            // Force Gun
            var forceGunItem = new UIMenuCheckboxItem(Localization.Get("Weapons", "ForceGun", "Force / Gravity Gun"), _forceGun, Localization.Get("Weapons", "ForceGunDesc", "Pushes vehicles and people hit by the shot"));
            forceGunItem.CheckboxEvent += (sender, state) => { _forceGun = state;
 ConfigManager.SetBool("Weapons", "ForceGun", _forceGun); };
            weapMenu.AddItem(forceGunItem);

            // RPG Ammo
            var rpgAmmoItem = new UIMenuCheckboxItem(Localization.Get("Weapons", "RpgAmmo", "RPG Ammo (Real Explosive Rounds)"), _rpgAmmo, Localization.Get("Weapons", "RpgAmmoDesc", "Each shot causes a missile explosion"));
            rpgAmmoItem.CheckboxEvent += (sender, state) => { _rpgAmmo = state;
 ConfigManager.SetBool("Weapons", "RpgAmmo", _rpgAmmo); };
            weapMenu.AddItem(rpgAmmoItem);

            // Explosive Ammo
            var expAmmo = new UIMenuCheckboxItem(Localization.Get("Weapons", "ExplosiveAmmo", "Explosive Ammo"), _explosiveAmmo, Localization.Get("Weapons", "ExplosiveAmmoDesc", "Shots explode"));
            expAmmo.CheckboxEvent += (sender, state) => { _explosiveAmmo = state;
 ConfigManager.SetBool("Weapons", "ExplosiveAmmo", _explosiveAmmo); };
            weapMenu.AddItem(expAmmo);

            // Fire Ammo
            var fireAmmo = new UIMenuCheckboxItem(Localization.Get("Weapons", "FireAmmo", "Incendiary Ammo"), _fireAmmo, Localization.Get("Weapons", "FireAmmoDesc", "Shots set targets on fire"));
            fireAmmo.CheckboxEvent += (sender, state) => { _fireAmmo = state;
 ConfigManager.SetBool("Weapons", "FireAmmo", _fireAmmo); };
            weapMenu.AddItem(fireAmmo);

            // Super Damage
            var superDmg = new UIMenuCheckboxItem(Localization.Get("Weapons", "SuperDamage", "Super Damage (One-Hit Kill)"), _superDamage, Localization.Get("Weapons", "SuperDamageDesc", "Kills in one shot"));
            superDmg.CheckboxEvent += (sender, state) =>
            {
                _superDamage = state;
                ConfigManager.SetBool("Weapons", "SuperDamage", _superDamage);
                Function.Call(Hash.SET_PLAYER_WEAPON_DAMAGE_MODIFIER, Game.Player.Handle, _superDamage ? 100.0f : 1.0f);
            };
            weapMenu.AddItem(superDmg);
            if (_superDamage)
                Function.Call(Hash.SET_PLAYER_WEAPON_DAMAGE_MODIFIER, Game.Player.Handle, 100.0f);
        }

        public void ReloadSettings()
        {
            _infiniteAmmo = ConfigManager.GetBool("Weapons", "InfiniteAmmo", false);
            _explosiveAmmo = ConfigManager.GetBool("Weapons", "ExplosiveAmmo", false);
            _fireAmmo = ConfigManager.GetBool("Weapons", "FireAmmo", false);
            _superDamage = ConfigManager.GetBool("Weapons", "SuperDamage", false);
            _teleportGun = ConfigManager.GetBool("Weapons", "TeleportGun", false);
            _forceGun = ConfigManager.GetBool("Weapons", "ForceGun", false);
            _rpgAmmo = ConfigManager.GetBool("Weapons", "RpgAmmo", false);
            _infiniteClip = ConfigManager.GetBool("Weapons", "InfiniteClip", false);
        }

        public void OnTick()
        {
            bool needsUpdate = _infiniteAmmo || _infiniteClip || _explosiveAmmo || _fireAmmo || _superDamage ||
                               _teleportGun || _forceGun || _rpgAmmo;
            if (!needsUpdate) return;

            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            int now = Game.GameTime;
            bool characterChanged = player.Handle != _lastWeaponPedHandle;
            if (characterChanged)
            {
                _lastWeaponPedHandle = player.Handle;
                // Player-specific weapon flags are not inherited when Story Mode swaps characters.
                Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, player.Handle, _infiniteClip);
                _lastClipRefreshTime = now;
                _lastAmmoRefreshTime = 0;
            }
            else if (_infiniteClip && now - _lastClipRefreshTime >= 500)
            {
                Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, player.Handle, true);
                _lastClipRefreshTime = now;
            }

            if (_infiniteAmmo && (characterChanged || now - _lastAmmoRefreshTime >= 150))
            {
                _lastAmmoRefreshTime = now;
                if (player.Weapons.Current != null)
                    player.Weapons.Current.Ammo = player.Weapons.Current.MaxAmmo;
            }

            // These natives are frame-scoped and must remain in the frame tick.
            if (_explosiveAmmo) Game.Player.SetExplosiveAmmoThisFrame();
            if (_fireAmmo) Game.Player.SetFireAmmoThisFrame();
            if (_superDamage) Game.Player.SetSuperJumpThisFrame();

            // Avoid querying the impact coordinate unless a shot-triggered feature is enabled.
            if ((_teleportGun || _forceGun || _rpgAmmo) && player.IsShooting)
            {
                OutputArgument outCoord = new OutputArgument();
                bool hit = Function.Call<bool>(Hash.GET_PED_LAST_WEAPON_IMPACT_COORD, player.Handle, outCoord);
                if (hit)
                {
                    Vector3 hitPos = outCoord.GetResult<Vector3>();
                    if (hitPos != Vector3.Zero)
                    {
                        if (_teleportGun) player.Position = hitPos + new Vector3(0, 0, 1.0f);
                        if (_forceGun) World.AddExplosion(hitPos, ExplosionType.Extinguisher, 6.0f, 0.0f, false, true);
                        if (_rpgAmmo) World.AddExplosion(hitPos, ExplosionType.GrenadeL, 3.5f, 1.0f, true, false);
                    }
                }
            }
        }
    }
}
