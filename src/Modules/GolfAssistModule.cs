using AvernesTreiner.Core;
using GTA.Math;
using GTA.Native;
using GTA;
using NativeUI;
using System;
using System.Collections.Generic;

namespace AvernesTreiner.Modules
{
    /// <summary>
    /// Experimental Story Mode golf helper.
    /// It aligns the player's heading toward the closest course flag and draws a guide line.
    /// It never triggers a shot or changes the score.
    /// </summary>
    public class GolfAssistModule : IAvernesModule
    {
        public string Name { get { return "Minigames"; } }

        private bool _enabled = ConfigManager.GetBool("Minigames", "GolfAssist", false);
        private int _lastScanTime;
        private int _lastAimTime;
        private int _ballHandle;
        private int _flagHandle;
        private Vector3 _ballPosition;
        private Vector3 _flagPosition;
        private bool _targetValid;

        private static readonly uint[] GolfBallModels = new uint[]
        {
            0xAF0E3F9Fu, // prop_golf_ball
            0x605A3DA9u, // prop_golf_ball_p2
            0xD5362763u, // prop_golf_ball_p3
            0xC593081Du, // prop_golf_ball_p4
            0xB5E60C50u  // prop_golf_ball_tee
        };
        private const uint GolfFlagModel = 0x8C60CCE5u; // prop_golfflag
        // GET_CLOSEST_OBJECT_OF_TYPE (native hash from the GTA V object namespace).
        private const ulong GetClosestObjectOfTypeNative = 0xE143FA2249364369UL;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            UIMenu minigameMenu = menuPool.AddSubMenu(
                mainMenu,
                Localization.Get("General", "SubmenuMinigames", "Minigame Assistant"));

            UIMenuCheckboxItem assistItem = new UIMenuCheckboxItem(
                Localization.Get("MiniGames", "GolfAssist", "Smart Golf Aim"),
                _enabled,
                Localization.Get("MiniGames", "GolfAssistDesc",
                    "Aligns your character toward the nearest flag and draws a guide line. You take the shot."));

            assistItem.CheckboxEvent += (sender, state) =>
            {
                _enabled = state;
                ConfigManager.SetBool("Minigames", "GolfAssist", _enabled);
                _targetValid = false;
                UI.Notify(_enabled
                    ? "~g~Golf aiming assistant enabled."
                    : "~y~Golf aiming assistant disabled.");
            };
            minigameMenu.AddItem(assistItem);

            UIMenuItem aimNowItem = new UIMenuItem(
                Localization.Get("MiniGames", "GolfAimNow", "Aim at the hole now"),
                Localization.Get("MiniGames", "GolfAimNowDesc", "Align your direction once toward the nearest hole"));
            aimNowItem.Activated += (sender, selected) =>
            {
                if (!TryFindGolfTarget())
                {
                    UI.Notify("~y~" + Localization.Get("MiniGames", "GolfTargetNotFound",
                        "Golf ball or flag not found. Stand near your ball during a golf round."));
                    return;
                }
                ApplyAim();
                UI.Notify("~g~" + Localization.Get("MiniGames", "GolfTargetFound",
                    "Direction calculated. Check the guide line and take the shot yourself."));
            };
            minigameMenu.AddItem(aimNowItem);
        }

        public void ReloadSettings()
        {
            _enabled = ConfigManager.GetBool("Minigames", "GolfAssist", false);
            _targetValid = false;
        }

        public void OnTick()
        {
            if (!_enabled) return;

            int now = Game.GameTime;
            if (now - _lastScanTime >= 250)
            {
                _lastScanTime = now;
                _targetValid = TryFindGolfTarget();
            }

            if (!_targetValid) return;
            if (!Function.Call<bool>(Hash.DOES_ENTITY_EXIST, _ballHandle) ||
                !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, _flagHandle))
            {
                _targetValid = false;
                return;
            }

            Ped player = Game.Player.Character;
            if (player == null || !player.Exists() || player.IsInVehicle() || player.IsDead)
            {
                _targetValid = false;
                return;
            }

            if (now - _lastAimTime >= 150)
            {
                _lastAimTime = now;
                ApplyAim();
            }

            // A world-space guide is drawn every frame so it remains visible while aiming.
            Function.Call(Hash.DRAW_LINE,
                _ballPosition.X, _ballPosition.Y, _ballPosition.Z + 0.12f,
                _flagPosition.X, _flagPosition.Y, _flagPosition.Z + 0.18f,
                70, 220, 110, 220);
        }

        private bool TryFindGolfTarget()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists() || player.IsInVehicle() || player.IsDead)
                return false;

            Vector3 playerPosition = player.Position;
            int closestBall = 0;
            Vector3 closestBallPosition = Vector3.Zero;
            float closestBallDistance = float.MaxValue;

            for (int i = 0; i < GolfBallModels.Length; i++)
            {
                int handle = FindClosestObject(playerPosition, 8.0f, GolfBallModels[i]);
                if (handle == 0) continue;

                Vector3 position = GetEntityPosition(handle);
                float distance = DistanceSquared(playerPosition, position);
                if (distance < closestBallDistance)
                {
                    closestBallDistance = distance;
                    closestBall = handle;
                    closestBallPosition = position;
                }
            }

            // A nearby ball is required so ambient golfers elsewhere on the course
            // cannot activate the helper for the player.
            if (closestBall == 0 || closestBallDistance > 64.0f)
                return false;

            int flag = FindClosestObject(closestBallPosition, 500.0f, GolfFlagModel);
            if (flag == 0) return false;

            Vector3 flagPosition = GetEntityPosition(flag);
            if (DistanceSquared(closestBallPosition, flagPosition) < 1.0f)
                return false;

            _ballHandle = closestBall;
            _flagHandle = flag;
            _ballPosition = closestBallPosition;
            _flagPosition = flagPosition;
            return true;
        }

        private int FindClosestObject(Vector3 origin, float radius, uint modelHash)
        {
            int handle = Function.Call<int>((Hash)GetClosestObjectOfTypeNative,
                origin.X, origin.Y, origin.Z, radius, modelHash, false, false, false);
            if (handle == 0 || !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle))
                return 0;
            return handle;
        }

        private Vector3 GetEntityPosition(int handle)
        {
            return Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, handle, true);
        }

        private void ApplyAim()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            float dx = _flagPosition.X - _ballPosition.X;
            float dy = _flagPosition.Y - _ballPosition.Y;
            float heading = (float)(Math.Atan2(dx, dy) * (180.0 / Math.PI));
            if (heading < 0.0f) heading += 360.0f;

            // Aligns the golfer's facing direction; the game still controls club,
            // swing timing, power, wind and ball physics.
            Function.Call(Hash.SET_ENTITY_HEADING, player.Handle, heading);
        }

        private static float DistanceSquared(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}