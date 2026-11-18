using System;
using System.Collections.Generic;
using System.Diagnostics;
using GTA;
using GTA.Math;
using GTA.Native;

namespace AvernesSowtware
{
    public sealed partial class AvernesSowtwareScript
    {
        private readonly HashSet<int> _staleEntityHandlesLogged = new HashSet<int>();
        private readonly Dictionary<string, ManagerTickProfile> _managerTickProfiles = new Dictionary<string, ManagerTickProfile>(StringComparer.OrdinalIgnoreCase);
        private DateTime _nextEntityWatchdogUtc = DateTime.MinValue;
        private DateTime _nextProfilerReportUtc = DateTime.MinValue;
        private Vector3 _lastSafePlayerPosition;
        private float _lastSafePlayerHeading;
        private bool _lastSafePlayerStateCaptured;
        private int _watchdogEntityHandle;
        private Vector3 _lastMovingEntityPosition;
        private float _lastMovingEntityHeading;
        private bool _lastMovingEntityCaptured;
        private int _stalledEntitySamples;

        private sealed class ManagerTickProfile
        {
            public long Samples;
            public double TotalMilliseconds;
            public double MaximumMilliseconds;
        }

        private bool IsEntityUsable(Entity entity)
        {
            if (entity == null) return false;
            int handle = 0;
            try
            {
                handle = entity.Handle;
                return handle != 0 && entity.Exists();
            }
            catch (Exception ex)
            {
                LogStaleEntityOnce(handle, "Entity.Exists failed: " + ex.GetType().Name);
                return false;
            }
        }

        private bool DoesEntityHandleExist(int handle)
        {
            if (handle == 0) return false;
            try { return Function.Call<bool>((Hash)0x7239B21A38F536BAUL, handle); }
            catch (Exception ex)
            {
                LogStaleEntityOnce(handle, "DOES_ENTITY_EXIST failed: " + ex.GetType().Name);
                return false;
            }
        }

        private bool IsPedHandleUsable(int handle)
        {
            if (!DoesEntityHandleExist(handle)) return false;
            try
            {
                if (Function.Call<int>((Hash)0x8ACD366038D14505UL, handle) != 1) return false;
                return IsEntityUsable(new Ped(handle));
            }
            catch (Exception ex) { LogStaleEntityOnce(handle, "Ped handle validation failed: " + ex.GetType().Name); return false; }
        }

        private bool IsVehicleHandleUsable(int handle)
        {
            if (!DoesEntityHandleExist(handle)) return false;
            try
            {
                if (Function.Call<int>((Hash)0x8ACD366038D14505UL, handle) != 2) return false;
                return IsEntityUsable(new Vehicle(handle));
            }
            catch (Exception ex) { LogStaleEntityOnce(handle, "Vehicle handle validation failed: " + ex.GetType().Name); return false; }
        }

        private bool IsPropHandleUsable(int handle)
        {
            if (!DoesEntityHandleExist(handle)) return false;
            try
            {
                if (Function.Call<int>((Hash)0x8ACD366038D14505UL, handle) != 3) return false;
                return IsEntityUsable(new Prop(handle));
            }
            catch (Exception ex) { LogStaleEntityOnce(handle, "Prop handle validation failed: " + ex.GetType().Name); return false; }
        }

        private void LogStaleEntityOnce(int handle, string details)
        {
            if (handle != 0 && !_staleEntityHandlesLogged.Add(handle)) return;
            Log("Entity guard skipped a stale or unavailable entity. Handle=" + handle + "; " + (details ?? "no details"));
            if (_staleEntityHandlesLogged.Count > 512) _staleEntityHandlesLogged.Clear();
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !Single.IsNaN(value.X) && !Single.IsInfinity(value.X) &&
                !Single.IsNaN(value.Y) && !Single.IsInfinity(value.Y) &&
                !Single.IsNaN(value.Z) && !Single.IsInfinity(value.Z);
        }

        private static bool IsSafeWorldPosition(Vector3 value)
        {
            return IsFiniteVector(value) && Math.Abs(value.X) <= 20000.0f &&
                Math.Abs(value.Y) <= 20000.0f && value.Z >= -1000.0f && value.Z <= 10000.0f;
        }

