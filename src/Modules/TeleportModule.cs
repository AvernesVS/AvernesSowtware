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
                    _waypointX = wp.X;
                    _waypointY = wp.Y;
                    _waypointStartTime = Game.GameTime;
                    _waypointAttempts = 0;
                    _waypointTeleportPending = true;
                    UI.Notify("~b~Finding safe ground at waypoint...");
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

            // Ask the game to stream collision before moving the player/entity.
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, pos.X, pos.Y, pos.Z);

            if (player.IsInVehicle())
            {
                player.CurrentVehicle.Position = pos;
            }
            else
            {
                player.Position = pos;
            }
        }

        public void OnTick()
        {
            if (!_waypointTeleportPending) return;

            _waypointAttempts++;
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, _waypointX, _waypointY, 1000.0f);

            float groundZ;
            if (TryGetGroundHeight(_waypointX, _waypointY, out groundZ))
            {
                _waypointTeleportPending = false;
                Teleport(new Vector3(_waypointX, _waypointY, groundZ + 1.0f));
                UI.Notify("~g~Teleported to waypoint at ground level.");
                return;
            }

            // Water waypoints have no terrain ground; use the actual water surface.
            OutputArgument waterHeight = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, _waypointX, _waypointY, 1000.0f, waterHeight))
            {
                float z = waterHeight.GetResult<float>();
                if (!float.IsNaN(z) && !float.IsInfinity(z))
                {
                    _waypointTeleportPending = false;
                    Teleport(new Vector3(_waypointX, _waypointY, z + 1.0f));
                    UI.Notify("~g~Teleported to waypoint at water level.");
                    return;
                }
            }

            if (Game.GameTime - _waypointStartTime > 5000 || _waypointAttempts > 180)
            {
                _waypointTeleportPending = false;
                UI.Notify("~r~Could not find safe ground at this waypoint. You were not teleported.");
            }
        }

        private bool TryGetGroundHeight(float x, float y, out float groundZ)
        {
            float[] probeHeights = new float[] { 1000.0f, 750.0f, 500.0f, 250.0f, 100.0f, 50.0f, 20.0f, 0.0f };
            for (int i = 0; i < probeHeights.Length; i++)
            {
                OutputArgument result = new OutputArgument();
                bool found = Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD,
                    x, y, probeHeights[i], result, false);
                if (!found) continue;

                float z = result.GetResult<float>();
                if (float.IsNaN(z) || float.IsInfinity(z)) continue;
                if (z > probeHeights[i] + 1.0f || z < -200.0f) continue;
                groundZ = z;
                return true;
            }
            groundZ = 0.0f;
            return false;
        }
    }
}
