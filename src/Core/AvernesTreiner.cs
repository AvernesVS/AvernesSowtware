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
        private bool _rebuildMenuRequested = false;
        private bool _reloadModuleSettingsRequested = false;

        // Menu navigation inputs are read first, then suppressed from gameplay.
        private static readonly GTA.Control[] MenuNavigationControls = new GTA.Control[]
        {
            GTA.Control.PhoneUp, GTA.Control.PhoneDown, GTA.Control.PhoneLeft, GTA.Control.PhoneRight,
            GTA.Control.CursorScrollUp, GTA.Control.CursorScrollDown,
            GTA.Control.FrontendAccept, GTA.Control.PhoneCancel, GTA.Control.FrontendPause
        };

        // PC phone-open input is separate from the phone navigation axes.
        private static readonly int[] PhoneOpenControls = new int[] { 27 };

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

                BuildMenus();

                Tick += OnTick;
                KeyDown += OnKeyDown;

                Interval = 0;

                UI.Notify("~b~AvernesTreiner v0.9.2 ~w~loaded!\nPress ~g~" + _toggleKey.ToString() + " ~w~to open.");
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Error starting AvernesTreiner: " + ex.Message);
            }
        }

        private void BuildMenus()
        {
            _menuPool = new MenuPool();
            _menuPool.MouseEdgeEnabled = false;
            _menuPool.ResetCursorOnOpen = false;
            _menuPool.ControlDisablingEnabled = false;

            string title = Localization.Get("General", "MenuTitle", "AvernesTreiner");
            string subtitle = Localization.Get("General", "MenuSubtitle", "v0.9.2");
            _mainMenu = new UIMenu(title, subtitle);
            _menuPool.Add(_mainMenu);

            if (_modules == null)
            {
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
                    new GolfAssistModule(),
                    new SettingsModule(delegate(bool reloadSettings)
                    {
                        _reloadModuleSettingsRequested = reloadSettings;
                        _rebuildMenuRequested = true;
                    })
                };
            }

            foreach (var module in _modules)
            {
                try { module.Initialize(_mainMenu, _menuPool); }
                catch { }
            }

            foreach (UIMenu menu in _menuPool.ToList())
            {
                menu.MouseControlsEnabled = false;
                menu.MouseEdgeEnabled = false;
                menu.ResetCursorOnOpen = false;
            }
            _menuPool.RefreshIndex();
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
                // NativeUI consumes its navigation inputs first. Restore gameplay controls
                // afterwards so movement, aiming, weapon selection and radio remain available.
                _menuPool.ProcessControl();
                _menuPool.Draw();
                EnableGameplayControls();
                DisableMenuNavigationControls();
            }

            if (_rebuildMenuRequested)
            {
                _rebuildMenuRequested = false;
                if (_reloadModuleSettingsRequested && _modules != null)
                {
                    _reloadModuleSettingsRequested = false;
                    for (int i = 0; i < _modules.Count; i++)
                    {
                        try { _modules[i].ReloadSettings(); }
                        catch { }
                    }
                }
                BuildMenus();
                _mainMenu.Visible = true;
                return;
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
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 1, control);
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 2, control);
            }
            for (int i = 0; i < PhoneOpenControls.Length; i++)
            {
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, PhoneOpenControls[i]);
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 1, PhoneOpenControls[i]);
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 2, PhoneOpenControls[i]);
            }
        }

        private void EnableGameplayControls()
        {
            // NativeUI has already consumed menu navigation for this frame.
            // Re-enable normal gameplay input, then suppress only controls reserved
            // for the trainer menu so keys are not processed by both systems.
            for (int inputGroup = 0; inputGroup <= 2; inputGroup++)
                Function.Call(Hash.ENABLE_ALL_CONTROL_ACTIONS, inputGroup);
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
