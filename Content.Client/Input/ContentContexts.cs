﻿using Content.Shared.Input;
using Robust.Shared.Input;

namespace Content.Client.Input
{
    /// <summary>
    ///     Contains a helper function for setting up all content
    ///     contexts, and modifying existing engine ones.
    /// </summary>
    public static class ContentContexts
    {
        public static void SetupContexts(IInputContextContainer contexts)
        {
            var common = contexts.GetContext("common");
            common.AddFunction(ContentKeyFunctions.FocusChat);
            common.AddFunction(ContentKeyFunctions.ExamineEntity);
            common.AddFunction(ContentKeyFunctions.OpenTutorial);
            common.AddFunction(ContentKeyFunctions.TakeScreenshot);
            common.AddFunction(ContentKeyFunctions.TakeScreenshotNoUI);

            var human = contexts.GetContext("human");
            human.AddFunction(ContentKeyFunctions.SwapHands);
            human.AddFunction(ContentKeyFunctions.Drop);
            human.AddFunction(ContentKeyFunctions.ActivateItemInHand);
            human.AddFunction(ContentKeyFunctions.OpenCharacterMenu);
            human.AddFunction(ContentKeyFunctions.ActivateItemInWorld);
            human.AddFunction(ContentKeyFunctions.ThrowItemInHand);
            human.AddFunction(ContentKeyFunctions.OpenContextMenu);
            human.AddFunction(ContentKeyFunctions.OpenCraftingMenu);
            human.AddFunction(ContentKeyFunctions.OpenInventoryMenu);
            human.AddFunction(ContentKeyFunctions.SmartEquipBackpack);
            human.AddFunction(ContentKeyFunctions.SmartEquipBelt);
            human.AddFunction(ContentKeyFunctions.MouseMiddle);
            human.AddFunction(ContentKeyFunctions.ToggleCombatMode);
            human.AddFunction(ContentKeyFunctions.WideAttack);
            human.AddFunction(ContentKeyFunctions.OpenAbilitiesMenu);
            human.AddFunction(ContentKeyFunctions.Hotbar0);
            human.AddFunction(ContentKeyFunctions.Hotbar1);
            human.AddFunction(ContentKeyFunctions.Hotbar2);
            human.AddFunction(ContentKeyFunctions.Hotbar3);
            human.AddFunction(ContentKeyFunctions.Hotbar4);
            human.AddFunction(ContentKeyFunctions.Hotbar5);
            human.AddFunction(ContentKeyFunctions.Hotbar6);
            human.AddFunction(ContentKeyFunctions.Hotbar7);
            human.AddFunction(ContentKeyFunctions.Hotbar8);
            human.AddFunction(ContentKeyFunctions.Hotbar9);

            var ghost = contexts.New("ghost", "common");
            ghost.AddFunction(EngineKeyFunctions.MoveUp);
            ghost.AddFunction(EngineKeyFunctions.MoveDown);
            ghost.AddFunction(EngineKeyFunctions.MoveLeft);
            ghost.AddFunction(EngineKeyFunctions.MoveRight);
            ghost.AddFunction(EngineKeyFunctions.Run);
            ghost.AddFunction(ContentKeyFunctions.OpenContextMenu);

            common.AddFunction(ContentKeyFunctions.OpenEntitySpawnWindow);
            common.AddFunction(ContentKeyFunctions.OpenSandboxWindow);
            common.AddFunction(ContentKeyFunctions.OpenTileSpawnWindow);
        }
    }
}
