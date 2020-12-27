﻿using System.Threading.Tasks;
using Content.Shared.GameObjects.Components;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.GameObjects;

namespace Content.Server.GameObjects.Components.Interactable
{
    // TODO make changes in icons when different threshold reached
    // e.g. different icons for 10% 50% 100%
    [RegisterComponent]
    public class MatchboxComponent : Component, IInteractUsing
    {
        public override string Name => "Matchbox";

        public int Priority => 1;

        public async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (eventArgs.Using.TryGetComponent<MatchstickComponent>(out var matchstick)
                && matchstick.CurrentState == MatchstickState.Unlit)
            {
                matchstick.Ignite(eventArgs.User);
                return true;
            }

            return false;
        }
    }
}
