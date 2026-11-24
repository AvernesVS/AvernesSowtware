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

namespace AvernesTreiner.Core
{
    public class AddonVehicle
    {
        private string _name;
        private string _model;
        private string _category;

        public string name { get { return _name; } set { _name = value; } }
        public string model { get { return _model; } set { _model = value; } }
        public string category { get { return _category; } set { _category = value; } }
    }

    public static class AddonManager
    {
        private static string AddonsPath { get { return Path.Combine(ConfigManager.UserDataDirectoryPath, "addons.json"); } }
        private static List<AddonVehicle> _addons = new List<AddonVehicle>();

        public static List<AddonVehicle> Addons
        {
            get { return _addons; }
        }

        public static void Load()
        {
            _addons.Clear();
            try
            {
                if (!File.Exists(AddonsPath))
                    return;

                if (new FileInfo(AddonsPath).Length > 1048576)
                    throw new InvalidDataException("addons.json exceeds the 1 MB safety limit.");
                string json = File.ReadAllText(AddonsPath, System.Text.Encoding.UTF8);
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 1024 * 1024;
                var list = serializer.Deserialize<List<AddonVehicle>>(json);
                if (list == null) return;

                HashSet<string> seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < list.Count && _addons.Count < 500; i++)
                {
                    AddonVehicle item = list[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.name) ||
                        string.IsNullOrWhiteSpace(item.model))
                        continue;

                    string model = item.model.Trim();
                    bool validModel = model.Length <= 64;
                    for (int c = 0; validModel && c < model.Length; c++)
                    {
                        char ch = model[c];
                        validModel = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') ||
                                     (ch >= '0' && ch <= '9') || ch == '_';
                    }
                    if (!validModel || !seenModels.Add(model)) continue;

                    item.name = item.name.Trim();
                    if (item.name.Length > 80) item.name = item.name.Substring(0, 80);
                    item.model = model;
                    if (string.IsNullOrWhiteSpace(item.category)) item.category = "Other";
                    else
                    {
                        item.category = item.category.Trim();
                        if (item.category.Length > 32) item.category = item.category.Substring(0, 32);
                    }
                    _addons.Add(item);
                }
            }
            catch (Exception ex)
            {
                TrainerLog.Write("AddonManager.Load", ex);
                _addons.Clear();
            }
        }
    }
}
