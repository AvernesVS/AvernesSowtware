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
        private Keys _toggleKey = Keys.F4;
        private int _lastToggleTime = 0;

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
                    _toggleKey = Keys.F4;
                }

                _menuPool = new MenuPool();
                _menuPool.MouseEdgeEnabled = false;
                _menuPool.ResetCursorOnOpen = false;

                string title = Localization.Get("General", "MenuTitle", "AvernesTreiner");
                string subtitle = Localization.Get("General", "MenuSubtitle", "v1.0.0");
                _mainMenu = new UIMenu(title, subtitle);
                _menuPool.Add(_mainMenu);

                _modules = new List<IAvernesModule>
                {
                    new PlayerModule(),
                    new VehicleModule(),
                    new VehicleCustomizationModule(),
                    new SkinChangerModule(),
                    new BodyguardModule(),
                    new MiscPowersModule(),
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

                _menuPool.RefreshIndex();

                Tick += OnTick;
                KeyDown += OnKeyDown;

                Interval = 0;

                UI.Notify("~b~AvernesTreiner v1.0.0 ~w~loaded!\nPress ~g~" + _toggleKey.ToString() + " ~w~to open.");
            }
            catch (Exception ex)
            {
                UI.Notify("~r~Error starting AvernesTreiner: " + ex.Message);
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_menuPool != null)
            {
                _menuPool.ProcessControl();
                _menuPool.Draw();

                if (_menuPool.IsAnyMenuOpen())
                {
                    Function.Call(Hash._SHOW_CURSOR_THIS_FRAME, false);
                }
            }

            if (_modules != null)
            {
                for (int i = 0; i < _modules.Count; i++)
                {
                    try
                    {
                        _modules[i].OnTick();
                    }
                    catch { }
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
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
