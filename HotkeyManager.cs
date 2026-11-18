using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace AvernesSowtware
{
    public sealed partial class AvernesSowtwareScript
    {
        private readonly Dictionary<string, Keys> _hotkeyBindings = CreateDefaultHotkeyBindings();
        private string _hotkeyCaptureAction;

        private static Dictionary<string, Keys> CreateDefaultHotkeyBindings()
        {
            Dictionary<string, Keys> result = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);
            result["MenuToggle"] = Keys.F11;
            result["MenuUp"] = Keys.Up;
            result["MenuDown"] = Keys.Down;
            result["MenuSelect"] = Keys.Enter;
            result["MenuBack"] = Keys.Back;
            result["VehicleRockets"] = Keys.Add;
            return result;
        }

        private bool LoadHotkeys()
        {
            Dictionary<string, Keys> defaults = CreateDefaultHotkeyBindings();
            Dictionary<string, Keys> candidate = CreateDefaultHotkeyBindings();
            if (String.IsNullOrEmpty(_hotkeysPath)) return false;
            if (!File.Exists(_hotkeysPath))
            {
                foreach (KeyValuePair<string, Keys> pair in defaults) _hotkeyBindings[pair.Key] = pair.Value;
                SaveHotkeys();
                Log("Created default hotkeys.json with legacy-compatible F11/menu key bindings.");
                return true;
            }

            try
            {
                FileInfo info = new FileInfo(_hotkeysPath);
                if (info.Length <= 0 || info.Length > 32 * 1024)
                    throw new InvalidDataException("hotkeys.json is empty or exceeds 32 KB.");
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 32 * 1024;
                serializer.RecursionLimit = 8;
                object rootValue = serializer.DeserializeObject(File.ReadAllText(_hotkeysPath, Encoding.UTF8));
                IDictionary<string, object> root = rootValue as IDictionary<string, object>;
                if (root == null) throw new InvalidDataException("hotkeys.json root must be a JSON object.");
                object versionValue;
                if (!root.TryGetValue("schema_version", out versionValue) || Convert.ToInt32(versionValue) != 1)
                    throw new InvalidDataException("Unsupported hotkeys.json schema_version.");
                object bindingsValue;
                IDictionary<string, object> bindings = root.TryGetValue("bindings", out bindingsValue)
                    ? bindingsValue as IDictionary<string, object> : null;
                if (bindings == null) throw new InvalidDataException("hotkeys.json bindings must be an object.");

                string[] actions = new string[] { "MenuToggle", "MenuUp", "MenuDown", "MenuSelect", "MenuBack", "VehicleRockets" };
                for (int i = 0; i < actions.Length; i++)
                {
                    object keyValue;
                    if (!bindings.TryGetValue(actions[i], out keyValue)) continue;
                    Keys parsed;
                    if (!(keyValue is string) || !TryParseHotkey((string)keyValue, out parsed))
                    {
                        Log("Ignored invalid hotkey value for " + actions[i] + "; default " + defaults[actions[i]] + " retained.");
                        continue;
                    }
                    if (HasHotkeyConflictIn(candidate, actions[i], parsed))
                    {
                        Log("Ignored conflicting hotkey " + parsed + " for " + actions[i] + "; default binding retained.");
                        continue;
                    }
                    candidate[actions[i]] = parsed;
                }

                foreach (KeyValuePair<string, Keys> pair in candidate) _hotkeyBindings[pair.Key] = pair.Value;
                Log("Loaded hotkeys.json. Bindings=" + candidate.Count + ".");
                return true;
            }
            catch (Exception ex)
            {
                foreach (KeyValuePair<string, Keys> pair in defaults) _hotkeyBindings[pair.Key] = pair.Value;
                LogException("LoadHotkeys; defaults retained", ex);
                return false;
            }
        }

        private bool SaveHotkeys()
        {
            try
            {
                Dictionary<string, object> bindings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, Keys> pair in _hotkeyBindings)
                    bindings[pair.Key] = pair.Value.ToString();
                Dictionary<string, object> root = new Dictionary<string, object>();
                root["schema_version"] = 1;
                root["bindings"] = bindings;
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(root) + Environment.NewLine;
                string error;
                if (!TryAtomicWriteFile(_hotkeysPath, json, out error))
                {
                    LogFailure("SaveHotkeys", error);
                    Notify("Could not save hotkeys.json. Check the log.");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                LogException("SaveHotkeys", ex);
                return false;
            }
        }

        private static bool TryParseHotkey(string value, out Keys key)
        {
            key = Keys.None;
            if (String.IsNullOrWhiteSpace(value)) return false;
            try
            {
                object parsed = Enum.Parse(typeof(Keys), value.Trim(), true);
                key = (Keys)parsed;
                return IsAllowedHotkey(key);
            }
            catch { return false; }
        }

        private static bool IsAllowedHotkey(Keys key)
        {
            if (key == Keys.None || key == Keys.Escape || key == Keys.NumPad5 || key == Keys.ShiftKey ||
                key == Keys.ControlKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
                return false;
            int numeric = (int)key;
            return Enum.IsDefined(typeof(Keys), key) && numeric >= 0 && (key & Keys.Modifiers) == Keys.None;
        }

        private bool HasHotkeyConflict(string action, Keys key)
        {
            return HasHotkeyConflictIn(_hotkeyBindings, action, key);
        }

        private static bool HasHotkeyConflictIn(Dictionary<string, Keys> bindings, string action, Keys key)
        {
            foreach (KeyValuePair<string, Keys> pair in bindings)
            {
                if (String.Equals(pair.Key, action, StringComparison.OrdinalIgnoreCase)) continue;
                if (pair.Value == key) return true;
            }
            return false;
        }

        private bool TryAssignHotkey(string action, Keys key, out string reason)
        {
            reason = null;
            if (!_hotkeyBindings.ContainsKey(action)) { reason = "Unknown hotkey action."; return false; }
            if (!IsAllowedHotkey(key)) { reason = "This key is reserved or unsupported."; return false; }
            if (HasHotkeyConflict(action, key)) { reason = "This key is already assigned to another action."; return false; }
            Keys previous = _hotkeyBindings[action];
            _hotkeyBindings[action] = key;
            if (!SaveHotkeys())
            {
                _hotkeyBindings[action] = previous;
                reason = "The settings file could not be written.";
                return false;
            }
            return true;
        }

        private void BeginHotkeyCapture(string action)
        {
            if (!_hotkeyBindings.ContainsKey(action)) return;
            _hotkeyCaptureAction = action;
            Notify("Press one key for " + GetHotkeyActionLabel(action) + ". ESC cancels.");
        }

        private bool ProcessHotkeyCapture(KeyEventArgs e)
        {
            if (String.IsNullOrEmpty(_hotkeyCaptureAction)) return false;
            string action = _hotkeyCaptureAction;
            if (e.KeyCode == Keys.Escape)
            {
                _hotkeyCaptureAction = null;
                Notify("Hotkey capture cancelled.");
                return true;
            }
            if (e.Control || e.Shift || e.Alt)
            {
                Notify("Use a single key without modifiers. Press another key or ESC to cancel.");
                return true;
            }
            string reason;
            if (TryAssignHotkey(action, e.KeyCode, out reason))
            {
                _hotkeyCaptureAction = null;
                Notify(GetHotkeyActionLabel(action) + " hotkey set to " + e.KeyCode + ".");
            }
            else Notify(reason + " Press another key or ESC to cancel.");
            return true;
        }

        private bool IsHotkey(string action, KeyEventArgs e)
        {
            Keys key;
            return e != null && !e.Control && !e.Shift && !e.Alt &&
                _hotkeyBindings.TryGetValue(action, out key) && e.KeyCode == key;
        }

        private string GetHotkeyLabel(string action)
        {
            Keys key;
            return _hotkeyBindings.TryGetValue(action, out key) ? key.ToString() : "Not set";
        }

        private string GetHotkeyActionLabel(string action)
        {
            switch (action)
            {
                case "MenuToggle": return "Menu open/close";
                case "MenuUp": return "Menu up";
                case "MenuDown": return "Menu down";
                case "MenuSelect": return "Menu select";
                case "MenuBack": return "Menu back";
                case "VehicleRockets": return "Vehicle rockets";
                default: return action;
            }
        }

        private void ResetHotkeys()
        {
            Dictionary<string, Keys> defaults = CreateDefaultHotkeyBindings();
            foreach (KeyValuePair<string, Keys> pair in defaults) _hotkeyBindings[pair.Key] = pair.Value;
            _hotkeyCaptureAction = null;
            if (SaveHotkeys()) Notify("Default hotkeys restored: F11 / arrows / Enter / Backspace / Numpad +.");
        }

        private void ReloadJsonConfigurations()
        {
            bool hotkeys = LoadHotkeys();
            bool vehicles = LoadVehicleCatalog();
            bool addons = LoadAddonVehicles();
            try { LoadFavoriteLocations(); }
            catch (Exception ex) { LogException("Developer JSON reload favorites", ex); }
            try { LoadSavedVehicles(); }
            catch (Exception ex) { LogException("Developer JSON reload saved vehicles", ex); }
            RebuildFavoriteLocationsMenu();
            RebuildSavedVehiclesMenu();
            RebuildVehicleSpawnerMenu();
            Log("Developer JSON reload requested. Hotkeys=" + hotkeys + "; vehicleCatalog=" + vehicles + "; addons=" + addons + ".");
            Notify("JSON reload finished. Invalid files kept their previous valid in-memory data where supported.");
        }
    }
}
