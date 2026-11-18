using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using GTA;
using GTA.Native;

namespace AvernesSowtware
{
    public sealed partial class AvernesSowtwareScript
    {
        private readonly List<AddonVehicle> _combinedVehicleCatalog = new List<AddonVehicle>();
        private bool _combinedVehicleCatalogDirty = true;

        private bool LoadVehicleCatalog()
        {
            // Merge every valid main-catalog copy. Installers can keep writable data in
            // AppData while the game-root scripts folder still contains a newer catalog.
            // First source wins for duplicate models; no valid source is discarded.
            string installedData = Path.Combine(_scriptsDirectory, "AvernesSowtware");
            string[] candidates = new string[]
            {
                _vehicleCatalogPath,
                Path.Combine(_vehiclesDirectory, "vehicles_builtin.json"),
                Path.Combine(_vehiclesDirectory, "vehicle_catalog.json"),
                Path.Combine(_vehiclesDirectory, "vehicle_catalog_builtin.json"),
                Path.Combine(_vehiclesDirectory, "vehicle_catalog"),
                _legacyVehicleCatalogPath,
                Path.Combine(_dataDirectory, "vehicles_builtin.json"),
                Path.Combine(_dataDirectory, "Vehicles", "vehicle_catalog.json"),
                Path.Combine(installedData, "Vehicles", "vehicles.json"),
                Path.Combine(installedData, "Vehicles", "vehicle_catalog.json"),
                Path.Combine(installedData, "vehicle_catalog.json"),
                Path.Combine(installedData, "vehicles_builtin.json"),
                Path.Combine(_legacyDataDirectory, "Vehicles", "vehicles.json"),
                Path.Combine(_legacyDataDirectory, "Vehicles", "vehicle_catalog.json"),
                Path.Combine(_legacyDataDirectory, "vehicle_catalog.json"),
                Path.Combine(_dataDirectory, "vehicles.json"),
                Path.Combine(_scriptsDirectory, "vehicle_catalog.json"),
                Path.Combine(_scriptsDirectory, "Vehicles", "vehicles.json"),
                Path.Combine(_scriptsDirectory, "Vehicles", "vehicle_catalog.json"),
                Path.Combine(_legacyDataDirectory, "vehicle_catalog"),
                Path.Combine(_legacyDataDirectory, "vehicles")
            };

            bool foundAny = false;
            Exception lastError = null;
            int validSources = 0;
            int sourceRecords = 0;
            int duplicateRecords = 0;
            List<AddonVehicle> merged = new List<AddonVehicle>();
            HashSet<string> seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (String.IsNullOrEmpty(candidate) || !checkedPaths.Add(candidate)) continue;
                if (!File.Exists(candidate)) continue;
                foundAny = true;

                try
                {
                    FileInfo info = new FileInfo(candidate);
                    if (info.Length <= 0) throw new InvalidDataException("Vehicle database is empty.");
                    if (info.Length > 2 * 1024 * 1024)
                        throw new InvalidDataException("Vehicle database exceeds the 2 MB safety limit.");

                    string json = File.ReadAllText(candidate);
                    List<AddonVehicle> parsed = DeserializeVehicleEntriesWithSchema(json, candidate, 2 * 1024 * 1024);
                    if (parsed.Count > 5000) throw new InvalidDataException("Vehicle database contains more than 5000 entries.");

                    List<AddonVehicle> validated = NormalizeVehicleEntries(parsed);
                    if (validated.Count == 0)
                        throw new InvalidDataException("Vehicle database contains no valid vehicle model names.");

                    validSources++;
                    sourceRecords += validated.Count;
                    int addedFromSource = 0;
                    for (int entryIndex = 0; entryIndex < validated.Count; entryIndex++)
                    {
                        AddonVehicle vehicle = validated[entryIndex];
                        if (vehicle == null || String.IsNullOrWhiteSpace(vehicle.model)) continue;
                        if (!seenModels.Add(vehicle.model))
                        {
                            duplicateRecords++;
                            continue;
                        }
                        merged.Add(vehicle);
                        addedFromSource++;
                    }
                    Log("Read vehicle catalog source: " + candidate + "; valid=" + validated.Count + "; new=" + addedFromSource + ".");
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    LogException("LoadVehicleCatalog candidate " + candidate, ex);
                }
            }

