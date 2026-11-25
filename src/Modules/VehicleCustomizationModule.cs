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

        public void ReloadSettings() { }

        public void OnTick() { }
    }
}
