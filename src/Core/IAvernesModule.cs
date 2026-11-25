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
    public interface IAvernesModule
    {
        string Name { get; }
        void Initialize(UIMenu mainMenu, MenuPool menuPool);
        void OnTick();
        void ReloadSettings();
    }
}
