using AvernesTreiner.Core;
using GTA;
using GTA.Math;
using GTA.Native;
using NativeUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace AvernesTreiner.Modules
{
    /// <summary>
    /// Manual personal vehicle catalog. Saves vehicle model and customization to
    /// scripts/AvernesTreiner/SavedVehicles.json; no per-frame or automatic disk writes.
    /// </summary>
    public class SavedVehiclesModule : IAvernesModule
    {
        private const string StoragePath = @"scripts\AvernesTreiner\SavedVehicles.json";
        private readonly List<SavedVehicleData> _saved = new List<SavedVehicleData>();
        private UIMenu _savedMenu;
        private MenuPool _menuPool;
        private JavaScriptSerializer _serializer;

        public string Name { get { return "Saved Vehicles"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            _menuPool = menuPool;
            _serializer = new JavaScriptSerializer();
            _serializer.MaxJsonLength = int.MaxValue;
            LoadFromDisk();

            _savedMenu = menuPool.AddSubMenu(mainMenu,
                Localization.Get("General", "SubmenuSavedVehicles", "Saved Vehicles"));

            var saveItem = new UIMenuItem(
                Localization.Get("Vehicle", "SaveCurrentVehicle", "Save Current Vehicle"),
                "Save the vehicle you are driving, including installed tuning and visual customization.");
            saveItem.Activated += (sender, selected) => SaveCurrentVehicle();
            _savedMenu.AddItem(saveItem);

            var infoItem = new UIMenuItem(
                "Saved cars: " + _saved.Count,
                "Saved vehicles are stored separately in SavedVehicles.json.");
            infoItem.Enabled = false;
            _savedMenu.AddItem(infoItem);

            AddSavedVehicleItems();
        }

        private void LoadFromDisk()
        {
            try
            {
                string directory = Path.GetDirectoryName(StoragePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                if (!File.Exists(StoragePath))
                {
                    File.WriteAllText(StoragePath, "[]", Encoding.UTF8);
                    return;
                }

                string json = File.ReadAllText(StoragePath, Encoding.UTF8);
                List<SavedVehicleData> loaded = _serializer.Deserialize<List<SavedVehicleData>>(json);
                if (loaded == null) return;
                for (int i = 0; i < loaded.Count; i++)
                {
                    if (loaded[i] != null && loaded[i].ModelHash != 0 &&
                        !string.IsNullOrWhiteSpace(loaded[i].Name))
                        _saved.Add(loaded[i]);
                }
            }
            catch
            {
                // Keep the trainer usable if the optional saved-car file is damaged.
                _saved.Clear();
            }
        }

        private bool SaveToDisk()
        {
            try
            {
                string directory = Path.GetDirectoryName(StoragePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                string json = _serializer.Serialize(_saved);
                string tempPath = StoragePath + ".tmp";
                File.WriteAllText(tempPath, json, Encoding.UTF8);
                if (File.Exists(StoragePath))
                {
                    string backupPath = StoragePath + ".bak";
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Replace(tempPath, StoragePath, backupPath);
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                }
                else
                {
                    File.Move(tempPath, StoragePath);
                }
                return true;
            }
            catch
            {
                try
                {
                    string tempPath = StoragePath + ".tmp";
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { }
                return false;
            }
        }

        private void SaveCurrentVehicle()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                UI.Notify("~r~Get inside the vehicle you want to save first.");
                return;
            }

            string name = Game.GetUserInput(30);
            if (string.IsNullOrWhiteSpace(name)) return;

            Vehicle vehicle = player.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists()) return;

            SavedVehicleData data;
            try
            {
                data = CaptureVehicle(vehicle, name.Trim());
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Could not read vehicle customization: " + ex.Message);
                return;
            }

            _saved.Add(data);
            if (!SaveToDisk())
            {
                _saved.RemoveAt(_saved.Count - 1);
                UI.Notify("~r~Could not save SavedVehicles.json.");
                return;
            }

            AddSavedVehicleItem(data);
            UI.Notify("~g~Saved vehicle: ~w~" + data.Name);
        }

        private SavedVehicleData CaptureVehicle(Vehicle vehicle, string name)
        {
            int handle = vehicle.Handle;
            SavedVehicleData data = new SavedVehicleData();
            data.Name = name;
            data.ModelHash = Function.Call<int>(Hash.GET_ENTITY_MODEL, handle);
            data.Mods = new int[50];
            data.ModVariations = new bool[50];
            data.ToggleMods = new bool[3];
            data.Extras = new bool[15];
            data.ExtraExists = new bool[15];

            Function.Call(Hash.SET_VEHICLE_MOD_KIT, handle, 0);
            for (int slot = 0; slot < data.Mods.Length; slot++)
            {
                data.Mods[slot] = Function.Call<int>(Hash.GET_VEHICLE_MOD, handle, slot);
                data.ModVariations[slot] = Function.Call<bool>(Hash.GET_VEHICLE_MOD_VARIATION, handle, slot);
            }

            int[] toggleSlots = new int[] { 18, 20, 22 };
            for (int i = 0; i < toggleSlots.Length; i++)
                data.ToggleMods[i] = Function.Call<bool>(Hash.IS_TOGGLE_MOD_ON, handle, toggleSlots[i]);

            OutputArgument primary = new OutputArgument();
            OutputArgument secondary = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_COLOURS, handle, primary, secondary);
            data.PrimaryColor = primary.GetResult<int>();
            data.SecondaryColor = secondary.GetResult<int>();

            OutputArgument pearlescent = new OutputArgument();
            OutputArgument wheel = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_EXTRA_COLOURS, handle, pearlescent, wheel);
            data.PearlescentColor = pearlescent.GetResult<int>();
            data.WheelColor = wheel.GetResult<int>();
            data.WheelType = Function.Call<int>(Hash.GET_VEHICLE_WHEEL_TYPE, handle);
            data.WindowTint = Function.Call<int>(Hash.GET_VEHICLE_WINDOW_TINT, handle);
            data.PlateStyle = Function.Call<int>(Hash.GET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, handle);
            data.PlateText = vehicle.NumberPlate;
            data.Livery = Function.Call<int>(Hash.GET_VEHICLE_LIVERY, handle);
            data.CanTiresBurst = Function.Call<bool>(Hash.GET_VEHICLE_TYRES_CAN_BURST, handle);

            data.NeonEnabled = new bool[4];
            for (int i = 0; i < 4; i++)
                data.NeonEnabled[i] = Function.Call<bool>(Hash._IS_VEHICLE_NEON_LIGHT_ENABLED, handle, i);
            OutputArgument neonR = new OutputArgument();
            OutputArgument neonG = new OutputArgument();
            OutputArgument neonB = new OutputArgument();
            Function.Call(Hash._GET_VEHICLE_NEON_LIGHTS_COLOUR, handle, neonR, neonG, neonB);
            data.NeonR = neonR.GetResult<int>();
            data.NeonG = neonG.GetResult<int>();
            data.NeonB = neonB.GetResult<int>();

            OutputArgument smokeR = new OutputArgument();
            OutputArgument smokeG = new OutputArgument();
            OutputArgument smokeB = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_TYRE_SMOKE_COLOR, handle, smokeR, smokeG, smokeB);
            data.SmokeR = smokeR.GetResult<int>();
            data.SmokeG = smokeG.GetResult<int>();
            data.SmokeB = smokeB.GetResult<int>();

            data.Extras = new bool[15];
            data.ExtraExists = new bool[15];
            for (int i = 0; i < 15; i++)
            {
                data.ExtraExists[i] = Function.Call<bool>(Hash.DOES_EXTRA_EXIST, handle, i);
                if (data.ExtraExists[i])
                    data.Extras[i] = Function.Call<bool>(Hash.IS_VEHICLE_EXTRA_TURNED_ON, handle, i);
            }
            return data;
        }

        private void AddSavedVehicleItems()
        {
            for (int i = 0; i < _saved.Count; i++)
                AddSavedVehicleItem(_saved[i]);
        }

        private void AddSavedVehicleItem(SavedVehicleData data)
        {
            string label = data.Name + "  [" + data.ModelHash.ToString("X8") + "]";
            var item = new UIMenuItem(label, "Spawn this saved vehicle with its stored tuning and appearance.");
            item.Activated += (sender, selected) => SpawnSavedVehicle(data);
            _savedMenu.AddItem(item);
        }

        private void SpawnSavedVehicle(SavedVehicleData data)
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                UI.Notify("~r~Player not found.");
                return;
            }

            Model model = new Model(data.ModelHash);
            try
            {
                if (!model.IsInCdImage || !model.IsValid || !model.Request(3000) || !model.IsLoaded)
                {
                    UI.Notify("~r~Saved vehicle model is unavailable in this game installation.");
                    return;
                }

                Vector3 position = player.Position + player.ForwardVector * 4.0f;
                Vehicle vehicle = World.CreateVehicle(model, position, player.Heading);
                if (vehicle == null || !vehicle.Exists())
                {
                    UI.Notify("~r~Could not create saved vehicle.");
                    return;
                }

                Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, vehicle.Handle, true, true);
                vehicle.IsPersistent = true;
                ApplyCustomization(vehicle, data);
                player.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                vehicle.PlaceOnGround();
                UI.Notify("~g~Saved vehicle loaded: ~w~" + data.Name);
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Could not load saved vehicle: " + ex.Message);
            }
            finally
            {
                try { model.MarkAsNoLongerNeeded(); } catch { }
            }
        }

        private void ApplyCustomization(Vehicle vehicle, SavedVehicleData data)
        {
            int handle = vehicle.Handle;
            Function.Call(Hash.SET_VEHICLE_MOD_KIT, handle, 0);

            if (data.Mods != null)
            {
                int length = Math.Min(data.Mods.Length, 50);
                for (int slot = 0; slot < length; slot++)
                {
                    int mod = data.Mods[slot];
                    if (mod >= 0)
                        Function.Call(Hash.SET_VEHICLE_MOD, handle, slot, mod,
                            data.ModVariations != null && slot < data.ModVariations.Length ? data.ModVariations[slot] : false);
                }
            }

            Function.Call(Hash.SET_VEHICLE_WHEEL_TYPE, handle, data.WheelType);
            Function.Call(Hash.SET_VEHICLE_COLOURS, handle, data.PrimaryColor, data.SecondaryColor);
            Function.Call(Hash.SET_VEHICLE_EXTRA_COLOURS, handle, data.PearlescentColor, data.WheelColor);
            Function.Call(Hash.SET_VEHICLE_WINDOW_TINT, handle, data.WindowTint);
            Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, handle, data.PlateStyle);
            if (!string.IsNullOrEmpty(data.PlateText))
                Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT, handle, data.PlateText);
            if (data.Livery >= 0)
                Function.Call(Hash.SET_VEHICLE_LIVERY, handle, data.Livery);
            Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, handle, data.CanTiresBurst);

            int[] toggleSlots = new int[] { 18, 20, 22 };
            for (int i = 0; i < toggleSlots.Length; i++)
                Function.Call(Hash.TOGGLE_VEHICLE_MOD, handle, toggleSlots[i], data.ToggleMods != null && i < data.ToggleMods.Length ? data.ToggleMods[i] : false);

            Function.Call(Hash._SET_VEHICLE_NEON_LIGHTS_COLOUR, handle, data.NeonR, data.NeonG, data.NeonB);
            if (data.NeonEnabled != null)
            {
                for (int i = 0; i < Math.Min(4, data.NeonEnabled.Length); i++)
                    Function.Call(Hash._SET_VEHICLE_NEON_LIGHT_ENABLED, handle, i, data.NeonEnabled[i]);
            }
            Function.Call(Hash.SET_VEHICLE_TYRE_SMOKE_COLOR, handle, data.SmokeR, data.SmokeG, data.SmokeB);

            if (data.ExtraExists != null && data.Extras != null)
            {
                for (int i = 0; i < Math.Min(15, data.ExtraExists.Length); i++)
                {
                    if (data.ExtraExists[i])
                        Function.Call(Hash.SET_VEHICLE_EXTRA, handle, i, !data.Extras[i]);
                }
            }
        }

        public void OnTick() { }
    }

    public class SavedVehicleData
    {
        public string Name;
        public int ModelHash;
        public int[] Mods;
        public bool[] ModVariations;
        public bool[] ToggleMods;
        public int PrimaryColor;
        public int SecondaryColor;
        public int PearlescentColor;
        public int WheelColor;
        public int WheelType;
        public int WindowTint;
        public int PlateStyle;
        public string PlateText;
        public int Livery;
        public bool CanTiresBurst;
        public bool[] NeonEnabled;
        public int NeonR;
        public int NeonG;
        public int NeonB;
        public int SmokeR;
        public int SmokeG;
        public int SmokeB;
        public bool[] Extras;
        public bool[] ExtraExists;
    }
}
