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
    public class MiscPowersModule : IAvernesModule
    {
        public string Name { get { return "Powers"; } }

        private bool _noClip = false;
        private bool _nightVision = false;
        private bool _thermalVision = false;
        private bool _fallProtectionActive = false;

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var powerMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuPowers", "Superpowers & Vision"));

            // NoClip
            var noClipItem = new UIMenuCheckboxItem(Localization.Get("Powers", "NoClip", "Flight Mode (No-Clip)"), _noClip, Localization.Get("Powers", "NoClipDesc", "Fly freely and pass through walls"));
            noClipItem.CheckboxEvent += (sender, state) =>
            {
                _noClip = state;
                Ped player = Game.Player.Character;
                if (player != null && player.Exists())
                {
                    player.HasCollision = !_noClip;
                    Function.Call(Hash.SET_ENTITY_COLLISION, player.Handle, !_noClip, true);
                    player.FreezePosition = _noClip;

                    if (_noClip)
                    {
                        player.IsInvincible = true;
                        _fallProtectionActive = false;
                    }
                    else
                    {
                        // Fall protection: protect player until they safely touch ground
                        _fallProtectionActive = true;
                        player.CanRagdoll = false;
                        Function.Call(Hash.SET_PED_CAN_RAGDOLL, player.Handle, false);
                        Function.Call(Hash.SET_ENTITY_PROOFS, player.Handle, true, true, true, true, true, true, true, true);
                        player.IsInvincible = true;
                    }
                }
            };
            powerMenu.AddItem(noClipItem);

            // Night Vision
            var nightItem = new UIMenuCheckboxItem(Localization.Get("Powers", "NightVision", "Night Vision"), _nightVision, Localization.Get("Powers", "NightVisionDesc", "Military green filter"));
            nightItem.CheckboxEvent += (sender, state) =>
            {
                _nightVision = state;
                Function.Call(Hash.SET_NIGHTVISION, _nightVision);
            };
            powerMenu.AddItem(nightItem);

            // Thermal Vision
            var thermItem = new UIMenuCheckboxItem(Localization.Get("Powers", "ThermalVision", "Thermal Vision"), _thermalVision, Localization.Get("Powers", "ThermalVisionDesc", "Infrared heat filter"));
            thermItem.CheckboxEvent += (sender, state) =>
            {
                _thermalVision = state;
                Function.Call(Hash.SET_SEETHROUGH, _thermalVision);
            };
            powerMenu.AddItem(thermItem);
        }

        public void OnTick()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            if (_noClip)
            {
                player.HasCollision = false;
                Function.Call(Hash.SET_ENTITY_COLLISION, player.Handle, false, true);
                player.FreezePosition = true;

                float speed = 0.8f;
                if (Game.IsKeyPressed(Keys.ShiftKey)) speed = 2.5f;

                Vector3 camDir = GameplayCamera.Direction;
                Vector3 rightDir = Vector3.Cross(camDir, new Vector3(0, 0, 1));
                Vector3 newPos = player.Position;

                if (Game.IsKeyPressed(Keys.W)) newPos += camDir * speed;
                if (Game.IsKeyPressed(Keys.S)) newPos -= camDir * speed;
                if (Game.IsKeyPressed(Keys.D)) newPos += rightDir * speed;
                if (Game.IsKeyPressed(Keys.A)) newPos -= rightDir * speed;
                if (Game.IsKeyPressed(Keys.Space)) newPos.Z += speed;
                if (Game.IsKeyPressed(Keys.ControlKey)) newPos.Z -= speed;

                player.Position = newPos;
                player.Heading = GameplayCamera.Rotation.Z;
            }
            else if (_fallProtectionActive)
            {
                // Deactivate fall protection only when safely on ground or in vehicle/water
                if (!player.IsInAir || player.HeightAboveGround < 1.5f || player.IsInWater || player.IsInVehicle())
                {
                    _fallProtectionActive = false;
                    player.CanRagdoll = true;
                    Function.Call(Hash.SET_PED_CAN_RAGDOLL, player.Handle, true);
                    Function.Call(Hash.SET_ENTITY_PROOFS, player.Handle, false, false, false, false, false, false, false, false);
                    if (!PlayerModule.IsGodModeActive)
                    {
                        player.IsInvincible = false;
                    }
                }
            }
        }
    }
}
