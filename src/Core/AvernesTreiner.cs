using AvernesTreiner.Core;
using AvernesTreiner.Modules;
using GTA.Math;
using GTA.Native;
using GTA;
using NativeUI;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System;

namespace AvernesTreiner
{
    internal static class TrainerLog
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, DateTime> LastWritten = new Dictionary<string, DateTime>();

        public static void Write(string source, Exception exception)
        {
            try
            {
                lock (Sync)
                {
                    DateTime now = DateTime.UtcNow;
                    DateTime last;
                    if (LastWritten.TryGetValue(source, out last) &&
                        (now - last).TotalSeconds < 10.0)
                        return;
                    LastWritten[source] = now;

                    string directory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "AvernesTreiner", "Logs");
                    if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, "trainer.log");
                    FileInfo info = new FileInfo(path);
                    if (info.Exists && info.Length > 1048576)
                    {
                        string previous = path + ".old";
                        if (File.Exists(previous)) File.Delete(previous);
                        File.Move(path, previous);
                    }

                    string details = exception == null ? "No exception details." : exception.ToString();
                    File.AppendAllText(path,
                        "[" + now.ToString("yyyy-MM-dd HH:mm:ss") + " UTC] " +
                        source + Environment.NewLine + details + Environment.NewLine +
                        "----------------------------------------" + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    public class AvernesTreinerScript : Script
    {
        private MenuPool _menuPool;
        private UIMenu _mainMenu;
        private List<IAvernesModule> _modules;
        private Keys _toggleKey = Keys.F10;
        private int _lastToggleTime = 0;
        private int _suppressPauseUntil = 0;

        // Menu navigation inputs are read first, then suppressed from gameplay.
        private static readonly GTA.Control[] MenuNavigationControls = new GTA.Control[]
        {
            GTA.Control.PhoneUp, GTA.Control.PhoneDown, GTA.Control.PhoneLeft, GTA.Control.PhoneRight,
            GTA.Control.CursorScrollUp, GTA.Control.CursorScrollDown,
            GTA.Control.FrontendAccept, GTA.Control.PhoneCancel, GTA.Control.FrontendPause
        };

        public AvernesTreinerScript()
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                    TrainerLog.Write("AppDomain", args.ExceptionObject as Exception);
                ConfigManager.Load();
                Localization.LoadLanguage(ConfigManager.Language);

                try
                {
                    _toggleKey = (Keys)Enum.Parse(typeof(Keys), ConfigManager.MenuKey, true);
                }
                catch
                {
                    _toggleKey = Keys.F10;
                }

                _menuPool = new MenuPool();
                _menuPool.MouseEdgeEnabled = false;
                // NativeUI 1.9.0 exposes cursor control on each UIMenu, not MenuPool.
                _menuPool.ResetCursorOnOpen = false;
                // Keep gameplay and camera controls active while the menu is open.
                _menuPool.ControlDisablingEnabled = false;

                string title = Localization.Get("General", "MenuTitle", "AvernesTreiner");
                string subtitle = Localization.Get("General", "MenuSubtitle", "v1.0");
                _mainMenu = new UIMenu(title, subtitle);
                _menuPool.Add(_mainMenu);

                _modules = new List<IAvernesModule>
                {
                    new PlayerModule(),
                    new VehicleModule(),
                    new SavedVehiclesModule(),
                    new VehicleCustomizationModule(),
                    new SkinChangerModule(),
                    new BodyguardModule(),
                    new AnimationModule(),
                    new WeaponModule(),
                    new TeleportModule(),
                    new WorldModule(),
                    new SettingsModule()
                };

                foreach (var module in _modules)
                {
                    try
                    {
                        module.Initialize(_mainMenu, _menuPool);
                    }
                    catch (Exception ex) { TrainerLog.Write("Module.Initialize: " + module.Name, ex); }
                }

                // Disable the NativeUI cursor on the main menu and every submenu.
                foreach (UIMenu menu in _menuPool.ToList())
                {
                    menu.MouseControlsEnabled = false;
                    menu.MouseEdgeEnabled = false;
                    menu.ResetCursorOnOpen = false;
                }

                _menuPool.RefreshIndex();

                Tick += OnTick;
                KeyDown += OnKeyDown;

                Interval = 0;

                UI.Notify("~b~AvernesTreiner v1.0 ~w~loaded!\nPress ~g~" + _toggleKey.ToString() + " ~w~to open.");
            }
            catch (Exception ex)
            {
                TrainerLog.Write("Startup", ex);
                UI.Notify("~r~Error starting AvernesTreiner: " + ex.Message);
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_menuPool != null && _menuPool.IsAnyMenuOpen() && Game.IsKeyPressed(Keys.Escape))
            {
                _menuPool.CloseAllMenus();
                _suppressPauseUntil = Game.GameTime + 500;
            }

            if (_menuPool != null && _menuPool.IsAnyMenuOpen())
            {
                _menuPool.ProcessControl();
                _menuPool.Draw();
                DisableMenuNavigationControls();
                EnableCameraControls();
            }

            int now = Game.GameTime;
            if (_suppressPauseUntil != 0)
            {
                if (now <= _suppressPauseUntil)
                    Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, (int)GTA.Control.FrontendPause);
                else
                    _suppressPauseUntil = 0;
            }

            if (_modules != null)
            {
                for (int i = 0; i < _modules.Count; i++)
                {
                    try { _modules[i].OnTick(); }
                    catch (Exception ex) { TrainerLog.Write("Module.OnTick: " + _modules[i].Name, ex); }
                }
            }
        }

        private void DisableMenuNavigationControls()
        {
            for (int i = 0; i < MenuNavigationControls.Length; i++)
            {
                int control = (int)MenuNavigationControls[i];
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, control);
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 2, control);
            }
        }

        private void EnableCameraControls()
        {
            // NativeUI can suppress look input even when ControlDisablingEnabled is false.
            // Re-enable the two camera axes in all gameplay input groups after menu processing.
            int[] cameraControls = new int[] { 1, 2 };
            int[] inputGroups = new int[] { 0, 1, 2 };
            for (int g = 0; g < inputGroups.Length; g++)
            {
                for (int c = 0; c < cameraControls.Length; c++)
                {
                    Function.Call(Hash.ENABLE_CONTROL_ACTION, inputGroups[g], cameraControls[c]);
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _menuPool != null && _menuPool.IsAnyMenuOpen())
            {
                _menuPool.CloseAllMenus();
                _suppressPauseUntil = Game.GameTime + 500;
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == _toggleKey)
            {
                int currentTime = Game.GameTime;
                if (currentTime - _lastToggleTime < 300) return;
                _lastToggleTime = currentTime;

                if (_menuPool != null)
                {
                    if (_menuPool.IsAnyMenuOpen())
                    {
                        _menuPool.CloseAllMenus();
                    }
                    else
                    {
                        _mainMenu.Visible = true;
                    }
                }
            }
        }
    }
}
