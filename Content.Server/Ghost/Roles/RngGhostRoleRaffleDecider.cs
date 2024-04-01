﻿using System.Linq;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Ghost.Roles;

/// <summary>
/// Chooses the winner of a ghost role raffle entirely randomly, without any weighting.
/// </summary>
public sealed partial class RngGhostRoleRaffleDecider : IGhostRoleRaffleDecider
{
    public void PickWinner(IEnumerable<ICommonSession> candidates, Func<ICommonSession, bool> tryTakeover)
    {
        // TODO: deciders should be able to access DI. surely there is a better way than this...?
        var random = IoCManager.Resolve<IRobustRandom>();

        var choices = candidates.ToList();
        random.Shuffle(choices); // shuffle the list so we can pick a lucky winner!

        foreach (var candidate in choices)
        {
            if (tryTakeover(candidate))
                return;
        }
    }
}
