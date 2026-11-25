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

namespace AvernesTreiner.Modules
{
    public class TeleportModule : IAvernesModule
    {
        public string Name { get { return "Teleport"; } }

        private bool _waypointTeleportPending;
        private float _waypointX;
        private float _waypointY;
        private int _waypointStartTime;
        private int _waypointAttempts;
        private bool _waypointLoadSceneStarted;
        private bool _waypointFocusActive;
        private int _waypointLastCollisionRequestTime;
        private int _waypointLastProbeTime;
        private static readonly float[] WaypointProbeHeights = new float[] {
            1200.0f, 1000.0f, 900.0f, 800.0f, 700.0f, 600.0f, 500.0f,
            400.0f, 300.0f, 200.0f, 150.0f, 100.0f, 50.0f, 20.0f, 0.0f, -50.0f, -100.0f
        };

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var teleMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuTeleport", "Teleport"));

            // Waypoint
            var wpItem = new UIMenuItem(Localization.Get("Teleport", "TeleportWaypoint", "Teleport to Waypoint"), Localization.Get("Teleport", "TeleportWaypointDesc", "Teleports to the marked map location"));
            wpItem.Activated += (sender, selected) =>
            {
                Vector3 wp = World.GetWaypointPosition();
                if (Game.IsWaypointActive && wp != Vector3.Zero)
                {
                    if (_waypointTeleportPending) CancelWaypointTeleport();
                    _waypointLastCollisionRequestTime = 0;
                    _waypointLastProbeTime = 0;
                    _waypointX = wp.X;
                    _waypointY = wp.Y;
                    _waypointStartTime = Game.GameTime;
                    _waypointAttempts = 0;
                    _waypointTeleportPending = true;
                    _waypointFocusActive = true;
                    Function.Call(Hash._SET_FOCUS_AREA, _waypointX, _waypointY, 500.0f, 0.0f, 0.0f, 0.0f);
                    _waypointLoadSceneStarted = Function.Call<bool>(Hash.NEW_LOAD_SCENE_START,
                        _waypointX, _waypointY, 500.0f,
                        0.0f, 0.0f, 0.0f, 300.0f, 0);
                    RequestWaypointCollision();
                    UI.Notify("~b~Loading waypoint area and finding ground...");
                }
                else
                {
                    UI.Notify("~r~Set a waypoint on the map first!");
                }
            };
            teleMenu.AddItem(wpItem);

            // Forward Teleport
            var fwdItem = new UIMenuItem(Localization.Get("Teleport", "ForwardTeleport", "Teleport 3m Forward"), Localization.Get("Teleport", "ForwardTeleportDesc", "Moves forward through doors"));
            fwdItem.Activated += (sender, selected) =>
            {
                Ped player = Game.Player.Character;
                Teleport(player.Position + player.ForwardVector * 3.0f);
            };
            teleMenu.AddItem(fwdItem);

            // Famous Locations
            AddLocation(teleMenu, Localization.Get("Teleport", "MazeBank", "Maze Bank Summit"), new Vector3(-75.015f, -818.215f, 327.376f));
            AddLocation(teleMenu, Localization.Get("Teleport", "VinewoodSign", "Vinewood Sign"), new Vector3(711.362f, 1198.134f, 349.726f));
            AddLocation(teleMenu, Localization.Get("Teleport", "MountChiliad", "Mount Chiliad Summit"), new Vector3(450.718f, 5566.614f, 807.383f));
            AddLocation(teleMenu, Localization.Get("Teleport", "Airport", "Los Santos International Airport"), new Vector3(-1037.7f, -2737.8f, 14.9f));
            AddLocation(teleMenu, Localization.Get("Teleport", "DelPerroPier", "Del Perro Pier"), new Vector3(-1850.127f, -1231.751f, 14.217f));
            AddLocation(teleMenu, Localization.Get("Teleport", "FortZancudo", "Fort Zancudo Military Base"), new Vector3(-2047.0f, 3132.0f, 33.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "Prison", "Bolingbroke Penitentiary"), new Vector3(1846.0f, 2585.0f, 46.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "SandyShoresAirfield", "Sandy Shores Airfield"), new Vector3(1710.2f, 3256.0f, 42.3f));
            AddLocation(teleMenu, Localization.Get("Teleport", "HumaneLabs", "Humane Labs"), new Vector3(3615.0f, 3740.0f, 28.0f));
            AddLocation(teleMenu, Localization.Get("Teleport", "GalileoObservatory", "Galileo Observatory"), new Vector3(-438.804f, 1076.097f, 353.611f));
        }

        private void AddLocation(UIMenu menu, string label, Vector3 coords)
        {
            var item = new UIMenuItem(label, "Teleport to " + label);
            item.Activated += (sender, selected) =>
            {
                Teleport(coords);
                UI.Notify("~b~Teleported: " + label);
            };
            menu.AddItem(item);
        }

