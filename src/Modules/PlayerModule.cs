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
    public class PlayerModule : IAvernesModule
    {
        public string Name { get { return "Player"; } }

        private static bool _isGodModeActive = ConfigManager.GetBool("Player", "GodMode", false);
        public static bool IsGodModeActive { get { return _isGodModeActive; } }
        private bool _neverWanted = ConfigManager.GetBool("Player", "NeverWanted", false);
        private bool _superJump = ConfigManager.GetBool("Player", "SuperJump", false);
        private bool _fastRun = ConfigManager.GetBool("Player", "FastRun", false);
        private bool _fastSwim = ConfigManager.GetBool("Player", "FastSwim", false);
        private bool _noRagdoll = ConfigManager.GetBool("Player", "NoRagdoll", false);
        private bool _unlimitedStamina = ConfigManager.GetBool("Player", "UnlimitedStamina", false);
        private bool _unlimitedOxygen = ConfigManager.GetBool("Player", "UnlimitedOxygen", false);
        private bool _infiniteSpecialAbility = ConfigManager.GetBool("Player", "InfiniteSpecialAbility", false);
        private static readonly string[] CharacterStatPrefixes = new string[] { "SP0_", "SP1_", "SP2_" };
        private static readonly string[] MaxSkillStatNames = new string[]
        {
            "STAMINA", "STRENGTH", "SHOOTING_ABILITY", "STEALTH_ABILITY",
            "FLYING_ABILITY", "WHEELIE_ABILITY", "LUNG_CAPACITY", "SPECIAL_ABILITY"
        };
        private bool _isInvisible = ConfigManager.GetBool("Player", "Invisibility", false);
        private bool _mobileRadio = ConfigManager.GetBool("Player", "MobileRadio", false);
        private int _lastMaintenanceTime = 0;
        private int _originalMaxWantedLevel = 5;
        private bool _neverWantedApplied = false;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            // Clear the legacy Cops Ignore state from older trainer versions.
            // Never Wanted is the supported police-related feature.
            Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, false);
            var playerMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuPlayer", "Player Options"));

            // God Mode
            var godItem = new UIMenuCheckboxItem(Localization.Get("Player", "GodMode", "God Mode (Invincibility)"), _isGodModeActive, Localization.Get("Player", "GodModeDesc", "Immune to all damage"));
            godItem.CheckboxEvent += (sender, state) =>
            {
                _isGodModeActive = state;

                ConfigManager.SetBool("Player", "GodMode", _isGodModeActive);
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

                ConfigManager.SetBool("Player", "NeverWanted", _neverWanted);
                if (_neverWanted) EnableNeverWanted();
                else DisableNeverWanted();
            };
            playerMenu.AddItem(neverWantedItem);
            if (_neverWanted) EnableNeverWanted();

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
            var radioItem = new UIMenuCheckboxItem(Localization.Get("Player", "MobileRadio", "Mobile Radio on Foot"), _mobileRadio, Localization.Get("Player", "MobileRadioDesc", "Allows listening to radio on foot"));
            radioItem.CheckboxEvent += (sender, state) =>
            {
                _mobileRadio = state;

                ConfigManager.SetBool("Player", "MobileRadio", _mobileRadio);
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

                ConfigManager.SetBool("Player", "Invisibility", _isInvisible);
                if (Game.Player.Character.Exists())
                {
                    Game.Player.Character.IsVisible = !_isInvisible;
                }
            };
            playerMenu.AddItem(invisItem);

            // Super Jump
            var jumpItem = new UIMenuCheckboxItem(Localization.Get("Player", "SuperJump", "Super Jump"), _superJump, Localization.Get("Player", "SuperJumpDesc", "Super-sized jumps"));
            jumpItem.CheckboxEvent += (sender, state) => { _superJump = state;
 ConfigManager.SetBool("Player", "SuperJump", _superJump); };
            playerMenu.AddItem(jumpItem);

            // Fast Run
            var runItem = new UIMenuCheckboxItem(Localization.Get("Player", "FastRun", "Super Speed"), _fastRun, Localization.Get("Player", "FastRunDesc", "Fast running"));
            runItem.CheckboxEvent += (sender, state) => { _fastRun = state;
 ConfigManager.SetBool("Player", "FastRun", _fastRun); };
            playerMenu.AddItem(runItem);

            // Fast Swim
            var swimItem = new UIMenuCheckboxItem(Localization.Get("Player", "FastSwim", "Fast Swim"), _fastSwim, Localization.Get("Player", "FastSwimDesc", "Fast swimming"));
            swimItem.CheckboxEvent += (sender, state) => { _fastSwim = state;
 ConfigManager.SetBool("Player", "FastSwim", _fastSwim); };
            playerMenu.AddItem(swimItem);

            // No Ragdoll
            var ragItem = new UIMenuCheckboxItem(Localization.Get("Player", "NoRagdoll", "No Ragdoll"), _noRagdoll, Localization.Get("Player", "NoRagdollDesc", "Never fall to the ground"));
            ragItem.CheckboxEvent += (sender, state) =>
            {
                _noRagdoll = state;

                ConfigManager.SetBool("Player", "NoRagdoll", _noRagdoll);
                if (Game.Player.Character.Exists())
                {
                    Game.Player.Character.CanRagdoll = !_noRagdoll;
                }
            };
            playerMenu.AddItem(ragItem);

            // Unlimited Stamina
            var stamItem = new UIMenuCheckboxItem(Localization.Get("Player", "UnlimitedStamina", "Infinite Stamina"), _unlimitedStamina, Localization.Get("Player", "UnlimitedStaminaDesc", "Never get tired"));
            stamItem.CheckboxEvent += (sender, state) => { _unlimitedStamina = state;
 ConfigManager.SetBool("Player", "UnlimitedStamina", _unlimitedStamina); };
            playerMenu.AddItem(stamItem);

            // Unlimited Oxygen
            var oxyItem = new UIMenuCheckboxItem(Localization.Get("Player", "UnlimitedOxygen", "Infinite Oxygen"), _unlimitedOxygen, Localization.Get("Player", "UnlimitedOxygenDesc", "Unlimited breathing"));
            oxyItem.CheckboxEvent += (sender, state) => { _unlimitedOxygen = state;
 ConfigManager.SetBool("Player", "UnlimitedOxygen", _unlimitedOxygen); };
            playerMenu.AddItem(oxyItem);

            // Infinite Special Ability
            var specialAbilityItem = new UIMenuCheckboxItem(
                Localization.Get("Player", "InfiniteSpecialAbility", "Infinite Special Ability"),
                _infiniteSpecialAbility,
                Localization.Get("Player", "InfiniteSpecialAbilityDesc", "Keep the special ability meter filled while enabled"));
            specialAbilityItem.CheckboxEvent += (sender, state) =>
            {
                _infiniteSpecialAbility = state;
                ConfigManager.SetBool("Player", "InfiniteSpecialAbility", _infiniteSpecialAbility);
                if (_infiniteSpecialAbility)
                    Function.Call(Hash.SPECIAL_ABILITY_FILL_METER, Game.Player, true);
            };
            playerMenu.AddItem(specialAbilityItem);

            // Max all protagonist skills
            var maxStatsItem = new UIMenuItem(
                Localization.Get("Player", "MaxAllStats", "Max All Character Stats"),
                Localization.Get("Player", "MaxAllStatsDesc", "Set all eight skills to 100% for Michael, Franklin and Trevor"));
            maxStatsItem.Activated += (sender, selected) => MaxAllCharacterStats();
            playerMenu.AddItem(maxStatsItem);

            // Money Submenu
            var moneyMenu = menuPool.AddSubMenu(playerMenu, Localization.Get("Player", "MoneyGenerator", "Money Generator"));
            AddCashOption(moneyMenu, "Add +$100,000", 100000);
            AddCashOption(moneyMenu, "Add +$1,000,000 (1 Million)", 1000000);
            AddCashOption(moneyMenu, "Add +$10,000,000 (10 Million)", 10000000);
            AddCashOption(moneyMenu, "Add +$100,000,000 (100 Million)", 100000000);

            var maxCashItem = new UIMenuItem(Localization.Get("Player", "MaximumMoney", "Maximum Money ($2,147,483,647)"), Localization.Get("Player", "MaximumMoneyDesc", "Sets your bank account to the maximum amount"));
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

        private void MaxAllCharacterStats()
        {
            bool allSucceeded = true;

            // Story Mode stat slots: SP0 = Michael, SP1 = Franklin, SP2 = Trevor.
            for (int characterIndex = 0; characterIndex < CharacterStatPrefixes.Length; characterIndex++)
            {
                for (int statIndex = 0; statIndex < MaxSkillStatNames.Length; statIndex++)
                {
                    string statName = CharacterStatPrefixes[characterIndex] + MaxSkillStatNames[statIndex];
                    int statHash = Function.Call<int>(Hash.GET_HASH_KEY, statName);
                    bool applied = Function.Call<bool>(Hash.STAT_SET_INT, statHash, 100, true);
                    if (!applied) allSucceeded = false;
                }
            }

            if (allSucceeded)
                UI.Notify("~g~All skills set to 100% for Michael, Franklin and Trevor!");
            else
                UI.Notify("~y~Some stats could not be updated. Check Story Mode and try again.");
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

        private void EnableNeverWanted()
        {
            if (!_neverWantedApplied)
            {
                _originalMaxWantedLevel = Function.Call<int>(Hash.GET_MAX_WANTED_LEVEL);
                if (_originalMaxWantedLevel < 1 || _originalMaxWantedLevel > 5)
                    _originalMaxWantedLevel = 5;
                _neverWantedApplied = true;
            }

            Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player, 0, false);
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
            Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, Game.Player);
        }

        private void DisableNeverWanted()
        {
            if (_neverWantedApplied)
            {
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, _originalMaxWantedLevel);
                _neverWantedApplied = false;
            }
            Game.Player.WantedLevel = 0;
            Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, Game.Player);
        }

        public void ReloadSettings()
        {
            Ped player = Game.Player.Character;
            bool wasGodMode = _isGodModeActive;
            bool wasNeverWanted = _neverWanted;
            bool wasInvisible = _isInvisible;

            _isGodModeActive = ConfigManager.GetBool("Player", "GodMode", false);
            _neverWanted = ConfigManager.GetBool("Player", "NeverWanted", false);
            _superJump = ConfigManager.GetBool("Player", "SuperJump", false);
            _fastRun = ConfigManager.GetBool("Player", "FastRun", false);
            _fastSwim = ConfigManager.GetBool("Player", "FastSwim", false);
            _noRagdoll = ConfigManager.GetBool("Player", "NoRagdoll", false);
            _unlimitedStamina = ConfigManager.GetBool("Player", "UnlimitedStamina", false);
            _unlimitedOxygen = ConfigManager.GetBool("Player", "UnlimitedOxygen", false);
            _infiniteSpecialAbility = ConfigManager.GetBool("Player", "InfiniteSpecialAbility", false);
            _isInvisible = ConfigManager.GetBool("Player", "Invisibility", false);
            _mobileRadio = ConfigManager.GetBool("Player", "MobileRadio", false);

            if (player != null && player.Exists())
            {
                if (wasGodMode != _isGodModeActive) player.IsInvincible = _isGodModeActive;
                if (wasInvisible != _isInvisible) player.IsVisible = !_isInvisible;
                if (!_noRagdoll) player.CanRagdoll = true;
                if (!_fastSwim) Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, Game.Player, 1.0f);
                if (!_unlimitedOxygen) Function.Call(Hash.SET_PED_MAX_TIME_UNDERWATER, player, 10.0f);
                if (!_mobileRadio) Function.Call(Hash.SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY, false);
            }

            if (wasNeverWanted && !_neverWanted) DisableNeverWanted();
            else if (_neverWanted) EnableNeverWanted();
        }

        public void OnTick()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            // These natives are explicitly frame-scoped and must run every frame.
            if (_superJump) Game.Player.SetSuperJumpThisFrame();
            if (_infiniteSpecialAbility) Function.Call(Hash.SPECIAL_ABILITY_FILL_METER, Game.Player, true);
            if (_fastRun) Game.Player.SetRunSpeedMultThisFrame(1.49f);

            // Never Wanted is enforced every frame to prevent transient stars and
            // the wanted-level flash between slower maintenance checks.
            if (_neverWanted)
            {
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);
                if (Game.Player.WantedLevel > 0)
                {
                    Game.Player.WantedLevel = 0;
                    Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, Game.Player);
                }
            }

            // Persistent states and maintenance do not need per-frame native calls.
            int now = Game.GameTime;
            if (now - _lastMaintenanceTime < 250) return;
            _lastMaintenanceTime = now;

            if (IsGodModeActive) player.IsInvincible = true;
            if (_isInvisible) player.IsVisible = false;
            if (_fastSwim) Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, Game.Player, 1.49f);
            if (_noRagdoll) player.CanRagdoll = false;
            if (_unlimitedStamina) Function.Call(Hash.RESET_PLAYER_STAMINA, Game.Player);
            if (_unlimitedOxygen) Function.Call(Hash.SET_PED_MAX_TIME_UNDERWATER, player, 9999.0f);
            if (_mobileRadio) Function.Call(Hash.SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY, true);
        }
    }
}
