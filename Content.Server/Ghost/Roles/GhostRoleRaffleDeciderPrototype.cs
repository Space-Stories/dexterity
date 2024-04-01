﻿using Robust.Shared.Prototypes;

namespace Content.Server.Ghost.Roles;

/// <summary>
/// Allows getting a <see cref="IGhostRoleRaffleDecider"/> as prototype.
/// </summary>
[Prototype("ghostRoleRaffleDecider")]
public sealed class GhostRoleRaffleDeciderPrototype : IPrototype
{
    /// <inheritdoc />
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The <see cref="IGhostRoleRaffleDecider"/> instance that chooses the winner of a raffle.
    /// </summary>
    [DataField("decider", required: true)]
    public IGhostRoleRaffleDecider Decider { get; private set; } = new RngGhostRoleRaffleDecider();
}
