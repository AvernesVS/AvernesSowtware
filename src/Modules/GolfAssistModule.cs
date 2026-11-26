using AvernesTreiner.Core;
using GTA;
using GTA.Math;
using GTA.Native;
using NativeUI;
using System;

namespace AvernesTreiner.Modules
{
    public class GolfAssistModule : IAvernesModule
    {
        public string Name { get { return "Golf Assistant"; } }
        private bool _enabled = ConfigManager.GetBool("Minigames", "GolfAssistEnabled", false);
        private int _lastAdviceTime;
        private const uint GolfBall = 0xAF0E3F9F;
        private const uint GolfBallP2 = 0x605A3DA9;
        private const uint GolfBallP3 = 0xD5362763;
        private const uint GolfBallP4 = 0xC593081D;
        private const uint GolfBallTee = 0xB5E60C50;
        private const uint GolfFlag = 0x8C60CCE5;
        private const ulong GetClosestObjectOfType = 0xE143FA2249364369UL;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            UIMenu menu = menuPool.AddSubMenu(mainMenu, Localization.Get("Minigames", "GolfAssistant", "Golf Shot Assistant"));
            var enabledItem = new UIMenuCheckboxItem(
                Localization.Get("Minigames", "GolfEnabled", "Live Golf Advice"),
                _enabled,
                Localization.Get("Minigames", "GolfEnabledDesc", "Show recommendations near a golf ball"));
            enabledItem.CheckboxEvent += (sender, state) =>
            {
                _enabled = state;
                ConfigManager.SetBool("Minigames", "GolfAssistEnabled", _enabled);
                _lastAdviceTime = 0;
            };
            menu.AddItem(enabledItem);

            var analyzeItem = new UIMenuItem(
                Localization.Get("Minigames", "GolfAnalyze", "Analyze Current Shot"),
                Localization.Get("Minigames", "GolfAnalyzeDesc", "Estimate distance, elevation and a club class"));
            analyzeItem.Activated += (sender, selected) => AnalyzeShot(true);
            menu.AddItem(analyzeItem);
        }

        public void ReloadSettings()
        {
            _enabled = ConfigManager.GetBool("Minigames", "GolfAssistEnabled", false);
            _lastAdviceTime = 0;
        }

        public void OnTick()
        {
            if (!_enabled || Game.GameTime - _lastAdviceTime < 1200) return;
            _lastAdviceTime = Game.GameTime;
            AnalyzeShot(false);
        }

        private string GetClubHint(float distance)
        {
            if (distance <= 20.0f)
                return Localization.Get("Minigames", "GolfClubPutter", "Putter (short putt)");
            if (distance <= 75.0f)
                return Localization.Get("Minigames", "GolfClubWedge", "Wedge (short approach)");
            if (distance <= 160.0f)
                return Localization.Get("Minigames", "GolfClubIron", "Iron (approach)");
            if (distance <= 250.0f)
                return Localization.Get("Minigames", "GolfClubWood", "Wood / long iron");
            return Localization.Get("Minigames", "GolfClubDriver", "Long club; Driver only from tee");
        }

        private void AnalyzeShot(bool notifyFailure)
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            int ball = FindClosestGolfBall(player.Position, 18.0f);
            if (ball == 0)
            {
                if (notifyFailure) UI.Notify(Localization.Get("Minigames", "GolfNoBall", "No nearby golf ball found"));
                return;
            }

            Vector3 ballPosition = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, ball, true);
            int flag = Function.Call<int>(
                (Hash)GetClosestObjectOfType,
                ballPosition.X, ballPosition.Y, ballPosition.Z, 180.0f,
                unchecked((int)GolfFlag), false, false, false);

            if (flag == 0 || !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, flag))
            {
                if (notifyFailure) UI.Notify(Localization.Get("Minigames", "GolfNoFlag", "Golf ball found, but no nearby hole flag was detected"));
                return;
            }

            Vector3 target = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, flag, true);
            float dx = target.X - ballPosition.X;
            float dy = target.Y - ballPosition.Y;
            float dz = target.Z - ballPosition.Z;
            float horizontalDistance = (float)Math.Sqrt(dx * dx + dy * dy);

            string elevation = dz > 1.5f
                ? Localization.Get("Minigames", "GolfUphill", "uphill +") + dz.ToString("0.0") + "m"
                : (dz < -1.5f
                    ? Localization.Get("Minigames", "GolfDownhill", "downhill ") + dz.ToString("0.0") + "m"
                    : Localization.Get("Minigames", "GolfLevel", "level"));

            string message =
                Localization.Get("Minigames", "GolfAdvice", "Golf estimate") + ": " +
                horizontalDistance.ToString("0.0") + "m | " +
                GetClubHint(horizontalDistance) + " | " +
                elevation + ". " +
                Localization.Get("Minigames", "GolfPowerHint",
                    "Use the game's yellow target zone for actual shot power; it accounts for club, aim and elevation.");

            UI.ShowSubtitle(message, 4500);
        }

        private int FindClosestGolfBall(Vector3 origin, float radius)
        {
            uint[] models = new uint[] { GolfBall, GolfBallP2, GolfBallP3, GolfBallP4, GolfBallTee };
            int best = 0;
            float bestDistance = radius;

            for (int i = 0; i < models.Length; i++)
            {
                int entity = Function.Call<int>(
                    (Hash)GetClosestObjectOfType,
                    origin.X, origin.Y, origin.Z, radius,
                    (int)models[i], false, false, false);

                if (entity == 0 || !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, entity)) continue;

                Vector3 position = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, entity, true);
                float dx = position.X - origin.X;
                float dy = position.Y - origin.Y;
                float dz = position.Z - origin.Z;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = entity;
                }
            }

            return best;
        }
    }
}