        private void RunEntityWatchdog(Ped player)
        {
            DateTime now = DateTime.UtcNow;
            if (now < _nextEntityWatchdogUtc) return;
            _nextEntityWatchdogUtc = now.AddSeconds(1);
            if (!IsEntityUsable(player))
            {
                LogStaleEntityOnce(0, "Player ped was no longer valid during watchdog inspection.");
                return;
            }

            try
            {
                Entity monitoredEntity = player;
                if (player.IsInVehicle() && IsEntityUsable(player.CurrentVehicle)) monitoredEntity = player.CurrentVehicle;
                if (!IsEntityUsable(monitoredEntity)) monitoredEntity = player;
                int monitoredHandle = monitoredEntity.Handle;
                Vector3 position = monitoredEntity.Position;
                Vector3 velocity = monitoredEntity.Velocity;
                float heading = monitoredEntity.Heading;
                bool safePosition = IsSafeWorldPosition(position) && !Single.IsNaN(heading) && !Single.IsInfinity(heading);
                if (safePosition)
                {
                    _lastSafePlayerPosition = position;
                    _lastSafePlayerHeading = heading;
                    _lastSafePlayerStateCaptured = true;
                    if (_watchdogEntityHandle != monitoredHandle)
                    {
                        _watchdogEntityHandle = monitoredHandle;
                        _lastMovingEntityPosition = position;
                        _lastMovingEntityHeading = heading;
                        _lastMovingEntityCaptured = true;
                        _stalledEntitySamples = 0;
                    }
                    if (_lastMovingEntityCaptured && IsSafeWorldPosition(_lastMovingEntityPosition))
                    {
                        double dx = position.X - _lastMovingEntityPosition.X;
                        double dy = position.Y - _lastMovingEntityPosition.Y;
                        double dz = position.Z - _lastMovingEntityPosition.Z;
                        double displacementSquared = dx * dx + dy * dy + dz * dz;
                        double speedSquared = (double)velocity.X * velocity.X + (double)velocity.Y * velocity.Y + (double)velocity.Z * velocity.Z;
                        if (displacementSquared > 0.04)
                        {
                            _lastMovingEntityPosition = position;
                            _lastMovingEntityHeading = heading;
                            _stalledEntitySamples = 0;
                        }
                        else if (IsFiniteVector(velocity) && speedSquared > 25.0)
                        {
                            _stalledEntitySamples++;
                            if (_stalledEntitySamples >= 5 && IsEntityUsable(monitoredEntity))
                            {
                                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, monitoredHandle,
                                    _lastMovingEntityPosition.X, _lastMovingEntityPosition.Y, _lastMovingEntityPosition.Z, false, false, false);
                                Function.Call((Hash)0x8E2530AA8ADA980EUL, monitoredHandle, _lastMovingEntityHeading); // SET_ENTITY_HEADING
                                Function.Call(Hash.SET_ENTITY_VELOCITY, monitoredHandle, 0.0f, 0.0f, 0.0f);
                                LogFailure("Entity watchdog", "Recovered a moving-but-stalled player entity to its last moving safe position.");
                                Notify("A stuck entity state was recovered to the last safe position.");
                                _lastSafePlayerPosition = _lastMovingEntityPosition;
                                _lastSafePlayerHeading = _lastMovingEntityHeading;
                                _stalledEntitySamples = 0;
                            }
                        }
                        else _stalledEntitySamples = 0;
                    }
                }
                else if (_lastSafePlayerStateCaptured && IsSafeWorldPosition(_lastSafePlayerPosition) && IsEntityUsable(monitoredEntity))
                {
                    Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, monitoredEntity.Handle,
                        _lastSafePlayerPosition.X, _lastSafePlayerPosition.Y, _lastSafePlayerPosition.Z, false, false, false);
                    Function.Call((Hash)0x8E2530AA8ADA980EUL, monitoredEntity.Handle, _lastSafePlayerHeading); // SET_ENTITY_HEADING
                    Function.Call(Hash.SET_ENTITY_VELOCITY, monitoredEntity.Handle, 0.0f, 0.0f, 0.0f);
                    LogFailure("Entity watchdog", "Recovered player/current vehicle from a non-finite/out-of-range position or heading using last safe coordinates.");
                    Notify("A damaged entity position was recovered safely.");
                    _lastMovingEntityPosition = _lastSafePlayerPosition;
                    _lastMovingEntityHeading = _lastSafePlayerHeading;
                    _watchdogEntityHandle = monitoredEntity.Handle;
                    _lastMovingEntityCaptured = true;
                    _stalledEntitySamples = 0;
                }
                if (!IsFiniteVector(velocity) && IsEntityUsable(monitoredEntity))
                {
                    Function.Call(Hash.SET_ENTITY_VELOCITY, monitoredEntity.Handle, 0.0f, 0.0f, 0.0f);
                    LogFailure("Entity watchdog", "Reset a non-finite entity velocity to zero.");
                }
            }
            catch (Exception ex) { LogException("Entity watchdog player state", ex); }

