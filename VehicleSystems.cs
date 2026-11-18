using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

namespace AvernesSowtware
{
    public sealed partial class AvernesSowtwareScript
    {
        private int _pendingGodModeRepairVehicleHandle;
        private DateTime _pendingGodModeRepairAtUtc = DateTime.MinValue;

        private void ApplyAntiEject(Ped ped)
        {
            if (!IsEntityUsable(ped)) return;
            if (!IsEntityUsable(ped) || !_settings.AntiEject || !ped.IsInVehicle())
            {
                RestoreAntiEject();
                return;
            }
            if (_antiEjectPedHandle != ped.Handle)
            {
                RestoreAntiEject();
                _antiEjectPedHandle = ped.Handle;
                _antiEjectOriginalWindshieldFlag = Function.Call<bool>((Hash)0x7EE53118C892B513UL, ped.Handle, 32, false);
                _antiEjectFlagCaptured = true;
            }
            // eKnockOffVehicle: 1 = NEVER; flag 32 prevents windscreen ejection.
            Function.Call((Hash)0x7A6535691B477C48UL, ped.Handle, 1);
            Function.Call((Hash)0x1913FE4CBF41C463UL, ped.Handle, 32, false);
        }
        private void RestoreAntiEject()
        {
            if (_antiEjectPedHandle == 0) return;
            try
            {
                if (IsPedHandleUsable(_antiEjectPedHandle))
                {
                    Function.Call((Hash)0x7A6535691B477C48UL, _antiEjectPedHandle, 0);
                    if (_antiEjectFlagCaptured)
                        Function.Call((Hash)0x1913FE4CBF41C463UL, _antiEjectPedHandle, 32, _antiEjectOriginalWindshieldFlag);
                }
            }
            catch (Exception ex) { LogException("RestoreAntiEject", ex); }
            _antiEjectPedHandle = 0;
            _antiEjectFlagCaptured = false;
        }
        private void ApplyVehicleSettings(Ped ped)
        {
            if (!IsEntityUsable(ped)) return;
            int currentHandle = 0;
            if (ped.IsInVehicle())
                currentHandle = Function.Call<int>((Hash)0x9A9112A0FE9A4713UL, ped.Handle, false); // GET_VEHICLE_PED_IS_IN(ped, lastVehicle=false)
            if (currentHandle == 0 && ped.IsInVehicle())
            {
                Vehicle fallbackVehicle = ped.CurrentVehicle;
                if (IsEntityUsable(fallbackVehicle)) currentHandle = fallbackVehicle.Handle;
            }
            if (currentHandle != 0 && !IsVehicleHandleUsable(currentHandle)) currentHandle = 0;

            if (_lastProtectedVehicleHandle != 0 && _lastProtectedVehicleHandle != currentHandle)
            {
                RestoreVehicleProtection(_lastProtectedVehicleHandle);
                _lastProtectedVehicleHandle = 0;
            }

            if (currentHandle == 0)
            {
                _pendingGodModeRepairVehicleHandle = 0;
                _pendingGodModeRepairAtUtc = DateTime.MinValue;
                _lastAppliedVehicleSettingsHandle = 0;
                _vehicleSettingsCacheValid = false;
                _nextVehicleSettingsRefreshUtc = DateTime.MinValue;
                return;
            }
            if (_pendingGodModeRepairVehicleHandle != 0 &&
                _pendingGodModeRepairVehicleHandle != currentHandle)
            {
                _pendingGodModeRepairVehicleHandle = 0;
                _pendingGodModeRepairAtUtc = DateTime.MinValue;
            }

            bool godMode = _settings.VehicleGodMode;
            bool protectWheels = godMode || _settings.BulletproofTires;
            bool wasGodModeAlreadyApplied = _vehicleSettingsCacheValid &&
                _lastAppliedVehicleSettingsHandle == currentHandle && _lastVehicleGodModeSetting;
            if (_vehicleSettingsCacheValid && _lastAppliedVehicleSettingsHandle == currentHandle &&
                _lastVehicleGodModeSetting == godMode &&
                _lastVehicleBulletproofTiresSetting == _settings.BulletproofTires &&
                DateTime.UtcNow < _nextVehicleSettingsRefreshUtc)
                return;

            Function.Call(Hash.SET_ENTITY_INVINCIBLE, currentHandle, godMode);
            Function.Call((Hash)0xFAEE099C6F890BB8UL, currentHandle,
                godMode, godMode, godMode, godMode, godMode, godMode, godMode, godMode); // SET_ENTITY_PROOFS
            Function.Call((Hash)0x1760FFA8AB074D66UL, currentHandle, !godMode); // SET_ENTITY_CAN_BE_DAMAGED
            Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, currentHandle, !godMode);
            if (godMode)
            {
                // Protection is applied immediately, but full repair is deferred while
                // the player is entering. SET_VEHICLE_FIXED can reset door state and
                // interrupt GTA's native entry animation even when called only once.
                bool playerEnteringVehicle = Function.Call<bool>((Hash)0xBB062B2B5722478EUL, ped.Handle); // IS_PED_GETTING_INTO_A_VEHICLE
                if (!wasGodModeAlreadyApplied && playerEnteringVehicle)
                {
                    _pendingGodModeRepairVehicleHandle = currentHandle;
                    _pendingGodModeRepairAtUtc = DateTime.UtcNow.AddMilliseconds(1200);
                    Log("Vehicle God Mode repair deferred until the entry animation completes. Vehicle=" + currentHandle + ".");
                }
                else if (!wasGodModeAlreadyApplied)
                {
                    RepairVehicleForGodMode(currentHandle);
                    _pendingGodModeRepairVehicleHandle = 0;
                    _pendingGodModeRepairAtUtc = DateTime.MinValue;
                }
                else if (_pendingGodModeRepairVehicleHandle == currentHandle &&
                    DateTime.UtcNow >= _pendingGodModeRepairAtUtc && !playerEnteringVehicle)
                {
                    RepairVehicleForGodMode(currentHandle);
                    _pendingGodModeRepairVehicleHandle = 0;
                    _pendingGodModeRepairAtUtc = DateTime.MinValue;
                }
            }
            else if (_pendingGodModeRepairVehicleHandle == currentHandle)
            {
                _pendingGodModeRepairVehicleHandle = 0;
                _pendingGodModeRepairAtUtc = DateTime.MinValue;
            }
            Function.Call((Hash)0x3E8C8727991A8A0BUL, currentHandle, godMode); // SET_VEHICLE_STRONG
            Function.Call((Hash)0x71B0892EC081D60AUL, currentHandle, !godMode); // SET_VEHICLE_EXPLODES_ON_HIGH_EXPLOSION_DAMAGE
            Function.Call((Hash)0xD565F438137F0E10UL, currentHandle, !godMode); // SET_VEHICLE_EXPLODES_ON_EXPLOSION_DAMAGE_AT_ZERO_BODY_HEALTH
            Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, currentHandle, !protectWheels);
            Function.Call(Hash.SET_VEHICLE_WHEELS_CAN_BREAK, currentHandle, !protectWheels);
            if (godMode)
            {
                Function.Call((Hash)0x59BF8C3D52C92F66UL, currentHandle, false); // SET_VEHICLE_CAN_BREAK
                Function.Call((Hash)0x983765856F2564F9UL, currentHandle, false); // SET_VEHICLE_ENGINE_CAN_DEGRADE
                Function.Call((Hash)0xA37B9A517B133349UL, currentHandle, false); // SET_VEHICLE_WHEELS_CAN_BREAK_OFF_WHEN_BLOW_UP
            }
            else
            {
                Function.Call((Hash)0x59BF8C3D52C92F66UL, currentHandle, true);
                Function.Call((Hash)0x983765856F2564F9UL, currentHandle, true);
                Function.Call((Hash)0xA37B9A517B133349UL, currentHandle, true);
            }

