using AvernesTreiner.Core;
using GTA;
using GTA.Math;
using GTA.Native;
using NativeUI;
using System;

namespace AvernesTreiner.Modules
{
    public class AimBotModule : IAvernesModule
    {
        public string Name { get { return "Aim Bot"; } }

        private const float MaximumRange = 140.0f;
        private const float MinimumAimDot = 0.76f;
        private const int AimControl = 25;
        private const int HeadBone = 31086;
        private const int ScanInterval = 100;
        private const float MaximumYawStep = 3.2f;
        private const float MaximumPitchStep = 2.4f;

        private bool _enabled = ConfigManager.GetBool("Combat", "AimBotEnabled", false);
        private int _lastScanTime;
        private int _targetHandle;
        private int _consecutiveErrors;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            UIMenu combatMenu = menuPool.AddSubMenu(mainMenu,
                Localization.Get("Combat", "AimBotTitle", "Aim Bot"));

            var aimBotItem = new UIMenuCheckboxItem(
                Localization.Get("Combat", "AimBotEnabled", "Aim Bot"),
                _enabled,
                Localization.Get("Combat", "AimBotEnabledDesc",
                    "Smoothly assists aim against hostile NPCs and mission enemies. Does not fire automatically."));
            aimBotItem.CheckboxEvent += (sender, state) =>
            {
                _enabled = state;
                _targetHandle = 0;
                ConfigManager.SetBool("Combat", "AimBotEnabled", _enabled);
                DiagnosticsManager.LogInfo("AimBot", _enabled ? "Aim Bot enabled." : "Aim Bot disabled.");
            };
            combatMenu.AddItem(aimBotItem);
        }

        public void ReloadSettings()
        {
            _enabled = ConfigManager.GetBool("Combat", "AimBotEnabled", false);
            _targetHandle = 0;
            _lastScanTime = 0;
        }

        public void OnTick()
        {
            if (!_enabled) return;

            try
            {
                RunAimLogic();
                _consecutiveErrors = 0;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                SetTarget(0);
                DiagnosticsManager.LogException("AimBot", ex);

                if (_consecutiveErrors >= 3 && _enabled)
                {
                    _enabled = false;
                    ConfigManager.SetBool("Combat", "AimBotEnabled", false);
                    DiagnosticsManager.LogWarning("AimBot", "Aim Bot was automatically disabled after repeated errors.");
                    UI.Notify(Localization.Get("Diagnostics", "AimBotAutoDisabled",
                        "~r~Aim Bot disabled after repeated errors. Check the diagnostics report."));
                }
            }
        }

