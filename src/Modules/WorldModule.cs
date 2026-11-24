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
    public class WorldModule : IAvernesModule
    {
        public string Name { get { return "World"; } }

        private bool _freezeTime = ConfigManager.GetBool("World", "FreezeTime", false);
        private bool _moonGravity = ConfigManager.GetBool("World", "MoonGravity", false);

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

                ConfigManager.SetBool("World", "FreezeTime", _freezeTime);
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

                ConfigManager.SetBool("World", "MoonGravity", _moonGravity);
                World.GravityLevel = _moonGravity ? 1 : 0;
            };
            worldMenu.AddItem(moonItem);

            var restoreWorldItem = new UIMenuItem("Restore World Defaults", "Resets slow motion, clock pause, gravity and visual filters.");
            restoreWorldItem.Activated += (sender, selected) => RestoreWorldDefaults();
            worldMenu.AddItem(restoreWorldItem);

            // Apply persisted world toggles on startup.
            if (_freezeTime) Function.Call(Hash.PAUSE_CLOCK, true);
            if (_moonGravity) World.GravityLevel = 1;
        }

        private void RestoreWorldDefaults()
        {
            Game.TimeScale = 1.0f;
            Function.Call(Hash.SET_TIME_SCALE, 1.0f);
            Function.Call(Hash.PAUSE_CLOCK, false);
            World.GravityLevel = 0;
            Function.Call(Hash.CLEAR_TIMECYCLE_MODIFIER);
            _freezeTime = false;
            _moonGravity = false;
            ConfigManager.SetBool("World", "FreezeTime", false);
            ConfigManager.SetBool("World", "MoonGravity", false);
            UI.Notify("~g~World effects restored to normal.");
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