            List<int> staleVehicles = new List<int>();
            foreach (KeyValuePair<int, uint> entry in _trainerSpawnedVehicleHashes)
            {
                try
                {
                    Vehicle vehicle = new Vehicle(entry.Key);
                    if (!IsEntityUsable(vehicle) || unchecked((uint)vehicle.Model.Hash) != entry.Value)
                        staleVehicles.Add(entry.Key);
                }
                catch (Exception ex)
                {
                    staleVehicles.Add(entry.Key);
                    LogException("Entity watchdog tracked vehicle " + entry.Key, ex);
                }
            }
            for (int i = 0; i < staleVehicles.Count; i++)
            {
                int handle = staleVehicles[i];
                _trainerSpawnedVehicleHashes.Remove(handle);
                _trainerSpawnedVehicleCreatedUtc.Remove(handle);
                _trainerVehicleDeleteRetryUtc.Remove(handle);
                LogStaleEntityOnce(handle, "Removed stale/recycled trainer vehicle handle from tracking registry.");
            }

            // Bodyguard lifecycle is owned by ProcessBodyguardFollowing. Never probe dead ped handles here:
            // Entity.Exists on a corpse during GTA's post-death cleanup can destabilize ScriptHookVDotNet.

            if (_vehicleSpawnPending && now >= _pendingVehicleSpawnDeadlineUtc)
            {
                LogFailure("Entity watchdog", "Cancelled a stalled vehicle model load before it could retain pending state.");
                CancelPendingVehicleSpawn();
            }
            if (_groundTeleportPending && !DoesEntityHandleExist(_groundTeleportEntityHandle))
            {
                LogFailure("Entity watchdog", "Cancelled a ground teleport whose target entity disappeared during streaming.");
                CancelPendingGroundTeleport();
            }
        }

        private long BeginManagerTick()
        {
            return Stopwatch.GetTimestamp();
        }

        private void RecordManagerTick(string managerName, long startedAt)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            if (elapsedTicks < 0) return;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / Stopwatch.Frequency;
            ManagerTickProfile profile;
            if (!_managerTickProfiles.TryGetValue(managerName, out profile))
            {
                profile = new ManagerTickProfile();
                _managerTickProfiles.Add(managerName, profile);
            }
            profile.Samples++;
            profile.TotalMilliseconds += elapsedMilliseconds;
            if (elapsedMilliseconds > profile.MaximumMilliseconds) profile.MaximumMilliseconds = elapsedMilliseconds;

            DateTime now = DateTime.UtcNow;
            if (_nextProfilerReportUtc == DateTime.MinValue) _nextProfilerReportUtc = now.AddSeconds(15);
            if (now < _nextProfilerReportUtc) return;
            _nextProfilerReportUtc = now.AddSeconds(15);
            foreach (KeyValuePair<string, ManagerTickProfile> item in _managerTickProfiles)
            {
                ManagerTickProfile current = item.Value;
                if (current.Samples == 0) continue;
                double average = current.TotalMilliseconds / current.Samples;
                if (average >= 1.0 || current.MaximumMilliseconds >= 5.0)
                    Log("Tick profiler: manager=" + item.Key + "; samples=" + current.Samples +
                        "; avgMs=" + average.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) +
                        "; maxMs=" + current.MaximumMilliseconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                current.Samples = 0;
                current.TotalMilliseconds = 0.0;
                current.MaximumMilliseconds = 0.0;
            }
        }

        private string BuildCrashTelemetryContext()
        {
            string assemblyPath = "<unknown>";
            try { assemblyPath = GetType().Assembly.Location; } catch { }
            string processName = "<unknown>";
            int processId = 0;
            try
            {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    processName = process.ProcessName;
                    processId = process.Id;
                }
            }
            catch { }
            return "Product=" + ProductName + " " + ProductVersion + Environment.NewLine +
                "Process=" + processName + "; PID=" + processId + Environment.NewLine +
                "GameBase=" + AppDomain.CurrentDomain.BaseDirectory + Environment.NewLine +
                "Assembly=" + assemblyPath + Environment.NewLine +
                "DataDirectory=" + _dataDirectory + Environment.NewLine +
                "MenuPage=" + (_currentPage ?? "<none>") + "; MenuOpen=" + _menuOpen + Environment.NewLine;
        }
    }
}