            _lastProtectedVehicleHandle = protectWheels || godMode ? currentHandle : 0;
            _lastAppliedVehicleSettingsHandle = currentHandle;
            _lastVehicleGodModeSetting = godMode;
            _lastVehicleBulletproofTiresSetting = _settings.BulletproofTires;
            _vehicleSettingsCacheValid = true;
            // Re-assert at a low rate so Story Mode missions/cutscenes that reset
            // entity proofs cannot permanently cancel protection for this vehicle.
            _nextVehicleSettingsRefreshUtc = DateTime.UtcNow.AddMilliseconds(250);
        }

        private void RepairVehicleForGodMode(int vehicleHandle)
        {
            if (!IsVehicleHandleUsable(vehicleHandle)) return;
            Function.Call(Hash.SET_VEHICLE_FIXED, vehicleHandle);
            Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicleHandle);
            for (int windowIndex = 0; windowIndex < 8; windowIndex++)
                Function.Call((Hash)0x772282EBEB95E682UL, vehicleHandle, windowIndex); // FIX_VEHICLE_WINDOW
            Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, vehicleHandle, false);
            Log("Vehicle God Mode initial repair completed. Vehicle=" + vehicleHandle + ".");
        }

        private void RestoreVehicleProtection(int vehicleHandle)
        {
            if (vehicleHandle == 0) return;
            try
            {
                if (!IsVehicleHandleUsable(vehicleHandle)) return;
                Function.Call(Hash.SET_ENTITY_INVINCIBLE, vehicleHandle, false);
                Function.Call((Hash)0xFAEE099C6F890BB8UL, vehicleHandle,
                    false, false, false, false, false, false, false, false); // SET_ENTITY_PROOFS
                Function.Call((Hash)0x1760FFA8AB074D66UL, vehicleHandle, true); // SET_ENTITY_CAN_BE_DAMAGED
                Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, vehicleHandle, true);
                Function.Call((Hash)0x3E8C8727991A8A0BUL, vehicleHandle, false); // SET_VEHICLE_STRONG
                Function.Call((Hash)0x71B0892EC081D60AUL, vehicleHandle, true);
                Function.Call((Hash)0xD565F438137F0E10UL, vehicleHandle, true);
                Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, vehicleHandle, true);
                Function.Call(Hash.SET_VEHICLE_WHEELS_CAN_BREAK, vehicleHandle, true);
                Function.Call((Hash)0x59BF8C3D52C92F66UL, vehicleHandle, true); // SET_VEHICLE_CAN_BREAK
                Function.Call((Hash)0x983765856F2564F9UL, vehicleHandle, true); // SET_VEHICLE_ENGINE_CAN_DEGRADE
                Function.Call((Hash)0xA37B9A517B133349UL, vehicleHandle, true); // SET_VEHICLE_WHEELS_CAN_BREAK_OFF_WHEN_BLOW_UP
            }
            catch (Exception ex)
            {
                LogException("RestoreVehicleProtection", ex);
            }
        }
        private void ProcessVehicleRocketFire(Ped ped)
        {
            if (!IsEntityUsable(ped)) return;
            if (!_vehicleRocketFirePending) return;
            _vehicleRocketFirePending = false;
            if (!_settings.VehicleRockets || !IsEntityUsable(ped) || !ped.IsInVehicle()) return;
            DateTime now = DateTime.UtcNow;
            if (now < _lastVehicleRocketUtc.AddMilliseconds(180)) return;
            Vehicle vehicle = ped.CurrentVehicle;
            if (!IsEntityUsable(vehicle)) return;
            int vehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, vehicle.Handle);
            if (vehicleClass != 15) return; // Keep the rocket feature exclusive to helicopters.

            uint rocketWeapon = GetHash("WEAPON_VEHICLE_ROCKET");
            if (!Function.Call<bool>((Hash)0x36E353271F0E90EEUL, rocketWeapon))
            {
                Function.Call((Hash)0x5443438F033E29C3UL, rocketWeapon, 31, 0); // REQUEST_WEAPON_ASSET
                if (!_vehicleRocketAssetRequested)
                {
                    Log("Requested WEAPON_VEHICLE_ROCKET asset for vehicle rocket mode.");
                    _vehicleRocketAssetRequested = true;
                }
                return;
            }

            int handle = vehicle.Handle;
            Vector3 leftFrom = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, handle, -0.35f, 2.5f, 0.35f);
            Vector3 rightFrom = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, handle, 0.35f, 2.5f, 0.35f);
            Vector3 leftTo = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, handle, -0.35f, 130.0f, 0.35f);
            Vector3 rightTo = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, handle, 0.35f, 130.0f, 0.35f);
            Function.Call((Hash)0x867654CBC7606F2CUL,
                leftFrom.X, leftFrom.Y, leftFrom.Z, leftTo.X, leftTo.Y, leftTo.Z,
                250, true, rocketWeapon, ped.Handle, true, false, -1.0f);
            Function.Call((Hash)0x867654CBC7606F2CUL,
                rightFrom.X, rightFrom.Y, rightFrom.Z, rightTo.X, rightTo.Y, rightTo.Z,
                250, true, rocketWeapon, ped.Handle, true, false, -1.0f);
            _lastVehicleRocketUtc = now;
            Log("Vehicle rocket pair fired. Model=" + vehicle.Model.Hash + "; entity=" + handle + ".");
        }
        private void ApplyLegacyVehicleEffects(Ped ped)
        {
            if (!IsEntityUsable(ped))
            {
                RestoreNitroSpeedLimit();
                return;
            }
            if (!ped.IsInVehicle() || !IsEntityUsable(ped.CurrentVehicle))
            {
                RestoreNitroSpeedLimit();
                return;
            }

            Vehicle vehicle = ped.CurrentVehicle;
            int handle = vehicle.Handle;
            if (_nitroVehicleHandle != 0 && _nitroVehicleHandle != handle) RestoreNitroSpeedLimit();
            Function.Call((Hash)0x222FF6A823D122E2UL, handle, _settings.DriftMode);
            // Nitro belongs to the player-controlled vehicle only. A passenger must
            // never change the speed/handling of an NPC-driven aircraft or vehicle.
            bool playerIsDriver = Function.Call<int>((Hash)0xBB40DD2270B65366UL, handle, -1) == ped.Handle;
            bool nitroRequested = _settings.Nitro && IsControlPressed(21) && playerIsDriver;
            if (_settings.Nitro && IsControlPressed(21) && !playerIsDriver)
                RestoreNitroSpeedLimit();
            if (nitroRequested)
            {
                bool hasTrailer = Function.Call<bool>((Hash)0xE7CF3C4F9F489F0CUL, handle);
                if (hasTrailer)
                {
                    if (!_nitroTrailerWarningShown)
                        Notify("Nitro is disabled while towing a trailer for stability.");
                    _nitroTrailerWarningShown = true;
                    RestoreNitroSpeedLimit();
                }
                else
                {
                    _nitroTrailerWarningShown = false;
                    float nitroPower = ClampFloat(_settings.NitroPower, 0.5f, 8.0f);
                    float currentSpeed = Function.Call<float>(Hash.GET_ENTITY_SPEED, handle);
                    float maximumNitroSpeed = CalculateNitroMaximumSpeedMetersPerSecond(nitroPower);

                    // SET_VEHICLE_MAX_SPEED alone does not override the vehicle's
                    // handling/transmission ceiling. Raise its handling top-speed once
                    // per boost session, using the native estimated maximum as baseline.
                    if (_nitroVehicleHandle != handle)
                    {
                        float baseMaximumSpeed = Function.Call<float>((Hash)0x53AF99BAA671CA47UL, handle); // GET_VEHICLE_ESTIMATED_MAX_SPEED
                        if (baseMaximumSpeed < 1.0f)
                            baseMaximumSpeed = Function.Call<float>((Hash)0xF417C2502FFFED43UL, Function.Call<uint>(Hash.GET_ENTITY_MODEL, handle)); // GET_VEHICLE_MODEL_ESTIMATED_MAX_SPEED
                        if (baseMaximumSpeed < 1.0f) baseMaximumSpeed = 50.0f;
                        float topSpeedIncreasePercent = ClampFloat(((maximumNitroSpeed / baseMaximumSpeed) - 1.0f) * 100.0f, 0.0f, 3000.0f);
                        Function.Call((Hash)0x93A3996368C94158UL, handle, topSpeedIncreasePercent); // MODIFY_VEHICLE_TOP_SPEED
                        _nitroVehicleHandle = handle;
                        Log("Nitro handling boost applied: vehicle=" + handle + "; estimatedBase=" + baseMaximumSpeed.ToString("0.00", CultureInfo.InvariantCulture) + " m/s; target=" + maximumNitroSpeed.ToString("0.00", CultureInfo.InvariantCulture) + " m/s; increase=" + topSpeedIncreasePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%.");
                    }

                    // This torque native is frame-based. 1.8 is the documented upper
                    // value used by GTA scripts; the additional force closes the gap
                    // between the current speed and the 1200 km/h x8 target.
                    Function.Call((Hash)0xB59E4BD37AE292DBUL, handle, 1.8f); // SET_VEHICLE_CHEAT_POWER_INCREASE
                    Function.Call((Hash)0xBAA045B4E42F3C06UL, handle, maximumNitroSpeed); // SET_VEHICLE_MAX_SPEED
                    if (currentSpeed < maximumNitroSpeed)
                    {
                        float deficit = ClampFloat((maximumNitroSpeed - currentSpeed) / maximumNitroSpeed, 0.0f, 1.0f);
                        float nitroForce = 60.0f * nitroPower * (1.0f + deficit);
                        Function.Call((Hash)0x18FF00FC7EFF559EUL, handle,
                            1, 0.0f, nitroForce, 0.0f, false, true, true, false);
                    }

                    // Stock handling caps many vehicles near their normal redline even
                    // after MODIFY_VEHICLE_TOP_SPEED/SET_VEHICLE_MAX_SPEED. Apply a
                    // controlled forward-velocity increment while boost is held; this
                    // bypasses the gearbox ceiling but preserves steering and vertical motion.
                    ApplyNitroVelocityAssist(handle, nitroPower, maximumNitroSpeed);
                }
            }
            else
            {
                _nitroTrailerWarningShown = false;
                RestoreNitroSpeedLimit();
            }

            if (_settings.HornJump && Function.Call<bool>((Hash)0x580417101DDB492FUL, 0, 86))
                Function.Call((Hash)0xC5F68BE9613E2D18UL, handle, 1, 0.0f, 0.0f, 8.5f, 0.0f, 0.0f, 0.0f, 0, false, true, true, false, true);
        }

        private void ApplyNitroVelocityAssist(int vehicleHandle, float nitroPower, float maximumSpeed)
        {
            if (vehicleHandle == 0 || !IsVehicleHandleUsable(vehicleHandle)) return;
            DateTime now = DateTime.UtcNow;
            double elapsed = _lastNitroAccelerationUtc == DateTime.MinValue
                ? 0.016
                : (now - _lastNitroAccelerationUtc).TotalSeconds;
            _lastNitroAccelerationUtc = now;
            float deltaSeconds = ClampFloat((float)elapsed, 0.005f, 0.05f);

            Vector3 forward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, vehicleHandle);
            float forwardLength = (float)Math.Sqrt(forward.X * forward.X + forward.Y * forward.Y);
            if (forwardLength < 0.001f) return;
            float forwardX = forward.X / forwardLength;
            float forwardY = forward.Y / forwardLength;

            Vector3 velocity = Function.Call<Vector3>(Hash.GET_ENTITY_VELOCITY, vehicleHandle);
            float forwardSpeed = velocity.X * forwardX + velocity.Y * forwardY;
            float acceleration = 45.0f + ClampFloat(nitroPower, 0.5f, 8.0f) * 5.0f;
            float nextForwardSpeed = ClampFloat(forwardSpeed, 0.0f, maximumSpeed);
            if (nextForwardSpeed < maximumSpeed)
                nextForwardSpeed = Math.Min(maximumSpeed, nextForwardSpeed + acceleration * deltaSeconds);
            else
                return;

            float lateralX = velocity.X - forwardX * forwardSpeed;
            float lateralY = velocity.Y - forwardY * forwardSpeed;
            Function.Call(Hash.SET_ENTITY_VELOCITY, vehicleHandle,
                forwardX * nextForwardSpeed + lateralX,
                forwardY * nextForwardSpeed + lateralY,
                velocity.Z);
        }

        private void RestoreNitroSpeedLimit()
        {
            int handle = _nitroVehicleHandle;
            _nitroVehicleHandle = 0;
            _lastNitroAccelerationUtc = DateTime.MinValue;
            if (handle == 0) return;
            try
            {
                if (DoesEntityHandleExist(handle))
                {
                    Function.Call((Hash)0xBAA045B4E42F3C06UL, handle, 0.0f); // Reset vehicle maximum speed.
                    Function.Call((Hash)0x93A3996368C94158UL, handle, 0.0f); // Reset handling/transmission top-speed modification.
                    Function.Call((Hash)0xB59E4BD37AE292DBUL, handle, 1.0f); // Restore default engine torque.
                }
            }
            catch (Exception ex) { LogException("Restore nitro speed limit", ex); }
        }

        private static float CalculateNitroMaximumSpeedMetersPerSecond(float nitroPower)
        {
            // 150 km/h per power step: x3 = 450 km/h; x8 = 1200 km/h.
            return ClampFloat(nitroPower, 0.5f, 8.0f) * (150.0f / 3.6f);
        }

        private void DrawSpeedometer(Ped ped)
        {
            if (!IsEntityUsable(ped) || !ped.IsInVehicle()) return;
            Vehicle vehicle = ped.CurrentVehicle;
            if (!IsEntityUsable(vehicle)) return;
            // GET_ENTITY_SPEED returns GTA's authoritative world-speed magnitude
            // in m/s. Using it avoids under-reading on slopes and jumps (the previous
            // XY-only vector calculation discarded vertical velocity).
            float speed = Function.Call<float>(Hash.GET_ENTITY_SPEED, vehicle.Handle);
            string unit = _settings.SpeedUnitMph ? "MPH" : "KM/H";
            speed *= _settings.SpeedUnitMph ? 2.236936f : 3.6f;
            if (Single.IsNaN(speed) || Single.IsInfinity(speed)) speed = 0.0f;
            speed = Math.Max(0.0f, speed);
            DrawText(speed.ToString("0.0", CultureInfo.InvariantCulture) + " " + unit, 0.875f, 0.91f, 0.42f, 255, 255, 255, 235);
        }
        private bool TryGetCurrentVehicle(out Vehicle vehicle)
        {
            Ped ped = Game.Player.Character;
            vehicle = IsEntityUsable(ped) && ped.IsInVehicle() ? ped.CurrentVehicle : null;
            if (!IsEntityUsable(vehicle))
            {
                Notify("Enter a vehicle first.");
                return false;
            }
            return true;
        }
        private void MaxTuneVehicle()
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;
            if (!IsEntityUsable(vehicle))
            {
                Notify("Current vehicle is no longer available.");
                return;
            }

            int handle = vehicle.Handle;
            int supportedSlots = 0;
            int appliedSlots = 0;
            int verifiedSlots = 0;
            int failedSlots = 0;
            int toggleAttempts = 0;
            int verifiedToggles = 0;
            bool tintApplied = false;
            bool neonApplied = false;
            bool wheelsApplied = false;
            try
            {
                Function.Call((Hash)0x1F2AA07F00B3217AUL, handle, 0); // SET_VEHICLE_MOD_KIT

                int vehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, handle); // GET_VEHICLE_CLASS
                bool antiAircraftTrailer = IsAntiAircraftTrailer(handle);
                if (antiAircraftTrailer)
                {
                    RestoreAntiAircraftTrailerWheels(handle);
                    Log("Max tuning: skipped custom wheel selection for the anti-aircraft trailer to preserve factory wheel proportions.");
                }
                else
                {
                    wheelsApplied = ApplyClassAppropriateWheels(handle, vehicleClass, ref supportedSlots, ref appliedSlots, ref verifiedSlots, ref failedSlots);
                }

                // Standard appearance and performance slots. Wheels are handled above
                // because their available options depend on the selected wheel family.
                bool isNebula = Function.Call<uint>(Hash.GET_ENTITY_MODEL, handle) == GetHash("nebula");
                for (int modType = 0; modType <= 49; modType++)
                {
                    if (modType == 18 || modType == 20 || modType == 22 || modType == 23 || modType == 24 ||
                        (isNebula && modType == 7)) continue;
                    try
                    {
                        int count = Function.Call<int>((Hash)0xE38E9162A2500646UL, handle, modType);
                        if (count <= 0) continue;
                        supportedSlots++;
                        int bestIndex = count - 1;
                        Function.Call((Hash)0x6AF0636DDEDCB6DDUL, handle, modType, bestIndex, false);
                        appliedSlots++;
                        int currentIndex = Function.Call<int>((Hash)0x772960298DA26FDBUL, handle, modType);
                        if (currentIndex == bestIndex) verifiedSlots++; else failedSlots++;
                    }
                    catch (Exception slotException)
                    {
                        failedSlots++;
                        LogException("MaxTuneVehicle slot " + modType, slotException);
                    }
                }

                int[] toggleModTypes = new int[] { 18, 20, 22 };
                for (int i = 0; i < toggleModTypes.Length; i++)
                {
                    int modType = toggleModTypes[i];
                    try
                    {
                        Function.Call((Hash)0x2A1F4F37F95BAD08UL, handle, modType, true); // TOGGLE_VEHICLE_MOD
                        toggleAttempts++;
                        if (Function.Call<bool>((Hash)0x84B233A8C8FC8AE7UL, handle, modType)) verifiedToggles++;
                        else failedSlots++;
                    }
                    catch (Exception toggleException)
                    {
                        failedSlots++;
                        LogException("MaxTuneVehicle toggle " + modType, toggleException);
                    }
                }

                ApplyMattePaintPreservingColor(handle);

                // Window tint index 1 is Pure Black (darker than Limo index 5).
                // Read the value back so unsupported vehicle glass is reported honestly.
                try
                {
                    const int maximumPureBlackTint = 1;
                    Function.Call((Hash)0x57C51E6BAD752696UL, handle, maximumPureBlackTint); // SET_VEHICLE_WINDOW_TINT
                    tintApplied = Function.Call<int>((Hash)0x0EE21293DAD47C95UL, handle) == maximumPureBlackTint; // GET_VEHICLE_WINDOW_TINT
                }
                catch (Exception tintException) { LogException("MaxTuneVehicle pure black tint", tintException); }

                bool plateApplied = false;
                try
                {
                    Function.Call((Hash)0x9088EB5A43FFB0A1UL, handle, 1); // SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX: Yellow/black plate style
                    plateApplied = Function.Call<int>((Hash)0xF11BC2DD9A3E7195UL, handle) == 1;
                }
                catch (Exception plateException) { LogException("MaxTuneVehicle yellow/black plate", plateException); }

                try
                {
                    for (int side = 0; side < 4; side++)
                        Function.Call((Hash)0x2AA720E4287BF269UL, handle, side, true);
                    Function.Call((Hash)0x8E0A582209A62695UL, handle, 255, 0, 0);
                    neonApplied = true;
                }
                catch (Exception neonException) { LogException("MaxTuneVehicle neon", neonException); }

                // Make tires bulletproof independently from the optional vehicle god mode.
                Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, handle, false);
                Function.Call(Hash.SET_VEHICLE_WHEELS_CAN_BREAK, handle, false);
                _settings.BulletproofTires = true;
                SaveSettings();

                bool rocketModeEnabled = false;
                if (vehicleClass == 15)
                {
                    _settings.VehicleRockets = true;
                    SaveSettings();
                    rocketModeEnabled = true;
                }

                // Texture liveries use a separate native from mod-kit liveries (slot 48).
                try
                {
                    int liveryCount = Function.Call<int>((Hash)0x87B63E25A529D526UL, handle);
                    if (liveryCount > 0)
                    {
                        Function.Call((Hash)0x60BF608F1B8CD1B6UL, handle, liveryCount - 1);
                        appliedSlots++;
                        supportedSlots++;
                    }
                }
                catch (Exception liveryException) { LogException("MaxTuneVehicle texture livery", liveryException); }

                int totalVerified = verifiedSlots + verifiedToggles;
                int totalAttempts = appliedSlots + toggleAttempts;
                Log("Max tuning result: model=" + vehicle.Model.Hash + ", class=" + (vehicleClass == 8 ? "motorcycle" : "other") + ", supported slots=" + supportedSlots +
                    ", attempts=" + totalAttempts + ", verified=" + totalVerified + ", failed/unsupported=" + failedSlots +
                    ", class-appropriate wheels=" + wheelsApplied + ", pure black tint=" + tintApplied +
                    ", yellow/black plate=" + plateApplied + ", red neon=" + neonApplied + ", bulletproof tires=true, vehicle rocket mode=" + rocketModeEnabled + ".");

                Notify("Max Tuning applied: " + totalVerified + "/" + totalAttempts + " parts verified; wheels " +
                    (wheelsApplied ? "yes" : (antiAircraftTrailer ? "factory wheels preserved" : "not available")) + ", pure black tint " + (tintApplied ? "yes" : "unsupported") +
                    ", yellow/black plate " + (plateApplied ? "yes" : "unsupported") + ", red neon " + (neonApplied ? "ON" : "unsupported") + ", bulletproof tires ON" +
                    (rocketModeEnabled ? ". Vehicle rockets enabled (Numpad +)." : "."));
            }
            catch (Exception ex)
            {
                LogException("MaxTuneVehicle", ex);
                NotifyError("Max Tuning");
            }
        }
        private void ApplyAllVehicleBodyKits()
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;

            int[] cosmeticSlots = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int available = 0;
            int applied = 0;
            int unsupported = 0;
            int failed = 0;
            int handle = vehicle.Handle;
            try
            {
                Function.Call((Hash)0x1F2AA07F00B3217AUL, handle, 0); // SET_VEHICLE_MOD_KIT
                for (int i = 0; i < cosmeticSlots.Length; i++)
                {
                    int slot = cosmeticSlots[i];
                    try
                    {
                        int count = Function.Call<int>((Hash)0xE38E9162A2500646UL, handle, slot);
                        if (count <= 0)
                        {
                            unsupported++;
                            continue;
                        }
                        available++;
                        int best = count - 1;
                        Function.Call((Hash)0x6AF0636DDEDCB6DDUL, handle, slot, best, false); // SET_VEHICLE_MOD
                        int current = Function.Call<int>((Hash)0x772960298DA26FDBUL, handle, slot); // GET_VEHICLE_MOD
                        if (current == best) applied++;
                        else failed++;
                    }
                    catch (Exception slotException)
                    {
                        failed++;
                        LogException("ApplyAllVehicleBodyKits slot " + slot, slotException);
                    }
                }
                Log("Body kit result: model=" + vehicle.Model.Hash + "; supported=" + available +
                    "; applied=" + applied + "; unsupported=" + unsupported + "; failed=" + failed + ".");
                Notify("Обвесы: установлено " + applied + "/" + available + "; неподдерживаемых слотов: " + unsupported +
                    (failed > 0 ? "; ошибок: " + failed : String.Empty) + ".");
            }
            catch (Exception ex)
            {
                LogException("ApplyAllVehicleBodyKits", ex);
                NotifyError("Установка обвесов");
            }
        }

        private static string GetWheelTypeName(int wheelType)
        {
            string[] names = new string[] { "Sport", "Muscle", "Lowrider", "SUV", "Off-road", "Tuner", "Bike", "High End", "Benny's Original", "Benny's Bespoke", "Open Wheel", "Street", "Track" };
            return wheelType >= 0 && wheelType < names.Length ? names[wheelType] : "Unknown";
        }

        private string GetCurrentVehicleWheelTypeLabel()
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return "Нет транспорта";
            try
            {
                int wheelType = Function.Call<int>((Hash)0xB3ED1BFB4BE636DCUL, vehicle.Handle); // GET_VEHICLE_WHEEL_TYPE
                return GetWheelTypeName(wheelType) + " (" + wheelType.ToString(CultureInfo.InvariantCulture) + ")";
            }
            catch (Exception ex)
            {
                LogException("GetCurrentVehicleWheelTypeLabel", ex);
                return "Недоступно";
            }
        }

        private void CycleVehicleWheelType(int direction)
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;
            if (direction == 0) return;
            int handle = vehicle.Handle;
            if (IsAntiAircraftTrailer(handle))
            {
                RestoreAntiAircraftTrailerWheels(handle);
                Notify("Factory wheels are fixed on the anti-aircraft trailer.");
                return;
            }
            try
            {
                Function.Call((Hash)0x1F2AA07F00B3217AUL, handle, 0); // SET_VEHICLE_MOD_KIT
                int current = Function.Call<int>((Hash)0xB3ED1BFB4BE636DCUL, handle);
                int vehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, handle);
                for (int step = 1; step <= 13; step++)
                {
                    int candidate = (current + (direction > 0 ? step : -step)) % 13;
                    if (candidate < 0) candidate += 13;
                    Function.Call((Hash)0x487EB21CC7295BA1UL, handle, candidate); // SET_VEHICLE_WHEEL_TYPE
                    int frontCount = Function.Call<int>((Hash)0xE38E9162A2500646UL, handle, 23);
                    int rearCount = vehicleClass == 8
                        ? Function.Call<int>((Hash)0xE38E9162A2500646UL, handle, 24) : 0;
                    if (frontCount <= 0 && rearCount <= 0) continue;
                    if (frontCount > 0)
                        Function.Call((Hash)0x6AF0636DDEDCB6DDUL, handle, 23, frontCount - 1, false);
                    if (rearCount > 0)
                        Function.Call((Hash)0x6AF0636DDEDCB6DDUL, handle, 24, rearCount - 1, false);
                    Notify("Тип дисков: " + GetWheelTypeName(candidate) + ".");
                    return;
                }
                Function.Call((Hash)0x487EB21CC7295BA1UL, handle, current);
                Notify("Для этого транспорта нет других доступных семейств дисков.");
            }
            catch (Exception ex)
            {
                LogException("CycleVehicleWheelType", ex);
                NotifyError("Выбор семейства дисков");
            }
        }

        private string GetCurrentVehicleWheelStyleLabel()
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return "Нет транспорта";
            try
            {
                Function.Call((Hash)0x1F2AA07F00B3217AUL, vehicle.Handle, 0);
                int vehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, vehicle.Handle);
                int frontCount = Function.Call<int>((Hash)0xE38E9162A2500646UL, vehicle.Handle, 23);
                int front = frontCount > 0 ? Function.Call<int>((Hash)0x772960298DA26FDBUL, vehicle.Handle, 23) : -2;
                if (vehicleClass == 8)
                {
                    int rearCount = Function.Call<int>((Hash)0xE38E9162A2500646UL, vehicle.Handle, 24);
                    int rear = rearCount > 0 ? Function.Call<int>((Hash)0x772960298DA26FDBUL, vehicle.Handle, 24) : -2;
                    return "Переднее: " + FormatVehicleModIndex(front, frontCount) + "; заднее: " + FormatVehicleModIndex(rear, rearCount);
                }
                return frontCount > 0 ? FormatVehicleModIndex(front, frontCount) : "Нет доступных дисков";
            }
            catch (Exception ex)
            {
                LogException("GetCurrentVehicleWheelStyleLabel", ex);
                return "Недоступно";
            }
        }

        private string FormatVehicleModIndex(int current, int count)
        {
            if (count <= 0 || current == -2) return "N/A";
            return current < 0 ? "Stock" : (current + 1).ToString(CultureInfo.InvariantCulture) + "/" + count.ToString(CultureInfo.InvariantCulture);
        }

        private void CycleVehicleWheelStyle(int direction)
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;
            if (direction == 0) return;
            if (IsAntiAircraftTrailer(vehicle.Handle))
            {
                RestoreAntiAircraftTrailerWheels(vehicle.Handle);
                Notify("Factory wheels are fixed on the anti-aircraft trailer.");
                return;
            }
            try
            {
                int handle = vehicle.Handle;
                Function.Call((Hash)0x1F2AA07F00B3217AUL, handle, 0);
                int vehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, handle);
                int[] slots = vehicleClass == 8 ? new int[] { 23, 24 } : new int[] { 23 };
                int changed = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    int slot = slots[i];
                    int count = Function.Call<int>((Hash)0xE38E9162A2500646UL, handle, slot);
                    if (count <= 0) continue;
                    int current = Function.Call<int>((Hash)0x772960298DA26FDBUL, handle, slot);
                    int next = current + (direction > 0 ? 1 : -1);
                    if (next >= count) next = -1;
                    if (next < -1) next = count - 1;
                    Function.Call((Hash)0x6AF0636DDEDCB6DDUL, handle, slot, next, false);
                    changed++;
                }
                Notify(changed > 0 ? "Стиль дисков обновлён." : "У этого транспорта нет сменных дисков.");
            }
            catch (Exception ex)
            {
                LogException("CycleVehicleWheelStyle", ex);
                NotifyError("Выбор стиля дисков");
            }
        }

        private string GetCurrentVehicleModLabel(int modType)
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return "Нет транспорта";
            try
            {
                Function.Call((Hash)0x1F2AA07F00B3217AUL, vehicle.Handle, 0);
                int count = Function.Call<int>((Hash)0xE38E9162A2500646UL, vehicle.Handle, modType);
                int current = count > 0 ? Function.Call<int>((Hash)0x772960298DA26FDBUL, vehicle.Handle, modType) : -2;
                return FormatVehicleModIndex(current, count);
            }
            catch (Exception ex)
            {
                LogException("GetCurrentVehicleModLabel slot " + modType, ex);
                return "Недоступно";
            }
        }

        private void CycleVehicleMod(int modType, int direction, string label)
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;
            if (direction == 0) return;
            try
            {
                int handle = vehicle.Handle;
                Function.Call((Hash)0x1F2AA07F00B3217AUL, handle, 0);
                int count = Function.Call<int>((Hash)0xE38E9162A2500646UL, handle, modType);
                if (count <= 0)
                {
                    Notify(label + ": для этой модели детали отсутствуют.");
                    return;
                }
                int current = Function.Call<int>((Hash)0x772960298DA26FDBUL, handle, modType);
                int next = current + (direction > 0 ? 1 : -1);
                if (next >= count) next = -1;
                if (next < -1) next = count - 1;
                Function.Call((Hash)0x6AF0636DDEDCB6DDUL, handle, modType, next, false);
                int actual = Function.Call<int>((Hash)0x772960298DA26FDBUL, handle, modType);
                Notify(label + ": " + FormatVehicleModIndex(actual, count) + ".");
            }
            catch (Exception ex)
            {
                LogException("CycleVehicleMod slot " + modType, ex);
                NotifyError(label);
            }
        }

        private int GetWheelTypeForVehicleClass(int vehicleClass)
        {
            // GTA wheel families: Sport=0, Muscle=1, SUV=3, Off-road=4,
            // Tuner=5, Bike=6, High End=7, Open Wheel=10.
            switch (vehicleClass)
            {
                case 0: case 1: case 3: case 11: return 5;
                case 12: return 3; // Vans
                case 2: case 17: case 18: return 3;       // SUVs, service, emergency
                case 4: return 1;                         // Muscle
                case 5: return 0;                         // Sports classics
                case 6: case 7: return 7;                 // Sports and super
                case 8: return 6;                         // Motorcycles
                case 9: case 10: case 19: case 20: return 4; // Off-road, industrial, military, commercial
                case 22: return 10;                       // Open wheel
                default: return -1;                       // Boats/aircraft/trains/unsupported
            }
        }

        private bool IsAntiAircraftTrailer(int handle)
        {
            if (!IsVehicleHandleUsable(handle)) return false;
            return Function.Call<uint>(Hash.GET_ENTITY_MODEL, handle) == GetHash("trailersmall2");
        }

        private void RestoreAntiAircraftTrailerWheels(int handle)
        {
            if (!IsAntiAircraftTrailer(handle)) return;
            try
            {
                Function.Call((Hash)0x1F2AA07F00B3217AUL, handle, 0); // SET_VEHICLE_MOD_KIT
                int frontCount = Function.Call<int>((Hash)0xE38E9162A2500646UL, handle, 23);
                if (frontCount > 0)
                    Function.Call((Hash)0x6AF0636DDEDCB6DDUL, handle, 23, -1, false); // Restore factory front wheel model
                int rearCount = Function.Call<int>((Hash)0xE38E9162A2500646UL, handle, 24);
                if (rearCount > 0)
                    Function.Call((Hash)0x6AF0636DDEDCB6DDUL, handle, 24, -1, false); // Restore factory rear wheel model
            }
            catch (Exception ex) { LogException("RestoreAntiAircraftTrailerWheels", ex); }
        }

        private bool ApplyClassAppropriateWheels(int handle, int vehicleClass,
            ref int supportedSlots, ref int appliedSlots, ref int verifiedSlots, ref int failedSlots)
        {
            if (IsAntiAircraftTrailer(handle)) return false;
            int wheelType = GetWheelTypeForVehicleClass(vehicleClass);
            if (wheelType < 0) return false;

            try
            {
                Function.Call((Hash)0x487EB21CC7295BA1UL, handle, wheelType); // SET_VEHICLE_WHEEL_TYPE
                bool motorcycle = vehicleClass == 8;
                bool anyApplied = false;
                int[] slots = motorcycle ? new int[] { 23, 24 } : new int[] { 23 };
                for (int i = 0; i < slots.Length; i++)
                {
                    int slot = slots[i];
                    int count = Function.Call<int>((Hash)0xE38E9162A2500646UL, handle, slot);
                    if (count <= 0) continue;
                    supportedSlots++;
                    // Choose different visual wheel positions for the major vehicle families
                    // instead of always applying the last catalogue entry to every class.
                    int classPreference = vehicleClass == 8 ? 0 :
                        (vehicleClass == 9 || vehicleClass == 10 || vehicleClass == 19 || vehicleClass == 20 ? count / 2 :
                        (vehicleClass == 4 || vehicleClass == 5 ? Math.Max(0, count - 2) : count - 1));
                    int bestIndex = Math.Max(0, Math.Min(count - 1, classPreference));
                    Function.Call((Hash)0x6AF0636DDEDCB6DDUL, handle, slot, bestIndex, motorcycle);
                    appliedSlots++;
                    bool verified = Function.Call<int>((Hash)0x772960298DA26FDBUL, handle, slot) == bestIndex;
                    if (verified) verifiedSlots++; else failedSlots++;
                    anyApplied = anyApplied || verified;
                }
                Log("Max tuning wheel selection: class=" + vehicleClass + ", wheelType=" + wheelType + ", applied=" + anyApplied + ".");
                return anyApplied;
            }
            catch (Exception ex)
            {
                failedSlots++;
                LogException("ApplyClassAppropriateWheels class " + vehicleClass, ex);
                return false;
            }
        }

        private static readonly int[] StandardVehicleColorRgb = new int[]
        {
                0x080808,
                0x0F0F0F,
                0x1C1E21,
                0x292C2E,
                0x5A5E66,
                0x777C87,
                0x515459,
                0x323B47,
                0x333333,
                0x1F2226,
                0x23292E,
                0x121110,
                0x050505,
                0x121212,
                0x2F3233,
                0x080808,
                0x121212,
                0x202224,
                0x575961,
                0x23292E,
                0x323B47,
                0x0F1012,
                0x212121,
                0x5B5D5E,
                0x888A99,
                0x697187,
                0x3B4654,
                0x690000,
                0x8A0B00,
                0x6B0000,
                0x611009,
                0x4A0A0A,
                0x470E0E,
                0x380C00,
                0x26030B,
                0x630012,
                0x802800,
                0x6E4F2D,
                0xBD4800,
                0x780000,
                0x360000,
                0xAB3F00,
                0xDE7E00,
                0x520000,
                0x8C0404,
                0x4A1000,
                0x592525,
                0x754231,
                0x210804,
                0x001207,
                0x001A0B,
                0x00211E,
                0x1F261E,
                0x003805,
                0x0B4145,
                0x418503,
                0x0F1F15,
                0x023613,
                0x162419,
                0x2A3625,
                0x455C56,
                0x000D14,
                0x001029,
                0x1C2F4F,
                0x001B57,
                0x3B4E78,
                0x272D3B,
                0x95B2DB,
                0x3E627A,
                0x1C3140,
                0x0055C4,
                0x1A182E,
                0x161629,
                0x0E316D,
                0x395A83,
                0x09142E,
                0x0F1021,
                0x152A52,
                0x324654,
                0x152563,
                0x223BA1,
                0x1F1FA1,
                0x030E2E,
                0x0F1E73,
                0x001C32,
                0x2A3754,
                0x303C5E,
                0x3B6796,
                0xF5890F,
                0xD9A600,
                0x4A341B,
                0xA2A827,
                0x568F00,
                0x57514B,
                0x291B06,
                0x262117,
                0x120D07,
                0x332111,
                0x3D3023,
                0x5E5343,
                0x37382B,
                0x221918,
                0x575036,
                0x241309,
                0x3B1700,
                0x6E6246,
                0x998D73,
                0xCFC0A5,
                0x1F1709,
                0x3D311D,
                0x665847,
                0xF0F0F0,
                0xB3B9C9,
                0x615F55,
                0x241E1A,
                0x171413,
                0x3B372F,
                0x3B4045,
                0x1A1E21,
                0x5E646B,
                0x000000,
                0xB0B0B0,
                0x999999,
                0xB56519,
                0xC45C33,
                0x47783C,
                0xBA8425,
                0x2A77A1,
                0x243022,
                0x6B5F54,
                0xC96E34,
                0xD9D9D9,
                0xF0F0F0,
                0x3F4228,
                0xFFFFFF,
                0xB01259,
                0xF69799,
                0x8F2F55,
                0xC26610,
                0x69BD45,
                0x00AEEF,
                0x000108,
                0x050008,
                0x080000,
                0x565751,
                0x320642,
                0x00080F,
                0x080808,
                0x320642,
                0x050008,
                0x6B0B00,
                0x121710,
                0x323325,
                0x3B352D,
                0x706656,
                0x2B302B,
                0x414347,
                0x6690B5,
                0x47391B,
                0x47391B,
                0xFFD859
        };

        private bool TryGetStandardVehicleColorRgb(int colorIndex, out int red, out int green, out int blue)
        {
            red = green = blue = 0;
            if (colorIndex < 0 || colorIndex >= StandardVehicleColorRgb.Length) return false;
            int rgb = StandardVehicleColorRgb[colorIndex];
            red = (rgb >> 16) & 255;
            green = (rgb >> 8) & 255;
            blue = rgb & 255;
            return true;
        }

        private void ApplyMattePaintPreservingColor(int handle)
        {
            try
            {
                int primaryType, primaryColor, pearl;
                int secondaryType, secondaryColor;
                using (OutputArgument type = new OutputArgument())
                using (OutputArgument color = new OutputArgument())
                using (OutputArgument pearlescent = new OutputArgument())
                {
                    Function.Call((Hash)0xE8D65CA700C9A693UL, handle, type, color, pearlescent); // GET_VEHICLE_MOD_COLOR_1
                    primaryType = type.GetResult<int>();
                    primaryColor = color.GetResult<int>();
                    pearl = pearlescent.GetResult<int>();
                }
                using (OutputArgument type = new OutputArgument())
                using (OutputArgument color = new OutputArgument())
                {
                    Function.Call((Hash)0x81592BE4E3878728UL, handle, type, color); // GET_VEHICLE_MOD_COLOR_2
                    secondaryType = type.GetResult<int>();
                    secondaryColor = color.GetResult<int>();
                }

                bool customPrimary = Function.Call<bool>((Hash)0xF095C0405307B21BUL, handle); // GET_IS_VEHICLE_PRIMARY_COLOUR_CUSTOM
                bool customSecondary = Function.Call<bool>((Hash)0x910A32E7AAD2656CUL, handle); // GET_IS_VEHICLE_SECONDARY_COLOUR_CUSTOM
                int primaryR = 0, primaryG = 0, primaryB = 0;
                int secondaryR = 0, secondaryG = 0, secondaryB = 0;
                bool primaryRgbKnown = false;
                bool secondaryRgbKnown = false;

                if (customPrimary)
                {
                    using (OutputArgument r = new OutputArgument())
                    using (OutputArgument g = new OutputArgument())
                    using (OutputArgument b = new OutputArgument())
                    {
                        Function.Call((Hash)0xB64CF2CCA9D95F52UL, handle, r, g, b); // GET_VEHICLE_CUSTOM_PRIMARY_COLOUR
                        primaryR = r.GetResult<int>(); primaryG = g.GetResult<int>(); primaryB = b.GetResult<int>();
                        primaryRgbKnown = true;
                    }
                }
                else primaryRgbKnown = TryGetStandardVehicleColorRgb(primaryColor, out primaryR, out primaryG, out primaryB);

                if (customSecondary)
                {
                    using (OutputArgument r = new OutputArgument())
                    using (OutputArgument g = new OutputArgument())
                    using (OutputArgument b = new OutputArgument())
                    {
                        Function.Call((Hash)0x8389CD56CA8072DCUL, handle, r, g, b); // GET_VEHICLE_CUSTOM_SECONDARY_COLOUR
                        secondaryR = r.GetResult<int>(); secondaryG = g.GetResult<int>(); secondaryB = b.GetResult<int>();
                        secondaryRgbKnown = true;
                    }
                }
                else secondaryRgbKnown = TryGetStandardVehicleColorRgb(secondaryColor, out secondaryR, out secondaryG, out secondaryB);

                // Never change a paint type unless its exact source RGB is known.
                // This avoids silently changing the shade for modded/DLC palette IDs.
                if (primaryRgbKnown)
                {
                    Function.Call((Hash)0x43FEB945EE7F85B8UL, handle, 3, primaryColor, pearl); // SET_VEHICLE_MOD_COLOR_1 (Matte)
                    Function.Call((Hash)0x7141766F91D15BEAUL, handle, primaryR, primaryG, primaryB); // SET_VEHICLE_CUSTOM_PRIMARY_COLOUR
                }
                else
                {
                    LogFailure("Max tuning matte paint", "Primary paint palette index " + primaryColor + " is not recognized; original primary finish was preserved.");
                }

                if (secondaryRgbKnown)
                {
                    Function.Call((Hash)0x816562BADFDEC83EUL, handle, 3, secondaryColor); // SET_VEHICLE_MOD_COLOR_2 (Matte)
                    Function.Call((Hash)0x36CED73BFED89754UL, handle, secondaryR, secondaryG, secondaryB); // SET_VEHICLE_CUSTOM_SECONDARY_COLOUR
                }

                Log("Max tuning matte paint applied: primary=" + primaryType + "/" + primaryColor + ", secondary=" + secondaryType + "/" + secondaryColor + ", primary RGB preserved=" + primaryRgbKnown + ", secondary RGB preserved=" + secondaryRgbKnown + ".");
            }
            catch (Exception ex)
            {
                LogException("ApplyMattePaintPreservingColor", ex);
            }
        }

        private void DeleteCurrentVehicle()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped) || !ped.IsInVehicle())
            {
                Notify("Enter the vehicle you want to delete first.");
                return;
            }

            Vehicle vehicle = ped.CurrentVehicle;
            if (!IsEntityUsable(vehicle))
            {
                Notify("Current vehicle is not available.");
                return;
            }

            _pendingVehicleDeleteHandle = vehicle.Handle;
            Function.Call((Hash)0xD3DBCE61A490BE02UL, ped.Handle, vehicle.Handle, 0);
            Notify("Leaving vehicle. It will be deleted safely after you exit.");
        }
        private void ProcessPendingVehicleDeletion(Ped ped)
        {
            if (_pendingVehicleDeleteHandle == 0 || !IsEntityUsable(ped)) return;
            if (ped.IsInVehicle() && IsEntityUsable(ped.CurrentVehicle) && ped.CurrentVehicle.Handle == _pendingVehicleDeleteHandle)
                return;

            int handle = _pendingVehicleDeleteHandle;
            _pendingVehicleDeleteHandle = 0;
            try
            {
                Vehicle vehicle = new Vehicle(handle);
                if (IsEntityUsable(vehicle)) vehicle.Delete();
                Notify("Vehicle deleted safely.");
            }
            catch (Exception ex)
            {
                LogException("DeleteCurrentVehicle", ex);
                Notify("Vehicle could not be deleted. See log.");
            }
        }
        private void ToggleSpeedUnit()
        {
            _settings.SpeedUnitMph = !_settings.SpeedUnitMph;
            SaveSettings();
            Notify("Speedometer unit: " + (_settings.SpeedUnitMph ? "MPH" : "KM/H"));
        }
        private void FlipVehicle()
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;
            Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicle.Handle);
            Notify("Vehicle placed upright.");
        }
        private void SetAllVehicleDoors(bool open)
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;
            int doorCount = Function.Call<int>((Hash)0x92922A607497B14DUL, vehicle.Handle);
            if (doorCount <= 0)
            {
                Notify("This vehicle has no standard doors to operate.");
                return;
            }
            if (doorCount > 6) doorCount = 6;

            for (int door = 0; door < doorCount; door++)
            {
                if (open) Function.Call((Hash)0x7C65DAC73C35C862UL, vehicle.Handle, door, false, false);
                else Function.Call((Hash)0x93D9BD300D7789E5UL, vehicle.Handle, door, false);
            }
            Notify(open ? "All available doors opened." : "All available doors closed.");
        }
        private void SetVehicleNeons(bool enabled)
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;
            for (int side = 0; side < 4; side++)
                Function.Call((Hash)0x2AA720E4287BF269UL, vehicle.Handle, side, enabled);
            if (enabled)
                Function.Call((Hash)0x8E0A582209A62695UL, vehicle.Handle, 255, 0, 0);
            Notify(enabled ? "Neon underglow enabled (red)." : "Neon underglow disabled.");
        }
        private void ChangeNitroPower(float delta)
        {
            _settings.NitroPower = ClampFloat(_settings.NitroPower + delta, 0.50f, 8.00f);
            SaveSettings();
            Notify("Nitro power: x" + _settings.NitroPower.ToString("0.00", CultureInfo.InvariantCulture));
        }
        private void SetVehicleTint()
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;
            Function.Call((Hash)0x57C51E6BAD752696UL, vehicle.Handle, 5); // WINDOW_TINT_LIMO
            int appliedTint = Function.Call<int>((Hash)0x0EE21293DAD47C95UL, vehicle.Handle);
            Log("Limo tint action: vehicle=" + vehicle.Handle + "; tintIndex=" + appliedTint + ".");
            Notify(appliedTint == 5 ? "Limo window tint applied." : "Vehicle does not support the requested limo tint.");
        }
        private void SetVehicleColours(int primary, int secondary)
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;
            Function.Call((Hash)0x4F1D4BE3A7F24601UL, vehicle.Handle, primary, secondary);
            Notify("Vehicle paint changed.");
        }
        private void SetVehiclePlate(string text)
        {
            Vehicle vehicle;
            if (!TryGetCurrentVehicle(out vehicle)) return;
            Function.Call((Hash)0x95A88F0B409CDA47UL, vehicle.Handle, text);
            Notify("License plate updated.");
        }
        private int FindExistingTrainerVehicle(int modelHash)
        {
            List<int> stale = new List<int>();
            int existingHandle = 0;
            foreach (KeyValuePair<int, uint> item in _trainerSpawnedVehicleHashes)
            {
                try
                {
                    if (!DoesEntityHandleExist(item.Key))
                    {
                        stale.Add(item.Key);
                        continue;
                    }
                    Vehicle vehicle = new Vehicle(item.Key);
                    if (!IsEntityUsable(vehicle))
                    {
                        stale.Add(item.Key);
                        continue;
                    }
                    uint actualHash = unchecked((uint)vehicle.Model.Hash);
                    if (actualHash != item.Value)
                    {
                        stale.Add(item.Key);
                        continue;
                    }
                    if (unchecked((int)actualHash) == modelHash) existingHandle = item.Key;
                }
                catch (Exception ex) { LogException("FindExistingTrainerVehicle " + item.Key, ex); stale.Add(item.Key); }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                _trainerSpawnedVehicleHashes.Remove(stale[i]);
                _trainerSpawnedVehicleCreatedUtc.Remove(stale[i]);
                _trainerVehicleDeleteRetryUtc.Remove(stale[i]);
            }
            return existingHandle;
        }

        private void BeginVehicleSpawn(string modelName, string displayName)
        {
            if (_vehicleSpawnPending)
            {
                Notify("A vehicle model is already loading. Please wait.");
                return;
            }
            if (DateTime.UtcNow < _nextVehicleSpawnAllowedUtc)
            {
                Notify("Wait briefly before spawning another vehicle.");
                return;
            }
            if (!IsSafeVehicleModelName(modelName))
            {
                Notify("Invalid vehicle model name.");
                LogFailure("Vehicle spawn", "Rejected invalid model name supplied to vehicle spawner: " + (modelName ?? "<null>"));
                Notify("Vehicle model ID is invalid. See scripts/AvernesSowtware/AvernesSowtware.log.");
                return;
            }

            try
            {
                string requestedModelName = modelName;
                if (IsRetiredOrUnsupportedVehicleModel(requestedModelName))
                {
                    string unsupportedReason = "Vehicle model is not supported by the installed GTA V Legacy 1.0.2245 build: " + requestedModelName;
                    LogFailure("Vehicle spawn", unsupportedReason);
                    Notify(unsupportedReason);
                    return;
                }
                Model model = new Model(modelName);
                int modelHash = model.Hash;
                bool modelValid = model.IsValid;
                bool inCdImage = model.IsInCdImage;
                bool isVehicle = model.IsVehicle;
                if (!modelValid || !inCdImage || !isVehicle)
                {
                    string reason = "Vehicle model is not available in this game build: " + requestedModelName;
                    LogFailure("Vehicle spawn", reason + " (valid=" + modelValid + ", cdimage=" + inCdImage + ", vehicle=" + isVehicle + ").");
                    Notify(reason);
                    return;
                }

                _pendingVehicleModelHash = modelHash;
                _pendingVehicleModelName = modelName;
                _pendingVehicleDisplayName = String.IsNullOrWhiteSpace(displayName) ? modelName : displayName;
                // In-air transfer/replacement is intentionally disabled in 1.3.1.
                // The trainer never moves the player out of a current aircraft or boat.
                _pendingVehicleSpawnDeadlineUtc = DateTime.UtcNow.AddSeconds(15);
                _vehicleSpawnPending = true;
                _pendingSavedVehicle = null;
                _nextVehicleSpawnAllowedUtc = DateTime.UtcNow.AddMilliseconds(1100);
                Function.Call((Hash)0x963D27A58DF860ACUL, _pendingVehicleModelHash); // REQUEST_MODEL
                Notify("Loading vehicle: " + _pendingVehicleDisplayName);
                Log("Requested vehicle model: " + modelName);
            }
            catch (Exception ex)
            {
                CancelPendingVehicleSpawn();
                LogException("Begin vehicle spawn " + modelName, ex);
                Notify("Vehicle spawn could not be started. See log.");
            }
        }
        private void ProcessPendingVehicleSpawn(Ped ped)
        {
            if (!_vehicleSpawnPending) return;
            int modelHash = _pendingVehicleModelHash;
            string modelName = _pendingVehicleModelName;
            string displayName = _pendingVehicleDisplayName;
            SavedVehicle savedVehicle = _pendingSavedVehicle;

            if (DateTime.UtcNow >= _pendingVehicleSpawnDeadlineUtc)
            {
                CancelPendingVehicleSpawn();
                string timeoutReason = "Vehicle model load timed out: " + modelName;
                LogFailure("Vehicle spawn", timeoutReason);
                Notify(timeoutReason);
                return;
            }

            try
            {
                bool loaded = Function.Call<bool>((Hash)0x98A4EB5D89A0C952UL, modelHash); // HAS_MODEL_LOADED
                if (!loaded) return;
                if (!IsEntityUsable(ped))
                {
                    CancelPendingVehicleSpawn();
                    LogFailure("Vehicle spawn", "Player entity was unavailable when the model finished loading: " + modelName);
                    Notify("Player is not ready. Vehicle spawn cancelled.");
                    return;
                }

                int existingVehicleHandle = FindExistingTrainerVehicle(modelHash);
                if (existingVehicleHandle != 0 && DoesEntityHandleExist(existingVehicleHandle))
                {
                    if (savedVehicle != null)
                    {
                        ApplySavedVehicleTuning(existingVehicleHandle, savedVehicle);
                        Notify("Reusing existing trainer vehicle and applying saved setup: " + displayName);
                    }
                    else Notify("Reusing existing trainer vehicle; duplicate creation was skipped: " + displayName);
                    Log("Reusing existing trainer vehicle. Model=" + modelName + "; entity=" + existingVehicleHandle + "; duplicate spawn skipped.");
                    CancelPendingVehicleSpawn();
                    return;
                }

                Vector3 forward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, ped.Handle);
                Vector3 spawnPosition;
                float spawnHeading = ped.Heading;
                float spawnOffset = 8.0f;
                float sideOffset = 0.0f;
                float heightOffset = 2.0f;
                Vector3 right = new Vector3(forward.Y, -forward.X, 0.0f);
                if (String.Equals(modelName, "bombushka", StringComparison.OrdinalIgnoreCase))
                {
                    spawnOffset = 85.0f;
                    sideOffset = 36.0f;
                }
                else if (String.Equals(modelName, "cargoplane", StringComparison.OrdinalIgnoreCase))
                {
                    spawnOffset = 115.0f;
                    sideOffset = 55.0f;
                }
                if (ped.IsInVehicle() && IsEntityUsable(ped.CurrentVehicle))
                {
                    int currentClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, ped.CurrentVehicle.Handle);
                    if (currentClass > 8)
                    {
                        spawnOffset = Math.Max(spawnOffset, 45.0f);
                        heightOffset = 8.0f;
                    }
                }
                spawnPosition = ped.Position + forward * spawnOffset + right * sideOffset;
                spawnPosition.Z += heightOffset;

                // Free only distant, empty vehicles previously created by this trainer.
                // This is a cleanup attempt, not an invented claim about GTA's hard entity limit.
                CleanupOldTrainerVehiclesForSpawn(ped, false);
                int vehicleHandle = CreateVehicleEntityWithFallback(modelHash, spawnPosition, forward, spawnHeading);
                if (!IsVehicleHandleUsable(vehicleHandle))
                {
                    CleanupOldTrainerVehiclesForSpawn(ped, true);
                    vehicleHandle = CreateVehicleEntityWithFallback(modelHash, spawnPosition, forward, spawnHeading);
                }
                if (!IsVehicleHandleUsable(vehicleHandle))
                {
                    int worldVehicleCount = -1;
                    try { Vehicle[] allVehicles = World.GetAllVehicles(); worldVehicleCount = allVehicles == null ? -1 : allVehicles.Length; }
                    catch (Exception countException) { LogException("Vehicle pool diagnostic", countException); }
                    CancelPendingVehicleSpawn();
                    string createReason = "CREATE_VEHICLE returned no valid entity for model: " + modelName +
                        "; worldVehicles=" + worldVehicleCount + "; trainerTracked=" + _trainerSpawnedVehicleHashes.Count +
                        ". Far/empty trainer vehicles were pruned before retry.";
                    LogFailure("Vehicle spawn", createReason);
                    Notify("Vehicle could not be created. Some game entities may be exhausting the available pool; use World cleanup and retry.");
                    return;
                }

                Function.Call((Hash)0xAD738C3085FE7E11UL, vehicleHandle, true, true); // SET_ENTITY_AS_MISSION_ENTITY
                Function.Call((Hash)0x2B5F9D2AF1F1722DUL, vehicleHandle, true); // SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicleHandle);
                _trainerSpawnedVehicleHashes[vehicleHandle] = unchecked((uint)modelHash);
                _trainerSpawnedVehicleCreatedUtc[vehicleHandle] = DateTime.UtcNow;
                if (savedVehicle != null)
                {
                    ApplySavedVehicleTuning(vehicleHandle, savedVehicle);
                    Notify("Restored saved vehicle: " + displayName);
                }
                else Notify("Spawned vehicle: " + displayName);
                Log("Spawned vehicle: " + modelName + "; entity=" + vehicleHandle + "; hash=" + modelHash +
                    "; tracked=" + _trainerSpawnedVehicleHashes.Count + "; savedSlot=" + (savedVehicle == null ? 0 : savedVehicle.Slot));
                CancelPendingVehicleSpawn();
            }
            catch (Exception ex)
            {
                CancelPendingVehicleSpawn();
                LogException("Process vehicle spawn " + modelName, ex);
                Notify("Vehicle spawn failed. See log.");
            }
        }
        private int CreateVehicleEntityWithFallback(int modelHash, Vector3 basePosition, Vector3 forward, float heading)
        {
            Vector3 right = new Vector3(forward.Y, -forward.X, 0.0f);
            Vector3[] positions = new Vector3[] {
                basePosition,
                basePosition + right * 5.5f,
                basePosition - right * 5.5f,
                basePosition + forward * 7.0f,
                basePosition + forward * 12.0f,
                basePosition + right * 9.0f,
                basePosition - right * 9.0f,
                basePosition + forward * 8.0f + right * 7.0f,
                basePosition + forward * 8.0f - right * 7.0f
            };
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 candidate = positions[i];
                Function.Call((Hash)0x07503F7948F491A7UL, candidate.X, candidate.Y, candidate.Z); // REQUEST_COLLISION_AT_COORD
                try
                {
                    if (Function.Call<bool>((Hash)0x61E1DD6125A3EEE6UL, candidate.X, candidate.Y, candidate.Z, 3.5f))
                        continue; // Do not overlap an existing vehicle when an alternate point is available.
                }
                catch (Exception proximityException) { LogException("Vehicle spawn proximity check", proximityException); }
                int handle = Function.Call<int>((Hash)0xAF35D0D2583051B0UL,
                    modelHash, candidate.X, candidate.Y, candidate.Z, heading, false, false);
                if (IsVehicleHandleUsable(handle))
                {
                    if (i > 0) Log("Vehicle spawn succeeded using alternate placement " + i + ".");
                    return handle;
                }
            }
            // Last-resort point: nearby vehicles may occupy every preferred point in dense areas.
            // Keep a final offset so a blocked parking position does not prevent all spawn attempts.
            try
            {
                Vector3 finalCandidate = basePosition + forward * 18.0f;
                Function.Call((Hash)0x07503F7948F491A7UL, finalCandidate.X, finalCandidate.Y, finalCandidate.Z);
                int fallbackHandle = Function.Call<int>((Hash)0xAF35D0D2583051B0UL,
                    modelHash, finalCandidate.X, finalCandidate.Y, finalCandidate.Z, heading, false, false);
                if (IsVehicleHandleUsable(fallbackHandle))
                {
                    Log("Vehicle spawn succeeded using final distant fallback placement.");
                    return fallbackHandle;
                }
            }
            catch (Exception ex) { LogException("Vehicle spawn final fallback", ex); }
            return 0;
        }
        private void ProcessDestroyedTrainerVehicles(Ped player)
        {
            DateTime now = DateTime.UtcNow;
            if (now < _nextVehicleRegistrySweepUtc) return;
            _nextVehicleRegistrySweepUtc = now.AddMilliseconds(500);
            if (!IsEntityUsable(player)) return;
            List<int> removeHandles = new List<int>();
            foreach (KeyValuePair<int, uint> tracked in _trainerSpawnedVehicleHashes)
            {
                int handle = tracked.Key;
                try
                {
                    if (!IsVehicleHandleUsable(handle) ||
                        Function.Call<uint>(Hash.GET_ENTITY_MODEL, handle) != tracked.Value)
                    { removeHandles.Add(handle); continue; }

                    if (!Function.Call<bool>((Hash)0x0A7B270912999B3CUL, handle)) // IS_ENTITY_A_MISSION_ENTITY
                        Function.Call((Hash)0xAD738C3085FE7E11UL, handle, true, true); // SET_ENTITY_AS_MISSION_ENTITY: reassert mission ownership
                    Function.Call((Hash)0x2B5F9D2AF1F1722DUL, handle, true); // SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER

                    bool destroyed = Function.Call<bool>((Hash)0x5F9532F3B5CC2551UL, handle, false) ||
                        Function.Call<int>(Hash.GET_ENTITY_HEALTH, handle) <= 0;
                    bool playerUsingVehicle = player.IsInVehicle() &&
                        IsEntityUsable(player.CurrentVehicle) && player.CurrentVehicle.Handle == handle;
                    if (playerUsingVehicle) continue;

                    if (destroyed)
                    {
                        if (TryDeleteTrackedVehicle(handle, "destroyed")) removeHandles.Add(handle);
                        continue;
                    }

                    if (IsVehicleOccupied(handle)) continue;
                    DateTime createdUtc;
                    if (!_trainerSpawnedVehicleCreatedUtc.TryGetValue(handle, out createdUtc) ||
                        createdUtc == DateTime.MinValue || now < createdUtc.AddSeconds(20)) continue;

                    Vector3 vehiclePosition = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, handle, true);
                    float distance = Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                        vehiclePosition.X, vehiclePosition.Y, vehiclePosition.Z,
                        player.Position.X, player.Position.Y, player.Position.Z, true);
                    if (distance < 650.0f) continue;

                    if (TryDeleteTrackedVehicle(handle, "distant"))
                    {
                        removeHandles.Add(handle);
                        Log("Auto-despawned distant empty trainer vehicle. Entity=" + handle +
                            "; distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture) + "m.");
                    }
                }
                catch (Exception ex) { LogException("ProcessDestroyedTrainerVehicles " + handle, ex); }
            }
            for (int i = 0; i < removeHandles.Count; i++)
            {
                _trainerSpawnedVehicleHashes.Remove(removeHandles[i]);
                _trainerSpawnedVehicleCreatedUtc.Remove(removeHandles[i]);
                _trainerVehicleDeleteRetryUtc.Remove(removeHandles[i]);
            }
        }
        private bool TryDeleteTrackedVehicle(int handle, string reason)
        {
            if (!IsVehicleHandleUsable(handle)) return true;
            // Entity handles can be recycled by GTA. Never delete a different
            // vehicle that happens to receive a handle previously tracked here.
            uint expectedModel;
            if (_trainerSpawnedVehicleHashes.TryGetValue(handle, out expectedModel))
            {
                uint currentModel = Function.Call<uint>(Hash.GET_ENTITY_MODEL, handle);
                if (currentModel != expectedModel)
                {
                    Log("Skipped stale trainer vehicle handle " + handle + ": model changed from " + expectedModel + " to " + currentModel + ".");
                    return true;
                }
            }
            DateTime now = DateTime.UtcNow;
            DateTime retryAt;
            if (_trainerVehicleDeleteRetryUtc.TryGetValue(handle, out retryAt) && now < retryAt) return false;
            _trainerVehicleDeleteRetryUtc[handle] = now.AddSeconds(2);
            try
            {
                Vehicle vehicle = new Vehicle(handle);
                if (IsEntityUsable(vehicle)) vehicle.Delete();
            }
            catch (Exception ex) { LogException("Delete tracked " + reason + " vehicle " + handle, ex); }
            bool stillExists = IsVehicleHandleUsable(handle);
            if (!stillExists)
            {
                _trainerVehicleDeleteRetryUtc.Remove(handle);
                Log("Removed " + reason + " trainer vehicle. Entity=" + handle + ".");
                return true;
            }
            Log("Delete pending for " + reason + " trainer vehicle; retry scheduled. Entity=" + handle + ".");
            return false;
        }
        private bool IsVehicleOccupied(int vehicleHandle)
        {
            try
            {
                int maxPassengers = Function.Call<int>((Hash)0xA7C4F2C6E744A550UL, vehicleHandle);
                if (maxPassengers < 0 || maxPassengers > 16) maxPassengers = 8;
                for (int seat = -1; seat < maxPassengers; seat++)
                    if (Function.Call<int>((Hash)0xBB40DD2270B65366UL, vehicleHandle, seat) != 0) return true;
            }
            catch (Exception ex) { LogException("IsVehicleOccupied " + vehicleHandle, ex); return true; }
            return false;
        }
        private int CleanupOldTrainerVehiclesForSpawn(Ped player, bool aggressive)
        {
            if (!IsEntityUsable(player)) return 0;
            int trackedCount = _trainerSpawnedVehicleHashes.Count;
            bool emergencyPrune = trackedCount > 64 || (aggressive && trackedCount > 36);
            int targetCount = emergencyPrune ? 32 : (aggressive ? 24 : 36);
            if (trackedCount <= targetCount) return 0;
            int removed = 0;
            int maxToRemove = emergencyPrune ? 16 : (aggressive ? 10 : 6);
            // Prefer distant cars. If the registry becomes very large, also remove
            // the oldest unoccupied trainer vehicles nearby; otherwise a long
            // spawn session at one location can exhaust the world entity pool.
            float minimumDistance = emergencyPrune ? 0.0f : (aggressive ? 25.0f : 75.0f);
            List<KeyValuePair<int, DateTime>> candidates = new List<KeyValuePair<int, DateTime>>();
            foreach (KeyValuePair<int, uint> item in _trainerSpawnedVehicleHashes)
            {
                DateTime created;
                if (!_trainerSpawnedVehicleCreatedUtc.TryGetValue(item.Key, out created)) created = DateTime.MinValue;
                candidates.Add(new KeyValuePair<int, DateTime>(item.Key, created));
            }
            candidates.Sort(delegate(KeyValuePair<int, DateTime> a, KeyValuePair<int, DateTime> b) { return a.Value.CompareTo(b.Value); });
            int playerVehicle = 0;
            try { if (player.IsInVehicle() && IsEntityUsable(player.CurrentVehicle)) playerVehicle = player.CurrentVehicle.Handle; } catch { }
            for (int i = 0; i < candidates.Count && removed < maxToRemove && _trainerSpawnedVehicleHashes.Count > targetCount; i++)
            {
                int handle = candidates[i].Key;
                if (handle == playerVehicle) continue;
                try
                {
                    if (!IsVehicleHandleUsable(handle))
                    {
                        _trainerSpawnedVehicleHashes.Remove(handle);
                        _trainerSpawnedVehicleCreatedUtc.Remove(handle);
                        _trainerVehicleDeleteRetryUtc.Remove(handle);
                        continue;
                    }
                    if (IsVehicleOccupied(handle)) continue;
                    Vector3 position = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, handle, true);
                    float distance = Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                        position.X, position.Y, position.Z, player.Position.X, player.Position.Y, player.Position.Z, true);
                    if (distance < minimumDistance) continue;
                    if (TryDeleteTrackedVehicle(handle, "old empty"))
                    {
                        _trainerSpawnedVehicleHashes.Remove(handle);
                        _trainerSpawnedVehicleCreatedUtc.Remove(handle);
                        _trainerVehicleDeleteRetryUtc.Remove(handle);
                        removed++;
                        Log("Pruned old empty trainer vehicle before spawn. Entity=" + handle + "; distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture) + ".");
                    }
                }
                catch (Exception ex) { LogException("CleanupOldTrainerVehiclesForSpawn " + handle, ex); }
            }
            if (removed > 0) Log("Spawn cleanup removed " + removed + " empty trainer vehicles; aggressive=" + aggressive + "; emergencyPrune=" + emergencyPrune + "; remaining tracked=" + _trainerSpawnedVehicleHashes.Count + ".");
            return removed;
        }
        private void CancelPendingVehicleSpawn()
        {
            if (_vehicleSpawnPending && _pendingVehicleModelHash != 0)
            {
                try { Function.Call((Hash)0xE532F5D78798DAABUL, _pendingVehicleModelHash); } // SET_MODEL_AS_NO_LONGER_NEEDED
                catch (Exception ex) { LogException("Release pending vehicle model", ex); }
            }
            _vehicleSpawnPending = false;
            _pendingVehicleModelHash = 0;
            _pendingVehicleModelName = null;
            _pendingVehicleDisplayName = null;
            _pendingSavedVehicle = null;
            _pendingVehicleSpawnDeadlineUtc = DateTime.MinValue;
        }
    }
}
