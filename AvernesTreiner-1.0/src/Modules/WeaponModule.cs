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