        private void RunAimLogic()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists() || player.IsDead)
            {
                SetTarget(0);
                return;
            }

            bool aiming = Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 0, AimControl) ||
                Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 0, AimControl);
            if (!aiming)
            {
                SetTarget(0);
                return;
            }

            int now = Game.GameTime;
            if (_targetHandle == 0 || now - _lastScanTime >= ScanInterval)
            {
                _lastScanTime = now;
                SetTarget(FindBestTarget(player));
            }

            if (_targetHandle == 0) return;

            Ped target = new Ped(_targetHandle);
            if (!target.Exists() || target.IsDead || !IsEnemy(target, player))
            {
                SetTarget(0);
                return;
            }

            MoveAimToward(player, target);
        }

        private void SetTarget(int handle)
        {
            if (_targetHandle == handle) return;
            _targetHandle = handle;
            if (handle == 0)
                DiagnosticsManager.LogDetail("AimBot", "Aim target cleared.");
            else
                DiagnosticsManager.LogDetail("AimBot", "Hostile target selected. Entity handle: " + handle + ".");
        }

        private int FindBestTarget(Ped player)
        {
            Ped[] peds = World.GetAllPeds();
            Vector3 cameraPosition = Function.Call<Vector3>((Hash)0x14D6F5678D8F1B37);
            Vector3 cameraDirection = GameplayCamera.Direction;
            float bestScore = float.MinValue;
            int bestHandle = 0;
            float maxDistanceSquared = MaximumRange * MaximumRange;

            for (int i = 0; i < peds.Length; i++)
            {
                Ped candidate = peds[i];
                if (candidate == null || !candidate.Exists() || candidate.Handle == player.Handle ||
                    candidate.IsDead || !IsEnemy(candidate, player))
                    continue;

                Vector3 head = GetHeadPosition(candidate);
                float dx = head.X - cameraPosition.X;
                float dy = head.Y - cameraPosition.Y;
                float dz = head.Z - cameraPosition.Z;
                float distanceSquared = dx * dx + dy * dy + dz * dz;
                if (distanceSquared <= 0.01f || distanceSquared > maxDistanceSquared) continue;

                if (!Function.Call<bool>(Hash.IS_ENTITY_ON_SCREEN, candidate.Handle)) continue;
                if (!Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY,
                    player.Handle, candidate.Handle, 17)) continue;

                float distance = (float)Math.Sqrt(distanceSquared);
                float inverseDistance = 1.0f / distance;
                float dot = cameraDirection.X * dx * inverseDistance +
                    cameraDirection.Y * dy * inverseDistance +
                    cameraDirection.Z * dz * inverseDistance;
                if (dot < MinimumAimDot) continue;

                float score = dot * 3.0f - distance / MaximumRange;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestHandle = candidate.Handle;
                }
            }

            return bestHandle;
        }

        private bool IsEnemy(Ped candidate, Ped player)
        {
            if (candidate == null || !candidate.Exists() || candidate.Handle == player.Handle ||
                candidate.IsDead)
                return false;

            if (Function.Call<bool>((Hash)0x12534C348C6CB68B, candidate.Handle))
                return false;

            int relationship = Function.Call<int>((Hash)0xEBA5AD3A0EAF7121,
                candidate.Handle, player.Handle);
            bool hostileRelationship = relationship >= 4;
            bool fightingPlayer = Function.Call<bool>((Hash)0x4859F1FC66A6278E,
                candidate.Handle, player.Handle);

            // Story mission objectives commonly receive a red entity blip before combat starts.
            bool markedAsMissionEnemy = HasRedEntityBlip(candidate);

            // Police are valid targets while the player is wanted, even if their combat
            // relationship has not yet updated for the current frame.
            bool wantedPolice = Game.Player.WantedLevel > 0 &&
                Function.Call<int>((Hash)0xFF059E1E4C01E63C, candidate.Handle) == 6;

            return hostileRelationship || fightingPlayer ||
                markedAsMissionEnemy || wantedPolice;
        }

        private bool HasRedEntityBlip(Ped ped)
        {
            int blip = Function.Call<int>((Hash)0xBC8DBDCA2436F7E8, ped.Handle);
            if (blip == 0 || !Function.Call<bool>((Hash)0xA6DB27D19ECBB7DA, blip))
                return false;

            // Blip colour 1 is the standard red hostile/objective colour.
            return Function.Call<int>((Hash)0xDF729E8D20CF7327, blip) == 1;
        }

        private Vector3 GetHeadPosition(Ped ped)
        {
            return Function.Call<Vector3>((Hash)0x17C07FC640E86B4E,
                ped.Handle, HeadBone, 0.0f, 0.0f, 0.0f);
        }

        private void MoveAimToward(Ped player, Ped target)
        {
            Vector3 cameraPosition = Function.Call<Vector3>((Hash)0x14D6F5678D8F1B37);
            Vector3 head = GetHeadPosition(target);
            float dx = head.X - cameraPosition.X;
            float dy = head.Y - cameraPosition.Y;
            float dz = head.Z - cameraPosition.Z;
            float horizontalDistance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (horizontalDistance < 0.01f) return;

            float targetWorldHeading = (float)(Math.Atan2(-dx, dy) * 180.0 / Math.PI);
            float targetRelativeHeading = NormalizeAngle(targetWorldHeading - player.Heading);
            float currentRelativeHeading = Function.Call<float>((Hash)0x743607648ADD4587);
            float nextHeading = MoveTowardAngle(
                currentRelativeHeading, targetRelativeHeading, MaximumYawStep);
            Function.Call((Hash)0xB4EC2312F4E5B1F1, nextHeading);

            float targetPitch = (float)(Math.Atan2(dz, horizontalDistance) * 180.0 / Math.PI);
            targetPitch = Clamp(targetPitch, -75.0f, 35.0f);
            float currentPitch = Function.Call<float>((Hash)0x3A6867B4845BEDA2);
            float nextPitch = MoveToward(currentPitch, targetPitch, MaximumPitchStep);
            Function.Call((Hash)0x6D0858B8EDFD2B7D, nextPitch, 1.0f);
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180.0f) angle -= 360.0f;
            while (angle < -180.0f) angle += 360.0f;
            return angle;
        }

        private static float MoveTowardAngle(float current, float target, float maxStep)
        {
            return NormalizeAngle(current + Clamp(NormalizeAngle(target - current), -maxStep, maxStep));
        }

        private static float MoveToward(float current, float target, float maxStep)
        {
            return current + Clamp(target - current, -maxStep, maxStep);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return value < minimum ? minimum : (value > maximum ? maximum : value);
        }
    }
}