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
        private static readonly int[] RangeTargetModelHashes = new int[]
        {
            new Model("prop_range_target_01").Hash,
            new Model("prop_range_target_02").Hash,
            new Model("prop_range_target_03").Hash,
            new Model("prop_target_blue").Hash,
            new Model("prop_target_red").Hash,
            new Model("prop_target_bull").Hash,
            new Model("prop_target_bull_b").Hash,
            new Model("prop_target_red_blue_01").Hash,
            new Model("prop_target_inner1").Hash,
            new Model("prop_target_inner2").Hash,
            new Model("prop_target_inner2_b").Hash,
            new Model("prop_target_inner3").Hash,
            new Model("prop_target_inner3_b").Hash,
            new Model("prop_target_inner_b").Hash,
            new Model("prop_target_ora_purp_01").Hash,
            new Model("prop_target_oran_cross").Hash,
            new Model("prop_target_orange_arrow").Hash,
            new Model("prop_target_purp_arrow").Hash,
            new Model("prop_target_purp_cross").Hash,
            new Model("prop_target_red_arrow").Hash,
            new Model("prop_target_red_cross").Hash
        };
        private const int ScanInterval = 250;
        private const float MaximumYawStep = 3.2f;
        private const float MaximumPitchStep = 2.4f;

        private bool _enabled = ConfigManager.GetBool("Combat", "AimBotEnabled", false);
        private int _lastScanTime;
        private int _targetHandle;
        private bool _targetIsRangeObject;
        private int _lastLoggedTargetHandle;
        private int _lastTargetLogTime;
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
                bool isRangeTarget;
                int nextTarget = FindBestTarget(player, out isRangeTarget);
                SetTarget(nextTarget, isRangeTarget);
            }

            if (_targetHandle == 0) return;

            if (_targetIsRangeObject)
            {
                Prop rangeTarget = new Prop(_targetHandle);
                if (!rangeTarget.Exists() || !IsRangeTarget(rangeTarget) ||
                    !Function.Call<bool>(Hash.IS_ENTITY_ON_SCREEN, rangeTarget.Handle) ||
                    !Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY, player.Handle, rangeTarget.Handle, 17))
                {
                    SetTarget(0);
                    return;
                }
                MoveAimTowardPosition(player, rangeTarget.Position + new Vector3(0.0f, 0.0f, 0.8f));
                return;
            }

            Ped target = new Ped(_targetHandle);
            if (!target.Exists() || target.IsDead || !IsEnemy(target, player))
            {
                SetTarget(0);
                return;
            }

            MoveAimTowardPosition(player, GetAimPosition(target));
        }

        private void SetTarget(int handle, bool isRangeObject = false)
        {
            if (_targetHandle == handle && _targetIsRangeObject == isRangeObject) return;
            _targetHandle = handle;
            _targetIsRangeObject = handle != 0 && isRangeObject;

            int now = Game.GameTime;
            if (handle != 0)
            {
                if (_lastLoggedTargetHandle == handle && now - _lastTargetLogTime < 10000) return;
                _lastLoggedTargetHandle = handle;
                _lastTargetLogTime = now;
                DiagnosticsManager.LogDetail("AimBot", (_targetIsRangeObject ? "Range target selected. Entity handle: " :
                    "Hostile target selected. Entity handle: ") + handle + ".");
            }
            else
            {
                DiagnosticsManager.LogDetail("AimBot", "Aim target cleared.");
            }
        }

        private int FindBestTarget(Ped player, out bool isRangeObject)
        {
            Ped[] peds = World.GetAllPeds();
            Vector3 cameraPosition = Function.Call<Vector3>((Hash)0x14D6F5678D8F1B37);
            Vector3 cameraDirection = GameplayCamera.Direction;
            float bestScore = float.MinValue;
            int bestHandle = 0;
            bool bestIsRangeObject = false;
            float maxDistanceSquared = MaximumRange * MaximumRange;

            for (int i = 0; i < peds.Length; i++)
            {
                Ped candidate = peds[i];
                if (candidate == null || !candidate.Exists() || candidate.Handle == player.Handle ||
                    candidate.IsDead || !IsEnemy(candidate, player))
                    continue;

                Vector3 head = GetAimPosition(candidate);
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

            // First test the exact crosshair ray. Some static range objects are not
            // returned consistently by the SHVDN object-pool enumeration.
            RaycastResult rangeRay = World.Raycast(GameplayCamera.Position, GameplayCamera.Direction,
                MaximumRange, IntersectOptions.Everything, player);
            if (rangeRay.DitHitEntity && rangeRay.HitEntity is Prop)
            {
                Prop rayTarget = (Prop)rangeRay.HitEntity;
                if (IsRangeTarget(rayTarget))
                {
                    bestHandle = rayTarget.Handle;
                    bestIsRangeObject = true;
                    bestScore = 100.0f;
                }
            }

            // Ammu-Nation shooting-range targets are props, not peds. Only a strict
            // model whitelist is eligible so Aim Bot cannot lock onto arbitrary scenery.
            Prop[] props = bestIsRangeObject && bestScore >= 100.0f ? new Prop[0] : World.GetAllProps();
            for (int i = 0; i < props.Length; i++)
            {
                Prop candidate = props[i];
                if (candidate == null || !candidate.Exists() || !IsRangeTarget(candidate)) continue;
                Vector3 targetPosition = candidate.Position + new Vector3(0.0f, 0.0f, 0.8f);
                float dx = targetPosition.X - cameraPosition.X;
                float dy = targetPosition.Y - cameraPosition.Y;
                float dz = targetPosition.Z - cameraPosition.Z;
                float distanceSquared = dx * dx + dy * dy + dz * dz;
                if (distanceSquared <= 0.01f || distanceSquared > maxDistanceSquared) continue;
                if (!Function.Call<bool>(Hash.IS_ENTITY_ON_SCREEN, candidate.Handle) ||
                    !Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY, player.Handle, candidate.Handle, 17))
                    continue;

                float distance = (float)Math.Sqrt(distanceSquared);
                float dot = cameraDirection.X * dx / distance +
                    cameraDirection.Y * dy / distance + cameraDirection.Z * dz / distance;
                if (dot < MinimumAimDot) continue;
                float score = dot * 3.0f - distance / MaximumRange;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestHandle = candidate.Handle;
                    bestIsRangeObject = true;
                }
            }

            isRangeObject = bestIsRangeObject;
            return bestHandle;
        }

        private bool IsRangeTarget(Prop prop)
        {
            if (prop == null || !prop.Exists()) return false;
            int modelHash = prop.Model.Hash;
            for (int i = 0; i < RangeTargetModelHashes.Length; i++)
                if (modelHash == RangeTargetModelHashes[i]) return true;
            return false;
        }

        private bool IsEnemy(Ped candidate, Ped player)
        {
            if (candidate == null || !candidate.Exists() || candidate.Handle == player.Handle ||
                candidate.IsDead)
                return false;

            // Aim Bot is for human opponents only: explicitly reject wildlife and pets.
            if (!Function.Call<bool>((Hash)0xB980061DA992779D, candidate.Handle))
                return false;

            // Never select the player's own relationship group, current squad members,
            // or peds that the game identifies as friends.
            int playerGroup = player.CurrentPedGroup.Handle;
            if (candidate.RelationshipGroup == player.RelationshipGroup ||
                Function.Call<bool>((Hash)0x9BB01E3834671191, candidate.Handle, playerGroup) ||
                Function.Call<bool>((Hash)0x6F972C1AB75A1ED0, candidate.Handle, player.Handle))
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

        private Vector3 GetAimPosition(Ped ped)
        {
            // The head bone can be partially occluded or offset by seat animation.
            // Use the upper torso for occupants; keep head targeting on foot.
            int bone = ped.IsInVehicle() ? 24818 : HeadBone;
            Vector3 position = Function.Call<Vector3>((Hash)0x17C07FC640E86B4E,
                ped.Handle, bone, 0.0f, 0.0f, 0.0f);
            return ped.IsInVehicle() ? position + new Vector3(0.0f, 0.0f, 0.08f) : position;
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