        private void Teleport(Vector3 pos)
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            // Stream collision, then move the actual entity with the native directly.
            // SHVDN's Vehicle.Position setter can reject distant waypoint placement when
            // its internal safe-placement resolver cannot find a vehicle-sized position.
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, pos.X, pos.Y, pos.Z);
            int targetHandle = player.IsInVehicle() ? player.CurrentVehicle.Handle : player.Handle;
            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, targetHandle,
                pos.X, pos.Y, pos.Z, false, false, false);
        }

        public void ReloadSettings() { }

        public void OnTick()
        {
            if (!_waypointTeleportPending) return;

            _waypointAttempts++;
            int now = Game.GameTime;
            if (now - _waypointLastCollisionRequestTime >= 250)
            {
                _waypointLastCollisionRequestTime = now;
                Function.Call(Hash._SET_FOCUS_AREA, _waypointX, _waypointY, 500.0f, 0.0f, 0.0f, 0.0f);
                RequestWaypointCollision();
            }

            int elapsed = now - _waypointStartTime;
            bool sceneLoaded = Function.Call<bool>(Hash.IS_NEW_LOAD_SCENE_LOADED);

            // Never probe ground or teleport using stale collision data. Wait for the game
            // to report that the requested distant scene has finished streaming.
            bool mayProbe = elapsed >= 1200 && sceneLoaded &&
                now - _waypointLastProbeTime >= 150;
            if (mayProbe)
            {
                _waypointLastProbeTime = now;
                float groundZ;
                if (TryGetGroundHeight(_waypointX, _waypointY, out groundZ))
                {
                    CompleteWaypointTeleport(new Vector3(_waypointX, _waypointY, groundZ + 1.0f),
                        "~g~Teleported to waypoint at ground level.");
                    return;
                }

                // A waypoint can be placed over water, where terrain ground does not exist.
                OutputArgument waterHeight = new OutputArgument();
                if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, _waypointX, _waypointY, 1000.0f, waterHeight))
                {
                    float waterZ = waterHeight.GetResult<float>();
                    if (!float.IsNaN(waterZ) && !float.IsInfinity(waterZ) &&
                        waterZ > -200.0f && waterZ < 1200.0f)
                    {
                        CompleteWaypointTeleport(new Vector3(_waypointX, _waypointY, waterZ + 1.0f),
                            "~g~Teleported to waypoint at water level.");
                        return;
                    }
                }

                // Ask the game for a nearby navmesh-safe position if the exact point
                // is on an unusable slope or surface. Never accept a distant fallback.
                Vector3 safePosition = World.GetSafeCoordForPed(
                    new Vector3(_waypointX, _waypointY, 1000.0f), false, 0);
                if (safePosition != Vector3.Zero &&
                    Math.Abs(safePosition.X - _waypointX) <= 25.0f &&
                    Math.Abs(safePosition.Y - _waypointY) <= 25.0f &&
                    safePosition.Z > -200.0f && safePosition.Z < 1200.0f)
                {
                    CompleteWaypointTeleport(safePosition + new Vector3(0.0f, 0.0f, 1.0f),
                        "~g~Teleported to the nearest safe ground near waypoint.");
                    return;
                }
            }

            if (elapsed > 20000 || _waypointAttempts > 1200)
            {
                CancelWaypointTeleport();
                UI.Notify("~r~Could not resolve a safe surface at this waypoint. No teleport was made.");
            }
        }

        private void RequestWaypointCollision()
        {
            // Ask for collision at several elevations; one high-altitude request
            // is not enough for every terrain and interior streaming state.
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, _waypointX, _waypointY, 1000.0f);
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, _waypointX, _waypointY, 500.0f);
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, _waypointX, _waypointY, 100.0f);
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, _waypointX, _waypointY, 0.0f);
        }

        private void CompleteWaypointTeleport(Vector3 destination, string message)
        {
            _waypointTeleportPending = false;
            Teleport(destination);
            StopWaypointStreaming();
            UI.Notify(message);
        }

        private void CancelWaypointTeleport()
        {
            _waypointTeleportPending = false;
            StopWaypointStreaming();
        }

        private void StopWaypointStreaming()
        {
            if (_waypointLoadSceneStarted)
                Function.Call(Hash.NEW_LOAD_SCENE_STOP);
            if (_waypointFocusActive)
                Function.Call(Hash.CLEAR_FOCUS);

            _waypointLoadSceneStarted = false;
            _waypointFocusActive = false;
        }

        private bool TryGetGroundHeight(float x, float y, out float groundZ)
        {
            for (int i = 0; i < WaypointProbeHeights.Length; i++)
            {
                OutputArgument result = new OutputArgument();
                bool found = Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD,
                    x, y, WaypointProbeHeights[i], result, false, false);
                if (!found) continue;

                float z = result.GetResult<float>();
                if (float.IsNaN(z) || float.IsInfinity(z)) continue;
                if (z > WaypointProbeHeights[i] + 1.0f || z < -200.0f) continue;
                groundZ = z;
                return true;
            }
            groundZ = 0.0f;
            return false;
        }
    }
}
