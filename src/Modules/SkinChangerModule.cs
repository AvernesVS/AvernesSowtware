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

        public void ReloadSettings() { }

        public void OnTick() { }
    }
}
