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
        private int _lastForcedReloadTime = 0;
        private readonly Dictionary<string, WeaponInventoryData> _savedInventories =
            new Dictionary<string, WeaponInventoryData>(StringComparer.OrdinalIgnoreCase);
        private string _lastInventoryJson = string.Empty;
        private int _lastInventoryPedHandle;
        private int _lastInventoryModelHash;
        private int _lastInventorySnapshotTime;
        private int _restoreInventoryAt;
        private bool _inventoryRestorePending;
        private bool _inventoryFileLoaded;
        private static string WeaponInventoryPath
        {
            get { return Path.Combine(ConfigManager.UserDataDirectoryPath, "WeaponInventory.json"); }
        }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            LoadWeaponInventories();
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
            infAmmo.CheckboxEvent += (sender, state) =>
            {
                _infiniteAmmo = state;
                ConfigManager.SetBool("Weapons", "InfiniteAmmo", _infiniteAmmo);
                _lastAmmoRefreshTime = 0;
                _lastForcedReloadTime = 0;
            };
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

        private static string GetInventorySlot(int modelHash)
        {
            int michael = Function.Call<int>(Hash.GET_HASH_KEY, "player_zero");
            int franklin = Function.Call<int>(Hash.GET_HASH_KEY, "player_one");
            int trevor = Function.Call<int>(Hash.GET_HASH_KEY, "player_two");
            if (modelHash == michael) return "SP0";
            if (modelHash == franklin) return "SP1";
            if (modelHash == trevor) return "SP2";
            return null;
        }

        private void LoadWeaponInventories()
        {
            if (_inventoryFileLoaded) return;
            _inventoryFileLoaded = true;
            try
            {
                string directory = Path.GetDirectoryName(WeaponInventoryPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                if (!File.Exists(WeaponInventoryPath)) return;

                string json = File.ReadAllText(WeaponInventoryPath);
                var loaded = new JavaScriptSerializer().Deserialize<Dictionary<string, WeaponInventoryData>>(json);
                if (loaded != null)
                {
                    foreach (var pair in loaded)
                    {
                        if (pair.Value != null &&
                            (pair.Key == "SP0" || pair.Key == "SP1" || pair.Key == "SP2"))
                            _savedInventories[pair.Key] = pair.Value;
                    }
                }
                _lastInventoryJson = json;
            }
            catch
            {
                // A damaged optional inventory file must never prevent the trainer from loading.
                _savedInventories.Clear();
                _lastInventoryJson = string.Empty;
            }
        }

        private bool SaveWeaponInventories()
        {
            try
            {
                string json = new JavaScriptSerializer().Serialize(_savedInventories);
                if (string.Equals(json, _lastInventoryJson, StringComparison.Ordinal)) return true;

                string directory = Path.GetDirectoryName(WeaponInventoryPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                string tempPath = WeaponInventoryPath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(WeaponInventoryPath))
                {
                    string backupPath = WeaponInventoryPath + ".bak";
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Replace(tempPath, WeaponInventoryPath, backupPath);
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                }
                else
                {
                    File.Move(tempPath, WeaponInventoryPath);
                }
                _lastInventoryJson = json;
                return true;
            }
            catch
            {
                try
                {
                    string tempPath = WeaponInventoryPath + ".tmp";
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { }
                return false;
            }
        }

        private WeaponInventoryData CaptureWeaponInventory(Ped player)
        {
            WeaponInventoryData snapshot = new WeaponInventoryData();
            snapshot.SelectedWeapon = Function.Call<uint>(Hash.GET_SELECTED_PED_WEAPON, player.Handle);
            HashSet<uint> seenWeapons = new HashSet<uint>();
            foreach (WeaponHash weaponEnum in Enum.GetValues(typeof(WeaponHash)))
            {
                uint weaponHash = (uint)weaponEnum;
                if (weaponHash == 0 || !seenWeapons.Add(weaponHash)) continue;
                if (!Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON, player.Handle, weaponHash, false))
                    continue;

                WeaponInventoryEntry entry = new WeaponInventoryEntry();
                entry.WeaponHash = weaponHash;
                entry.Ammo = Function.Call<int>(Hash.GET_AMMO_IN_PED_WEAPON, player.Handle, weaponHash);
                entry.Tint = Function.Call<int>(Hash.GET_PED_WEAPON_TINT_INDEX, player.Handle, weaponHash);
                using (OutputArgument clip = new OutputArgument())
                {
                    entry.ClipAmmo = Function.Call<bool>(Hash.GET_AMMO_IN_CLIP, player.Handle, weaponHash, clip)
                        ? clip.GetResult<int>() : -1;
                }

                // The game normally persists weapon components itself. Keep this
                // snapshot focused on ownership, ammo and tint to avoid thousands of
                // component-probe natives during periodic inventory saves.
                snapshot.Weapons.Add(entry);
            }
            return snapshot;
        }

        private void RestoreWeaponInventory(Ped player, WeaponInventoryData snapshot)
        {
            if (snapshot == null || snapshot.Weapons == null) return;
            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                WeaponInventoryEntry entry = snapshot.Weapons[i];
                if (entry == null || entry.WeaponHash == 0) continue;
                uint weaponHash = entry.WeaponHash;
                bool hasWeapon = Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON, player.Handle, weaponHash, false);
                if (!hasWeapon)
                {
                    Function.Call(Hash.GIVE_WEAPON_TO_PED, player.Handle, weaponHash, Math.Max(0, entry.Ammo), false, false);
                    hasWeapon = Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON, player.Handle, weaponHash, false);
                }
                if (!hasWeapon) continue;

                Function.Call(Hash.SET_PED_WEAPON_TINT_INDEX, player.Handle, weaponHash, entry.Tint);
                int currentAmmo = Function.Call<int>(Hash.GET_AMMO_IN_PED_WEAPON, player.Handle, weaponHash);
                if (entry.Ammo > currentAmmo)
                    Function.Call(Hash.SET_PED_AMMO, player.Handle, weaponHash, entry.Ammo, false);

                uint ammoType = Function.Call<uint>((Hash)0x7FEAD38B326B9F74, player.Handle, weaponHash);
                if (ammoType != 0 && entry.Ammo > Function.Call<int>(Hash.GET_PED_AMMO_BY_TYPE, player.Handle, ammoType))
                    Function.Call(Hash.SET_PED_AMMO_BY_TYPE, player.Handle, ammoType, entry.Ammo);

                if (entry.ClipAmmo >= 0)
                    Function.Call(Hash.SET_AMMO_IN_CLIP, player.Handle, weaponHash, entry.ClipAmmo);
            }

            if (snapshot.SelectedWeapon != 0 &&
                Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON, player.Handle, snapshot.SelectedWeapon, false))
                Function.Call(Hash.SET_CURRENT_PED_WEAPON, player.Handle, snapshot.SelectedWeapon, true);
        }

        private void MaintainStoryModeWeaponInventory(Ped player, int now)
        {
            if (!_inventoryFileLoaded) LoadWeaponInventories();
            int modelHash = Function.Call<int>(Hash.GET_ENTITY_MODEL, player.Handle);
            string slot = GetInventorySlot(modelHash);
            if (slot == null) return;

            if (player.Handle != _lastInventoryPedHandle || modelHash != _lastInventoryModelHash)
            {
                _lastInventoryPedHandle = player.Handle;
                _lastInventoryModelHash = modelHash;
                _inventoryRestorePending = true;
                _restoreInventoryAt = now + 2500;
                _lastInventorySnapshotTime = now;
            }

            if (_inventoryRestorePending && now >= _restoreInventoryAt)
            {
                WeaponInventoryData saved;
                if (_savedInventories.TryGetValue(slot, out saved))
                    RestoreWeaponInventory(player, saved);
                _inventoryRestorePending = false;
                _lastInventorySnapshotTime = now;
                return;
            }

            if (!_inventoryRestorePending && now - _lastInventorySnapshotTime >= 2000)
            {
                _lastInventorySnapshotTime = now;
                _savedInventories[slot] = CaptureWeaponInventory(player);
                SaveWeaponInventories();
            }
        }

        public void ReloadSettings()
        {
            _infiniteAmmo = ConfigManager.GetBool("Weapons", "InfiniteAmmo", false);
            _explosiveAmmo = ConfigManager.GetBool("Weapons", "ExplosiveAmmo", false);
            _fireAmmo = ConfigManager.GetBool("Weapons", "FireAmmo", false);
            _superDamage = ConfigManager.GetBool("Weapons", "SuperDamage", false);
            Function.Call(Hash.SET_PLAYER_WEAPON_DAMAGE_MODIFIER, Game.Player.Handle, _superDamage ? 100.0f : 1.0f);
            _teleportGun = ConfigManager.GetBool("Weapons", "TeleportGun", false);
            _forceGun = ConfigManager.GetBool("Weapons", "ForceGun", false);
            _rpgAmmo = ConfigManager.GetBool("Weapons", "RpgAmmo", false);
            _infiniteClip = ConfigManager.GetBool("Weapons", "InfiniteClip", false);
            _lastAmmoRefreshTime = 0;
            _lastForcedReloadTime = 0;
        }

        private void RefreshInfiniteAmmoForCurrentWeapon(Ped player, int now)
        {
            uint weaponHash = Function.Call<uint>(Hash.GET_SELECTED_PED_WEAPON, player.Handle);
            if (weaponHash == 0) return;

            // Mk II magazines use the currently selected ammo-type hash, not the
            // weapon's default ammo pool. This also works for ordinary weapons.
            // GET_PED_AMMO_TYPE_FROM_WEAPON (native hash used because SHVDN2 2.10.13 omits its enum member).
            uint ammoTypeHash = Function.Call<uint>((Hash)0x7FEAD38B326B9F74, player.Handle, weaponHash);
            if (ammoTypeHash == 0) return;

            int maxAmmo = 0;
            using (OutputArgument maxAmmoArg = new OutputArgument())
            {
                // GET_MAX_AMMO_BY_TYPE(Ped, Hash, int*) is available in GTA V
                // build 1.0.2245.0; use its native hash for SHVDN2 compatibility.
                bool hasMaxAmmo = Function.Call<bool>((Hash)0x585847C5E4E11709, player.Handle, ammoTypeHash, maxAmmoArg);
                if (hasMaxAmmo) maxAmmo = maxAmmoArg.GetResult<int>();
            }
            if (maxAmmo <= 0) maxAmmo = 9999;

            int currentAmmo = Function.Call<int>(Hash.GET_PED_AMMO_BY_TYPE, player.Handle, ammoTypeHash);
            if (currentAmmo < maxAmmo)
            {
                // ADD creates the ammo-type pool when it does not exist yet;
                // SET then keeps an existing pool at its supported maximum.
                if (currentAmmo <= 0)
                    Function.Call((Hash)0x2472622CE1F2D45F, player.Handle, ammoTypeHash, maxAmmo); // ADD_PED_AMMO_BY_TYPE
                else
                    Function.Call(Hash.SET_PED_AMMO_BY_TYPE, player.Handle, ammoTypeHash, maxAmmo);
            }

            // Mk II clip counts cannot reliably be repaired by setting the clip
            // directly. Force a real reload and complete it immediately instead.
            if (!_infiniteClip && now - _lastForcedReloadTime >= 1000 &&
                !Function.Call<bool>(Hash.IS_PED_RELOADING, player.Handle))
            {
                using (OutputArgument clipAmmoArg = new OutputArgument())
                {
                    bool hasClipAmmo = Function.Call<bool>(Hash.GET_AMMO_IN_CLIP, player.Handle, weaponHash, clipAmmoArg);
                    if (hasClipAmmo && clipAmmoArg.GetResult<int>() <= 0)
                    {
                        _lastForcedReloadTime = now;
                        if (Function.Call<bool>(Hash.MAKE_PED_RELOAD, player.Handle))
                            Function.Call((Hash)0x8C0D57EA686FAD87, player.Handle); // REFILL_AMMO_INSTANTLY
                    }
                }
            }
        }

        public void OnTick()
        {
            Ped inventoryPlayer = Game.Player.Character;
            if (inventoryPlayer != null && inventoryPlayer.Exists())
                MaintainStoryModeWeaponInventory(inventoryPlayer, Game.GameTime);

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
                RefreshInfiniteAmmoForCurrentWeapon(player, now);
            }

            // These natives are frame-scoped and must remain in the frame tick.
            if (_explosiveAmmo) Game.Player.SetExplosiveAmmoThisFrame();
            if (_fireAmmo) Game.Player.SetFireAmmoThisFrame();

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

    public class WeaponInventoryData
    {
        public uint SelectedWeapon;
        public List<WeaponInventoryEntry> Weapons = new List<WeaponInventoryEntry>();
    }

    public class WeaponInventoryEntry
    {
        public uint WeaponHash;
        public int Ammo;
        public int ClipAmmo;
        public int Tint;
        public List<uint> Components = new List<uint>();
    }
}
