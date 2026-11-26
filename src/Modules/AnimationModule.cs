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
    public class AnimationModule : IAvernesModule
    {
        public string Name { get { return "Animations"; } }

        public void Initialize(UIMenu mainMenu, MenuPool menuPool)
        {
            var animMenu = menuPool.AddSubMenu(mainMenu, Localization.Get("General", "SubmenuAnimations", "Animations & Scenarios"));

            var stopItem = new UIMenuItem(
                Localization.Get("Animations", "StopAnim", "Stop Animation"),
                Localization.Get("Animations", "StopAnimDesc", "Return to the normal stance")
            );
            stopItem.Activated += (sender, selected) =>
            {
                Ped p = Game.Player.Character;
                if (p != null && p.Exists())
                {
                    p.Task.ClearAllImmediately();
                    p.Task.ClearAll();
                    Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, p.Handle);
                    UI.Notify("~y~Animation cancelled.");
                }
            };
            animMenu.AddItem(stopItem);

            AddScenarioItem(animMenu, Localization.Get("Animations", "Smoke", "Smoke a Cigarette"), "WORLD_HUMAN_SMOKING");
            AddScenarioItem(animMenu, Localization.Get("Animations", "DrinkBeer", "Drink Beer"), "WORLD_HUMAN_DRINKING");
            AddScenarioItem(animMenu, Localization.Get("Animations", "Party", "Party / Dance"), "WORLD_HUMAN_PARTYING");
            AddScenarioItem(animMenu, Localization.Get("Animations", "Sit", "Sit on the Ground"), "WORLD_HUMAN_PICNIC");
            AddScenarioItem(animMenu, Localization.Get("Animations", "Pushups", "Do Push-ups"), "WORLD_HUMAN_PUSH_UPS");
            AddScenarioItem(animMenu, "Do Yoga", "WORLD_HUMAN_YOGA");
            AddScenarioItem(animMenu, "Play Guitar", "WORLD_HUMAN_MUSICIAN");
            AddScenarioItem(animMenu, "Celebrate / Cheer", "WORLD_HUMAN_CHEERING");
            AddScenarioItem(animMenu, "Flex Muscles", "WORLD_HUMAN_MUSCLE_FLEX");
            AddScenarioItem(animMenu, "Binoculars", "WORLD_HUMAN_BINOCULARS");
            AddScenarioItem(animMenu, "Sit in a Chair", "PROP_HUMAN_SEAT_CHAIR");
            AddScenarioItem(animMenu, "Guard Stance", "WORLD_HUMAN_GUARD_STAND");
        }

        private void AddScenarioItem(UIMenu menu, string label, string scenarioName)
        {
            var item = new UIMenuItem(label, "Perform scenario " + label);
            item.Activated += (sender, selected) =>
            {
                Ped p = Game.Player.Character;
                if (p != null && p.Exists())
                {
                    p.Task.ClearAllImmediately();
                    Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, p.Handle, scenarioName, 0, true);
                    UI.Notify("~g~" + Localization.Get("Animations", "ScenarioStarted", "Scenario started:") + " ~w~" + label);
                }
            };
            menu.AddItem(item);
        }

        public void ReloadSettings() { }

        public void OnTick()
        {
        }
    }
}
