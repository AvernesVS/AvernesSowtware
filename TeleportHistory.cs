using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;

namespace AvernesSowtware
{
    public sealed partial class AvernesSowtwareScript
    {
        private const int MaximumTeleportHistory = 10;
        private readonly List<TeleportPoint> _teleportHistory = new List<TeleportPoint>();
        private TeleportPoint _pendingGroundTeleportHistoryOrigin;

        private sealed class TeleportPoint
        {
            public float X;
            public float Y;
            public float Z;
            public float Heading;
        }

        private TeleportPoint CaptureTeleportOrigin()
        {
            Ped ped = Game.Player.Character;
            if (!IsEntityUsable(ped)) return null;
            Entity entity = ped;
            try
            {
                if (ped.IsInVehicle())
                {
                    Vehicle vehicle = ped.CurrentVehicle;
                    if (IsEntityUsable(vehicle)) entity = vehicle;
                }
                Vector3 position = entity.Position;
                float heading = entity.Heading;
                if (!IsSafeWorldPosition(position) || Single.IsNaN(heading) || Single.IsInfinity(heading)) return null;
                return new TeleportPoint { X = position.X, Y = position.Y, Z = position.Z, Heading = heading };
            }
            catch (Exception ex)
            {
                LogException("Capture teleport origin", ex);
                return null;
            }
        }

        private void RecordTeleportOrigin(TeleportPoint point)
        {
            if (point == null) return;
            _teleportHistory.Add(point);
            while (_teleportHistory.Count > MaximumTeleportHistory) _teleportHistory.RemoveAt(0);
            Log("Teleport history recorded. Count=" + _teleportHistory.Count + "; X=" + point.X + "; Y=" + point.Y + "; Z=" + point.Z + ".");
        }

        private string GetTeleportHistoryLabel()
        {
            return _teleportHistory.Count.ToString() + "/" + MaximumTeleportHistory;
        }

        private void UndoLastTeleport()
        {
            if (_teleportHistory.Count == 0)
            {
                Notify("Teleport history is empty.");
                return;
            }
            TeleportPoint point = _teleportHistory[_teleportHistory.Count - 1];
            if (point == null || !IsSafeWorldPosition(new Vector3(point.X, point.Y, point.Z)) ||
                Single.IsNaN(point.Heading) || Single.IsInfinity(point.Heading))
            {
                _teleportHistory.RemoveAt(_teleportHistory.Count - 1);
                LogFailure("Teleport history", "Skipped a malformed history record during rollback.");
                Notify("Stored teleport point was invalid and has been skipped.");
                return;
            }

            BeginHistoryRollbackTeleport(point);
        }

        private void CompleteHistoryRollbackTeleport(TeleportPoint target)
        {
            if (target == null) return;
            int lastIndex = _teleportHistory.Count - 1;
            if (lastIndex >= 0 && Object.ReferenceEquals(_teleportHistory[lastIndex], target))
            {
                _teleportHistory.RemoveAt(lastIndex);
                Log("Teleport rollback completed after destination streaming. Remaining history=" + _teleportHistory.Count + ".");
                Notify("Returned to the previous location.");
                return;
            }
            LogFailure("Teleport history", "Rollback completed but the expected history entry was no longer at the top of the stack.");
            Notify("Returned to the previous location; history changed during loading.");
        }

    }
}