            if (merged.Count > 0)
            {
                _vehicleCatalog.Clear();
                _vehicleCatalog.AddRange(merged);
                _combinedVehicleCatalogDirty = true;
                RebuildVehicleSpawnerMenu();
                Log("Merged vehicle catalogs: sources=" + validSources + "; source records=" + sourceRecords +
                    "; unique main vehicles=" + _vehicleCatalog.Count + "; duplicate records skipped=" + duplicateRecords + ".");
                if (lastError != null) Log("One or more vehicle catalog sources were invalid; valid sources were still merged.");
                Notify("Vehicle database loaded: " + _vehicleCatalog.Count + " unique models.");
                return true;
            }

            if (!foundAny)
            {
                LogFailure("LoadVehicleCatalog", "No candidate vehicle database file exists. Checked: " + String.Join("; ", candidates));
                Notify("AvernesSowtware vehicle database was not found. Run BUILD_RELEASE.bat / Install_AvernesSowtware.bat or check scripts\\AvernesSowtware\\Vehicles.");
            }
            else
            {
                Log("Vehicle database candidates existed, but none passed validation.");
                if (lastError != null) LogException("LoadVehicleCatalog: all candidates failed", lastError);
                Notify("Vehicle database reload failed. Previous valid list was retained; check scripts/AvernesSowtware/AvernesSowtware.log.");
            }
            return false;
        }
        private bool LoadAddonVehicles()
        {
            // Merge every valid source instead of stopping at the first one. The active
            // addons.json may contain user entries from an older install; the bundled
            // addons_builtin.json must still contribute its verified entries. Earlier
            // candidates have priority when the same model appears more than once.
            string installedData = Path.Combine(_scriptsDirectory, "AvernesSowtware");
            string[] candidates = new string[]
            {
                _addonPath,
                Path.Combine(_dataDirectory, "addons"),
                Path.Combine(_dataDirectory, "addons_builtin.json"),
                Path.Combine(installedData, "addons.json"),
                Path.Combine(installedData, "addons_builtin.json"),
                Path.Combine(installedData, "addons"),
                Path.Combine(_scriptsDirectory, "addons.json"),
                Path.Combine(_scriptsDirectory, "addons_builtin.json"),
                Path.Combine(_legacyDataDirectory, "addons.json")
            };

            bool foundAny = false;
            Exception lastError = null;
            int validSources = 0;
            int sourceRecords = 0;
            int duplicateRecords = 0;
            List<AddonVehicle> merged = new List<AddonVehicle>();
            HashSet<string> seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < candidates.Length; i++)
            {
                string addonReadPath = candidates[i];
                if (String.IsNullOrWhiteSpace(addonReadPath) || !checkedPaths.Add(addonReadPath)) continue;
                if (!File.Exists(addonReadPath)) continue;
                foundAny = true;

                try
                {
                    FileInfo info = new FileInfo(addonReadPath);
                    if (info.Length <= 0) throw new InvalidDataException("addons.json is empty.");
                    if (info.Length > 1024 * 1024)
                        throw new InvalidDataException("addons.json exceeds the 1 MB safety limit.");

                    string json = File.ReadAllText(addonReadPath);
                    List<AddonVehicle> parsed = DeserializeVehicleEntriesWithSchema(json, addonReadPath, 1024 * 1024);
                    if (parsed.Count > 2000) throw new InvalidDataException("addons.json contains more than 2000 entries.");

                    List<AddonVehicle> validated = NormalizeVehicleEntries(parsed);
                    if (validated.Count == 0)
                        throw new InvalidDataException("No valid vehicle models were found in addons source: " + addonReadPath);

                    validSources++;
                    sourceRecords += validated.Count;
                    int addedFromSource = 0;
                    for (int entryIndex = 0; entryIndex < validated.Count; entryIndex++)
                    {
                        AddonVehicle vehicle = validated[entryIndex];
                        if (vehicle == null || String.IsNullOrWhiteSpace(vehicle.model)) continue;
                        if (!seenModels.Add(vehicle.model))
                        {
                            duplicateRecords++;
                            continue;
                        }
                        merged.Add(vehicle);
                        addedFromSource++;
                    }

                    Log("Read add-on source: " + addonReadPath + "; valid=" + validated.Count +
                        "; new=" + addedFromSource + "; duplicates=" + (validated.Count - addedFromSource) + ".");
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    // A damaged user file must not prevent the bundled list or a legacy
                    // fallback from loading. Keep trying every remaining candidate.
                    LogException("LoadAddonVehicles candidate " + addonReadPath, ex);
                }
            }

