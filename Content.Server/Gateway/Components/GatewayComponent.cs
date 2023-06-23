using Content.Server.Gateway.Systems;

namespace Content.Server.Gateway.Components;

/// <summary>
/// Controlling gateway that links to other gateway destinations on the server.
/// </summary>
[RegisterComponent, Access(typeof(GatewaySystem))]
public sealed class GatewayComponent : Component
{
    /// <summary>
    /// Every other gateway destination on the server.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> Destinations = default!;

    /// <summary>
    /// The time at which the portal will be closed.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextClose;
}
