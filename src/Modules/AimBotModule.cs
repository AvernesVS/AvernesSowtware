using AvernesTreiner.Core;
using GTA;
using GTA.Math;
using GTA.Native;
using NativeUI;
using System;
using System.Collections.Generic;

namespace AvernesTreiner.Modules
{
    public class AimBotModule : IAvernesModule
    {
        public string Name { get { return "Aim Bot"; } }

        private const float MaximumRange = 140.0f;
        private const float MinimumAimDot = 0.76f;
        private const int AimControl = 25;
        private const int HeadBone = 31086;
        private const int ScanInterval = 500;
        private const float MaximumYawStep = 3.2f;
        private const float MaximumPitchStep = 2.4f;

        private bool _enabled = ConfigManager.GetBool("Combat", "AimBotEnabled", false);
        private UIMenuCheckboxItem _aimBotItem;
        private int _lastScanTime;
        private bool _hasScanned;
        private readonly Queue<int> _errorTimes = new Queue<int>();
        private int _targetHandle;
        private int _lastLoggedTargetHandle;
        private int _lastTargetLogTime;
        private int _cachedAimPedHandle;
        private int _cachedAimVehicleHandle;
        private int _cachedAimVehicleClass = -1;

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
                _hasScanned = false;
                ConfigManager.SetBool("Combat", "AimBotEnabled", _enabled);
                DiagnosticsManager.LogInfo("AimBot", _enabled ? "Aim Bot enabled." : "Aim Bot disabled.");
            };
            _aimBotItem = aimBotItem;
            combatMenu.AddItem(aimBotItem);
        }

        public void ReloadSettings()
        {
            _enabled = ConfigManager.GetBool("Combat", "AimBotEnabled", false);
            _targetHandle = 0;
            _lastScanTime = 0;
            _hasScanned = false;
            _errorTimes.Clear();
            if (_aimBotItem != null) _aimBotItem.Checked = _enabled;
        }

        public void OnTick()
        {
            if (!_enabled) return;

            try
            {
                RunAimLogic();
            }
            catch (Exception ex)
            {
                int now = Game.GameTime;
                while (_errorTimes.Count > 0 && now - _errorTimes.Peek() > 10000)
                    _errorTimes.Dequeue();
                _errorTimes.Enqueue(now);

                SetTarget(0);
                DiagnosticsManager.LogException("AimBot", ex);

                if (_errorTimes.Count >= 5 && _enabled)
                {
                    _enabled = false;
                    _targetHandle = 0;
                    if (_aimBotItem != null) _aimBotItem.Checked = false;
                    ConfigManager.SetBool("Combat", "AimBotEnabled", false);
                    DiagnosticsManager.LogWarning("AimBot", "Aim Bot was automatically disabled after five errors within ten seconds.");
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

            // Safety interlock: do not run target scans or camera natives while the player is driving.
            // Hostile NPCs in vehicles remain targetable when the player is on foot.
            if (player.IsInVehicle())
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
            if (!_hasScanned || now - _lastScanTime >= ScanInterval)
            {
                _hasScanned = true;
                _lastScanTime = now;
                int nextTarget = FindBestTarget(player);
                SetTarget(nextTarget);
            }

            if (_targetHandle == 0) return;

            Ped target = new Ped(_targetHandle);
            if (!target.Exists() || target.IsDead)
            {
                SetTarget(0);
                return;
            }

            MoveAimTowardPosition(player, GetAimPosition(target));
        }

        private void SetTarget(int handle)
        {
            if (_targetHandle == handle) return;
            _targetHandle = handle;

            int now = Game.GameTime;
            if (handle != 0)
            {
                if (_lastLoggedTargetHandle == handle && now - _lastTargetLogTime < 10000) return;
                _lastLoggedTargetHandle = handle;
                _lastTargetLogTime = now;
                DiagnosticsManager.LogDetail("AimBot", "Hostile NPC target selected. Entity handle: " + handle + ".");
            }
            else
            {
                _lastLoggedTargetHandle = 0;
                _lastTargetLogTime = 0;
                DiagnosticsManager.LogDetail("AimBot", "Aim target cleared.");
            }
        }

        private int FindBestTarget(Ped player)
        {
            Vector3 cameraPosition = GameplayCamera.Position;
            Vector3 cameraDirection = GameplayCamera.Direction;
            float bestScore = float.MinValue;
            int bestHandle = 0;
            float maxDistanceSquared = MaximumRange * MaximumRange;

            // Restrict enumeration to entities near the camera instead of every
            // entity currently registered in the world pools.
            Ped[] peds = World.GetNearbyPeds(cameraPosition, MaximumRange);
            for (int i = 0; i < peds.Length; i++)
            {
                Ped candidate = peds[i];
                if (candidate == null || !candidate.Exists() || candidate.Handle == player.Handle ||
                    candidate.IsDead || !IsEnemy(candidate, player))
                    continue;

                Vector3 aimPosition = GetAimPosition(candidate);
                float dx = aimPosition.X - cameraPosition.X;
                float dy = aimPosition.Y - cameraPosition.Y;
                float dz = aimPosition.Z - cameraPosition.Z;
                float distanceSquared = dx * dx + dy * dy + dz * dz;
                if (distanceSquared <= 0.01f || distanceSquared > maxDistanceSquared) continue;

                float distance = (float)Math.Sqrt(distanceSquared);
                float dot = cameraDirection.X * dx / distance +
                    cameraDirection.Y * dy / distance +
                    cameraDirection.Z * dz / distance;
                if (dot < MinimumAimDot) continue;

                if (!Function.Call<bool>(Hash.IS_ENTITY_ON_SCREEN, candidate.Handle)) continue;
                if (!Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY,
                    player.Handle, candidate.Handle, 17)) continue;

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

            // Aim Bot is for human opponents only: explicitly reject wildlife and pets.
            if (!Function.Call<bool>((Hash)0xB980061DA992779D, candidate.Handle))
                return false;

            // Exclude only members of the player's actual ped group. Relationship
            // groups are faction relationships and are not the same as squad membership.
            int playerGroup = Function.Call<int>((Hash)0xF162E133B4E7A675, player.Handle);
            if (playerGroup != 0)
            {
                int candidateGroup = Function.Call<int>((Hash)0xF162E133B4E7A675, candidate.Handle);
                if (candidateGroup == playerGroup) return false;
            }

            if (Function.Call<bool>((Hash)0x12534C348C6CB68B, candidate.Handle))
                return false;

            int relationship = Function.Call<int>((Hash)0xEBA5AD3A0EAF7121,
                candidate.Handle, player.Handle);
            bool hostileRelationship = relationship >= 4;
            bool fightingPlayer = Function.Call<bool>((Hash)0x4859F1FC66A6278E,
                candidate.Handle, player.Handle);

            // Story mission objectives commonly receive a red entity blip before combat starts.
            bool markedAsMissionEnemy = HasRedEntityBlip(candidate);

            // GET_PED_TYPE: 6 = Cop, 27 = SWAT. Both are valid targets while wanted.
            int pedType = Function.Call<int>((Hash)0xFF059E1E4C01E63C, candidate.Handle);
            bool wantedPolice = Game.Player.WantedLevel > 0 &&
                (pedType == 6 || pedType == 27);

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

        private Vector3 GetAimPosition(Ped ped)
        {
            // Keep the head bone for pedestrians on foot, with a small downward
            // correction to avoid aiming above the head. Vehicle occupants use Spine3.
            bool inVehicle = ped.IsInVehicle();
            int bone = inVehicle ? 24818 : HeadBone;
            Vector3 position = Function.Call<Vector3>((Hash)0x17C07FC640E86B4E,
                ped.Handle, bone, 0.0f, 0.0f, 0.0f);

            if (!inVehicle)
            {
                if (_cachedAimPedHandle == ped.Handle)
                {
                    _cachedAimPedHandle = 0;
                    _cachedAimVehicleHandle = 0;
                    _cachedAimVehicleClass = -1;
                }
                return position + new Vector3(0.0f, 0.0f, -0.05f);
            }

            // Resolve the vehicle class only when the ped/vehicle changes. This keeps
            // the truck-specific correction from adding a class native every frame.
            int vehicleHandle = Function.Call<int>((Hash)0x9A9112A0FE9A4713, ped.Handle, false);
            if (vehicleHandle == 0)
            {
                _cachedAimPedHandle = ped.Handle;
                _cachedAimVehicleHandle = 0;
                _cachedAimVehicleClass = -1;
                return position + new Vector3(0.0f, 0.0f, 0.08f);
            }

            if (_cachedAimPedHandle != ped.Handle || _cachedAimVehicleHandle != vehicleHandle)
            {
                _cachedAimPedHandle = ped.Handle;
                _cachedAimVehicleHandle = vehicleHandle;
                _cachedAimVehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62, vehicleHandle);
            }

            // GET_VEHICLE_CLASS: 10 = Industrial, 20 = Commercial (heavy trucks).
            float verticalOffset = (_cachedAimVehicleClass == 10 || _cachedAimVehicleClass == 20)
                ? 0.18f : 0.08f;
            return position + new Vector3(0.0f, 0.0f, verticalOffset);
        }

        private void MoveAimTowardPosition(Ped player, Vector3 head)
        {
            Vector3 cameraPosition = Function.Call<Vector3>((Hash)0x14D6F5678D8F1B37);
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