            if (merged.Count > 0)
            {
                // Commit only after all candidates have been considered. If every source
                // fails, the previously working add-on list remains untouched.
                _addonVehicles.Clear();
                _addonVehicles.AddRange(merged);
                _combinedVehicleCatalogDirty = true;
                if (_addonIndex >= _addonVehicles.Count) _addonIndex = 0;
                Log("Merged add-on databases: sources=" + validSources + "; source records=" +
                    sourceRecords + "; unique add-ons=" + _addonVehicles.Count +
                    "; duplicate records skipped=" + duplicateRecords + ".");
                if (lastError != null)
                    Log("One or more add-on sources were invalid; valid sources were still merged. See preceding exception entries.");
                RebuildVehicleSpawnerMenu();
                return true;
            }

            if (!foundAny)
                Log("No add-on list found. Checked: " + String.Join("; ", candidates));
            else if (lastError != null)
                LogException("LoadAddonVehicles: all available sources failed", lastError);

            // Do not erase a previously loaded list on a transient read/JSON failure.
            Notify("Add-on vehicle lists could not be loaded. Previous valid list was retained.");
            RebuildVehicleSpawnerMenu();
            return false;
        }
        private List<AddonVehicle> DeserializeVehicleEntriesWithSchema(string json, string sourcePath, int maximumLength)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = maximumLength;
            serializer.RecursionLimit = 16;
            object rootValue = serializer.DeserializeObject(json);
            IList rows = rootValue as IList;
            if (rows == null) throw new InvalidDataException("Vehicle JSON root must be an array: " + sourcePath);
            if (rows.Count > 5000) throw new InvalidDataException("Vehicle JSON contains more than 5000 entries: " + sourcePath);

            List<AddonVehicle> parsed = new List<AddonVehicle>();
            for (int i = 0; i < rows.Count; i++)
            {
                IDictionary<string, object> row = rows[i] as IDictionary<string, object>;
                if (row == null)
                {
                    Log("Skipped malformed vehicle JSON record at " + sourcePath + " index " + i + ": record must be an object.");
                    continue;
                }
                string model, name, id, category, source;
                if (!TryReadVehicleJsonString(row, "model", true, out model) ||
                    !TryReadVehicleJsonString(row, "name", false, out name) ||
                    !TryReadVehicleJsonString(row, "id", false, out id) ||
                    !TryReadVehicleJsonString(row, "category", false, out category) ||
                    !TryReadVehicleJsonString(row, "source", false, out source) || String.IsNullOrWhiteSpace(model))
                {
                    Log("Skipped malformed vehicle JSON record at " + sourcePath + " index " + i + ": invalid or missing field type/model.");
                    continue;
                }
                parsed.Add(new AddonVehicle { model = model, name = name, id = id, category = category, source = source });
            }
            return parsed;
        }

        private static bool TryReadVehicleJsonString(IDictionary<string, object> row, string key, bool required, out string value)
        {
            value = null;
            object raw = null;
            bool found = false;
            foreach (KeyValuePair<string, object> pair in row)
            {
                if (String.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    raw = pair.Value;
                    found = true;
                    break;
                }
            }
            if (!found) return !required;
            if (raw == null) return !required;
            if (!(raw is string)) return false;
            value = (string)raw;
            return !required || !String.IsNullOrWhiteSpace(value);
        }

        private List<AddonVehicle> NormalizeVehicleEntries(List<AddonVehicle> parsed)
        {
            List<AddonVehicle> result = new List<AddonVehicle>();
            HashSet<string> seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < parsed.Count; i++)
            {
                AddonVehicle item = parsed[i];
                if (item == null || String.IsNullOrWhiteSpace(item.model)) continue;
                string model = item.model.Trim();
                if (!IsSafeVehicleModelName(model))
                {
                    Log("Skipped unsafe/invalid vehicle model value at JSON index " + i + ".");
                    continue;
                }
                model = model.ToLowerInvariant();
                if (IsRetiredOrUnsupportedVehicleModel(model))
                {
                    Log("Skipped retired or unsupported vehicle model from catalog: " + model + ".");
                    continue;
                }
                if (!IsVehicleModelAvailableInCurrentBuild(model))
                {
                    Log("Skipped vehicle model unavailable in current GTA build: " + model + ".");
                    continue;
                }
                if (!seenModels.Add(model)) continue;

                // Only models validated against this installed Legacy build enter the
                // searchable/spawnable catalogue; this avoids known missing DLC entries.
                string name = SanitizeMenuText(String.IsNullOrWhiteSpace(item.name) ? model : item.name, 80, model);
                string category = NormalizeVehicleCategory(String.IsNullOrWhiteSpace(item.category) ? "Other" : item.category);
                if (String.Equals(model, "t20", StringComparison.OrdinalIgnoreCase)) category = "Super";
                else if (String.Equals(model, "fbi2", StringComparison.OrdinalIgnoreCase))
                {
                    name = "FBI SUV";
                    category = "Sedans / Coupes / SUVs";
                }
                else if (String.Equals(model, "fbi", StringComparison.OrdinalIgnoreCase))
                {
                    // Keep the FIB sedan/buffalo model in the emergency category;
                    // it is not the FBI SUV entry (model fbi2).
                    category = "Boats / Military / Emergency";
                }
                string source = SanitizeMenuText(String.IsNullOrWhiteSpace(item.source) ? "custom" : item.source, 48, "custom");
                result.Add(new AddonVehicle { name = name, model = model, id = FormatVehicleModelId(model), category = category, source = source });
            }
            return result;
        }
        private bool IsRetiredOrUnsupportedVehicleModel(string model)
        {
            return String.Equals(model, "arwing", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(model, "proto", StringComparison.OrdinalIgnoreCase) ||
                // LM87 is present in newer vehicle dumps, but not in the installed
                // GTA V Legacy 1.0.2245 game build reported for this project.
                String.Equals(model, "lm87", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsVehicleModelAvailableInCurrentBuild(string modelName)
        {
            try
            {
                Model model = new Model(modelName);
                return model.IsValid && model.IsInCdImage && model.IsVehicle;
            }
            catch (Exception ex)
            {
                LogException("Vehicle model availability check " + modelName, ex);
                return false;
            }
        }

        private string NormalizeVehicleCategory(string category)
        {
            string value = SanitizeMenuText(category, 48, "Other").Trim().ToLowerInvariant();
            switch (value)
            {
                case "sports classic":
                case "classic": return "Sports Classics";
                case "off road":
                case "offroad":
                case "off-road": return "Off-Road";
                case "helicopter":
                case "helicopters":
                case "planes":
                case "aircraft":
                case "aircraft & helicopters": return "Aircraft & Helicopters";
                case "sedan":
                case "sedans":
                case "coupes":
                case "suvs":
                case "sedans / coupes / suvs": return "Sedans / Coupes / SUVs";
                case "motorcycles":
                case "motorcycles & bicycles": return "Motorcycles & Bicycles";
                case "military":
                case "boats":
                case "boats / military / emergency": return "Boats / Military / Emergency";
                default: return SanitizeMenuText(category, 48, "Other");
            }
        }
        private string SanitizeMenuText(string value, int maxLength, string fallback)
        {
            if (value == null) return fallback;
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < value.Length && builder.Length < maxLength; i++)
            {
                char c = value[i];
                if (Char.IsControl(c) || c == '~') continue;
                builder.Append(c);
            }
            string cleaned = builder.ToString().Trim();
            return String.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
        }
        private bool IsSafeVehicleModelName(string modelName)
        {
            if (String.IsNullOrWhiteSpace(modelName) || modelName.Length > 64) return false;
            for (int i = 0; i < modelName.Length; i++)
            {
                char c = modelName[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '_')) return false;
            }
            return true;
        }
        private void RebuildVehicleSpawnerMenu()
        {
            List<MenuEntry> root;
            if (!_pages.TryGetValue("VehicleSpawner", out root)) return;

            for (int i = 0; i < _vehicleCategoryPageKeys.Count; i++)
                _pages.Remove(_vehicleCategoryPageKeys[i]);
            _vehicleCategoryPageKeys.Clear();
            root.Clear();

            RebuildCombinedVehicleCatalogCache();
            Dictionary<string, List<AddonVehicle>> grouped = new Dictionary<string, List<AddonVehicle>>(StringComparer.OrdinalIgnoreCase);
            List<string> categoryOrder = new List<string>();
            HashSet<string> seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Use the one normalized in-memory union for both spawner and search.
            AddVehiclesToGroups(_combinedVehicleCatalog, grouped, categoryOrder, seenModels);

            root.Add(ActionEntry("Search by name / Model ID", "VehicleSearch", null, OpenVehicleSearchKeyboard));
            if (categoryOrder.Count == 0)
            {
                root.Add(new MenuEntry("No vehicle entries loaded", "Check Vehicles\\vehicles.json and addons.json.", null, null, null));
            }
            else
            {
                for (int i = 0; i < categoryOrder.Count; i++)
                {
                    string category = categoryOrder[i];
                    string pageKey = "VehicleSpawner_Category_" + i.ToString();
                    List<AddonVehicle> vehicles = grouped[category];
                    _vehicleCategoryPageKeys.Add(pageKey);
                    root.Add(PageEntry(category + " (" + vehicles.Count + ")", pageKey,
                        "Spawn vehicles from this category."));

                    List<MenuEntry> page = NewPage(pageKey);
                    for (int j = 0; j < vehicles.Count; j++)
                    {
                        AddonVehicle capturedVehicle = vehicles[j];
                        string capturedModel = capturedVehicle.model;
                        string capturedName = capturedVehicle.name;
                        page.Add(ActionEntry(capturedName, capturedModel,
                            "Spawn model: " + capturedModel,
                            delegate { BeginVehicleSpawn(capturedModel, capturedName); }));
                    }
                    page.Add(BackEntry());
                }
            }
            root.Insert(0, new MenuEntry(seenModels.Count + " unique vehicles", "Master database + preserved addons.json. Search supports names and model hashes.", null, null, null));
            root.Add(ActionEntry("Reload vehicle database", "ReloadVehicleLists", null, delegate
            {
                bool catalogLoaded = LoadVehicleCatalog();
                bool addonsLoaded = LoadAddonVehicles();
                if (catalogLoaded || addonsLoaded)
                    Notify("Vehicle lists reloaded: " + GetCombinedVehicleList().Count + " unique models.");
                else
                    Notify("Vehicle lists reload failed; previous valid lists were kept. Check scripts/AvernesSowtware/AvernesSowtware.log.");
            }));
            root.Add(BackEntry());

            if (_currentPage != null && _currentPage.StartsWith("VehicleSpawner_Category_", StringComparison.OrdinalIgnoreCase))
            {
                _currentPage = "VehicleSpawner";
                _selectedIndex = 0;
                _scrollOffset = 0;
                _pageStack.Clear();
            }
            else if (String.Equals(_currentPage, "VehicleSpawner", StringComparison.OrdinalIgnoreCase))
            {
                _selectedIndex = 0;
                _scrollOffset = 0;
            }
            Log("Vehicle spawner rebuilt. Master database=" + _vehicleCatalog.Count +
                ", compatibility addons.json=" + _addonVehicles.Count +
                ", grouped unique models=" + seenModels.Count + ", categories=" + categoryOrder.Count);
        }
        private void OpenVehicleSearchKeyboard()
        {
            if (_vehicleSearchInputPending)
            {
                Notify("Vehicle search input is already open.");
                return;
            }

            try
            {
                _vehicleSearchInputPending = true;
                Function.Call((Hash)0x00DC833F2568DBF6UL, 0, "FMMC_KEY_TIP8", "", "", "", "", "", 64); // DISPLAY_ONSCREEN_KEYBOARD
                Notify("Enter a vehicle name, model code or model ID.");
            }
            catch (Exception ex)
            {
                _vehicleSearchInputPending = false;
                LogException("OpenVehicleSearchKeyboard", ex);
                NotifyError("Vehicle search");
            }
        }
        private void ProcessVehicleSearchInput()
        {
            int status = Function.Call<int>((Hash)0x0CF2B696BBF945AEUL); // UPDATE_ONSCREEN_KEYBOARD
            if (status == 0) return;

            _vehicleSearchInputPending = false;
            if (status != 1)
            {
                Notify("Vehicle search cancelled.");
                return;
            }

            string query = Function.Call<string>((Hash)0x8362B09B91893647UL); // GET_ONSCREEN_KEYBOARD_RESULT
            _vehicleSearchQuery = String.IsNullOrWhiteSpace(query) ? String.Empty : query.Trim();
            BuildVehicleSearchPage();
            if (!String.Equals(_currentPage, "VehicleSearch", StringComparison.OrdinalIgnoreCase))
                _pageStack.Push(_currentPage);
            _currentPage = "VehicleSearch";
            _selectedIndex = 0;
            _scrollOffset = 0;
        }
        private void BuildVehicleSearchPage()
        {
            List<MenuEntry> results = NewPage("VehicleSearch");
            string query = _vehicleSearchQuery == null ? String.Empty : _vehicleSearchQuery.Trim();
            results.Add(new MenuEntry("Query: " + (query.Length == 0 ? "(empty)" : query),
                "Matches display name, model code, decimal or hexadecimal JOAAT hash.", null, null, null));
            results.Add(ActionEntry("Search again", "SearchAgain", null, OpenVehicleSearchKeyboard));

            if (query.Length > 0)
            {
                List<AddonVehicle> allVehicles = GetCombinedVehicleList();
                string needle = query.ToLowerInvariant();
                string hashNeedle = needle.StartsWith("0x", StringComparison.Ordinal) ? needle.Substring(2) : needle;
                string compactNeedle = needle.Replace(" ", String.Empty).Replace("-", String.Empty).Replace("_", String.Empty);
                bool superDiamondAlias = compactNeedle == "superdiamond" || compactNeedle == "enussuperdiamond" || compactNeedle == "enusdiamond";
                int added = 0;
                for (int i = 0; i < allVehicles.Count; i++)
                {
                    AddonVehicle item = allVehicles[i];
                    string name = item.name == null ? String.Empty : item.name.ToLowerInvariant();
                    string model = item.model == null ? String.Empty : item.model.ToLowerInvariant();
                    string id = item.id == null ? String.Empty : item.id.ToLowerInvariant();
                    uint unsignedHash = ComputeVehicleModelHash(item.model);
                    string decimalId = unsignedHash.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string signedDecimalId = unchecked((int)unsignedHash).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    bool match = name.IndexOf(needle, StringComparison.Ordinal) >= 0 ||
                        model.IndexOf(needle, StringComparison.Ordinal) >= 0 ||
                        id.IndexOf(hashNeedle, StringComparison.Ordinal) >= 0 ||
                        decimalId.IndexOf(needle, StringComparison.Ordinal) >= 0 ||
                        signedDecimalId.IndexOf(needle, StringComparison.Ordinal) >= 0 ||
                        (superDiamondAlias && String.Equals(model, "superd", StringComparison.Ordinal));
                    if (!match) continue;

                    AddonVehicle found = item;
                    results.Add(new MenuEntry(found.name, found.model + "  " + found.id,
                        delegate { return found.id; }, null,
                        delegate { BeginVehicleSpawn(found.model, found.name); }));
                    added++;
                }
                results.Insert(0, new MenuEntry(added + " matches", "Select a result to spawn it.", null, null, null));
                if (added == 0)
                    results.Add(new MenuEntry("No matching vehicles", "Try a different name or model ID.", null, null, null));
            }
            else
            {
                results.Add(new MenuEntry("No query entered", "Use Search again to enter a name or model ID.", null, null, null));
            }
            results.Add(BackEntry());
        }
        private void RebuildCombinedVehicleCatalogCache()
        {
            if (!_combinedVehicleCatalogDirty) return;
            _combinedVehicleCatalog.Clear();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddVehiclesToSearchList(_vehicleCatalog, _combinedVehicleCatalog, seen);
            AddVehiclesToSearchList(_addonVehicles, _combinedVehicleCatalog, seen);
            _combinedVehicleCatalogDirty = false;
            Log("Vehicle search cache rebuilt once: " + _combinedVehicleCatalog.Count + " unique model records.");
        }

        private List<AddonVehicle> GetCombinedVehicleList()
        {
            RebuildCombinedVehicleCatalogCache();
            return _combinedVehicleCatalog;
        }
        private void AddVehiclesToSearchList(List<AddonVehicle> source, List<AddonVehicle> destination, HashSet<string> seen)
        {
            for (int i = 0; i < source.Count; i++)
            {
                AddonVehicle item = source[i];
                if (item == null || !IsSafeVehicleModelName(item.model) || !seen.Add(item.model)) continue;
                destination.Add(item);
            }
        }
        private string FormatVehicleModelId(string modelName)
        {
            return "0x" + ComputeVehicleModelHash(modelName).ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
        }
        private uint ComputeVehicleModelHash(string modelName)
        {
            // GTA uses Jenkins one-at-a-time (JOAAT) hashing over the lowercase model code.
            uint hash = 0;
            string value = (modelName ?? String.Empty).ToLowerInvariant();
            for (int i = 0; i < value.Length; i++)
            {
                hash = unchecked(hash + (byte)value[i]);
                hash = unchecked(hash + (hash << 10));
                hash ^= hash >> 6;
            }
            hash = unchecked(hash + (hash << 3));
            hash ^= hash >> 11;
            hash = unchecked(hash + (hash << 15));
            return hash;
        }
        private void AddVehiclesToGroups(List<AddonVehicle> source,
            Dictionary<string, List<AddonVehicle>> grouped,
            List<string> categoryOrder,
            HashSet<string> seenModels)
        {
            for (int i = 0; i < source.Count; i++)
            {
                AddonVehicle item = source[i];
                if (item == null || !IsSafeVehicleModelName(item.model)) continue;
                if (!seenModels.Add(item.model)) continue;

                string category = NormalizeVehicleCategory(String.IsNullOrWhiteSpace(item.category) ? "Other" : item.category);
                List<AddonVehicle> list;
                if (!grouped.TryGetValue(category, out list))
                {
                    list = new List<AddonVehicle>();
                    grouped.Add(category, list);
                    categoryOrder.Add(category);
                }
                list.Add(item);
            }
        }
    }
}
