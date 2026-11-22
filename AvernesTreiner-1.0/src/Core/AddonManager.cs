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
        private static readonly string AddonsPath = @"scripts\AvernesTreiner\addons.json";
        private static List<AddonVehicle> _addons = new List<AddonVehicle>();

        public static List<AddonVehicle> Addons
        {
            get { return _addons; }
        }

        public static void Load()
        {
            try
            {
                _addons.Clear();
                if (File.Exists(AddonsPath))
                {
                    string json = File.ReadAllText(AddonsPath);
                    var serializer = new JavaScriptSerializer();
                    var list = serializer.Deserialize<List<AddonVehicle>>(json);
                    if (list != null)
                    {
                        _addons = list;
                    }
                }
            }
            catch { }
        }
    }
}
