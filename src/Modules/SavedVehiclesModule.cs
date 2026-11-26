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
        private static string StoragePath { get { return Path.Combine(ConfigManager.UserDataDirectoryPath, "SavedVehicles.json"); } }
        private readonly List<SavedVehicleData> _saved = new List<SavedVehicleData>();
        private UIMenu _savedMenu;
        private MenuPool _menuPool;
        private JavaScriptSerializer _serializer;
        private bool _hasLoadedFromDisk;
        private UIMenuItem _saveCurrentVehicleItem;

        public string Name { get { return "Saved Vehicles"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            _menuPool = menuPool;
            if (_serializer == null)
            {
                _serializer = new JavaScriptSerializer();
                _serializer.MaxJsonLength = int.MaxValue;
            }
            if (!_hasLoadedFromDisk) LoadFromDisk();

            _savedMenu = menuPool.AddSubMenu(mainMenu,
                Localization.Get("General", "SubmenuSavedVehicles", "Saved Vehicles"));

            _saveCurrentVehicleItem = new UIMenuItem(
                Localization.Get("Vehicle", "SaveCurrentVehicle", "Save Current Vehicle"),
                Localization.Get("Vehicle", "SaveCurrentVehicleDesc", "Save the vehicle you are driving, including installed tuning and visual customization."));
            _saveCurrentVehicleItem.Activated += (sender, selected) => SaveCurrentVehicle();
            RefreshSavedVehicleMenu();
        }

        private void LoadFromDisk()
        {
            try
            {
                // Menu rebuilds reuse this module instance; replace, do not append, cached records.
                _saved.Clear();
                string directory = Path.GetDirectoryName(StoragePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                if (!File.Exists(StoragePath))
                {
                    File.WriteAllText(StoragePath, "[]", Encoding.UTF8);
                    _hasLoadedFromDisk = true;
                    return;
                }

                string json = File.ReadAllText(StoragePath, Encoding.UTF8);
                List<SavedVehicleData> loaded = _serializer.Deserialize<List<SavedVehicleData>>(json);
                if (loaded == null)
                {
                    _hasLoadedFromDisk = true;
                    return;
                }
                bool normalized = false;
                for (int i = 0; i < loaded.Count; i++)
                {
                    SavedVehicleData item = loaded[i];
                    if (item == null || item.ModelHash == 0 || string.IsNullOrWhiteSpace(item.Name))
                    {
                        normalized = true;
                        continue;
                    }

                    string cleanName = item.Name.Trim();
                    if (!string.Equals(cleanName, item.Name, StringComparison.Ordinal))
                        normalized = true;
                    item.Name = MakeUniqueLoadedName(cleanName);
                    if (!string.Equals(item.Name, cleanName, StringComparison.Ordinal))
                        normalized = true;
                    _saved.Add(item);
                }
                SortSavedVehicles();
                _hasLoadedFromDisk = true;
                if (normalized) SaveToDisk();
            }
            catch
            {
                // Keep the trainer usable if the optional saved-car file is damaged.
                _saved.Clear();
                _hasLoadedFromDisk = false;
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
            if (string.IsNullOrWhiteSpace(name))
            {
                UI.Notify(Localization.Get("Vehicle", "SavedVehicleNameEmpty", "Vehicle name cannot be empty."));
                return;
            }
            name = name.Trim();
            if (ContainsSavedName(name))
            {
                UI.Notify(Localization.Get("Vehicle", "SavedVehicleNameDuplicate", "A saved vehicle with this name already exists."));
                return;
            }

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

            SortSavedVehicles();
            RefreshSavedVehicleMenu();
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

        private bool ContainsSavedName(string name)
        {
            for (int i = 0; i < _saved.Count; i++)
            {
                if (string.Equals(_saved[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private string MakeUniqueLoadedName(string name)
        {
            string candidate = name;
            int suffix = 2;
            while (ContainsSavedName(candidate))
            {
                string ending = " (" + suffix.ToString() + ")";
                int maxBaseLength = Math.Max(1, 30 - ending.Length);
                string baseName = name.Length > maxBaseLength ? name.Substring(0, maxBaseLength) : name;
                candidate = baseName + ending;
                suffix++;
            }
            return candidate;
        }

        private void SortSavedVehicles()
        {
            _saved.Sort(delegate(SavedVehicleData left, SavedVehicleData right)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            });
        }

        private void RefreshSavedVehicleMenu()
        {
            if (_savedMenu == null) return;
            _savedMenu.Clear();
            _savedMenu.AddItem(_saveCurrentVehicleItem);

            var infoItem = new UIMenuItem(
                Localization.Get("Vehicle", "SavedCars", "Saved cars: ") + _saved.Count,
                Localization.Get("Vehicle", "SavedCarsDesc", "Saved vehicles are stored separately in SavedVehicles.json."));
            infoItem.Enabled = false;
            _savedMenu.AddItem(infoItem);
            AddSavedVehicleItems();
        }

        private void AddSavedVehicleItems()
        {
            for (int i = 0; i < _saved.Count; i++)
                AddSavedVehicleItem(_saved[i], i + 1);
        }

        private void AddSavedVehicleItem(SavedVehicleData data, int number)
        {
            string label = number.ToString() + ". " + data.Name + "  [" + data.ModelHash.ToString("X8") + "]";
            var spawnItem = new UIMenuItem(label, Localization.Get("Vehicle", "SpawnSavedVehicleDesc", "Spawn this saved vehicle with its stored tuning and appearance."));
            spawnItem.Activated += (sender, selected) => SpawnSavedVehicle(data);
            _savedMenu.AddItem(spawnItem);

            var deleteItem = new UIMenuItem(
                string.Format(Localization.Get("Vehicle", "DeleteSavedVehicle", "Delete #{0} - {1}"), number, data.Name),
                Localization.Get("Vehicle", "DeleteSavedVehicleDesc", "Delete this saved vehicle. Type DELETE to confirm."));
            deleteItem.Activated += (sender, selected) => ConfirmDeleteSavedVehicle(data);
            _savedMenu.AddItem(deleteItem);
        }

        private void ConfirmDeleteSavedVehicle(SavedVehicleData data)
        {
            int index = _saved.IndexOf(data);
            if (index < 0) return;

            UI.Notify(Localization.Get("Vehicle", "DeleteSavedVehiclePrompt", "Type DELETE to confirm the deletion."));
            string confirmation = Game.GetUserInput(10);
            if (!string.Equals(confirmation == null ? string.Empty : confirmation.Trim(), "DELETE", StringComparison.OrdinalIgnoreCase))
            {
                UI.Notify(Localization.Get("Vehicle", "DeleteSavedVehicleCancelled", "Deletion cancelled."));
                return;
            }

            _saved.RemoveAt(index);
            SortSavedVehicles();
            if (!SaveToDisk())
            {
                _saved.Insert(index, data);
                SortSavedVehicles();
                UI.Notify(Localization.Get("Vehicle", "DeleteSavedVehicleFailed", "Could not update SavedVehicles.json. Vehicle was kept."));
                return;
            }

            RefreshSavedVehicleMenu();
            UI.Notify(Localization.Get("Vehicle", "SavedVehicleDeleted", "Saved vehicle deleted. The list has been renumbered."));
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
                VehicleModule.TrackSpawnedVehicle(vehicle, true);
                ApplyCustomization(vehicle, data);
                player.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                vehicle.PlaceOnGround();
                VehicleModule.ActivateVehicleRadio(vehicle);
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

        public void ReloadSettings() { }

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
