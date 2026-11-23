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

namespace AvernesTreiner
{
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
                string subtitle = Localization.Get("General", "MenuSubtitle", "v0.8");
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
                    catch { }
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

                UI.Notify("~b~AvernesTreiner v0.8 ~w~loaded!\nPress ~g~" + _toggleKey.ToString() + " ~w~to open.");
            }
            catch (Exception ex)
            {
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
                    catch { }
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
