using Content.Server.Gateway.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Gateway.Components;

/// <summary>
/// Controlling gateway that links to other gateway destinations on the server.
/// </summary>
[RegisterComponent, Access(typeof(GatewaySystem))]
public sealed partial class GatewayComponent : Component
{
    /// <summary>
    /// Sound to play when opening the portal.
    /// </summary>
    /// <remarks>
    /// Originally named PortalSound as it was used for opening and closing.
    /// </remarks>
    [DataField("portalSound")]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");

    /// <summary>
    /// Sound to play when closing the portal.
    /// </summary>
    [DataField]
    public SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");

    /// <summary>
    /// Sound to play when trying to open or close the portal and missing access.
    /// </summary>
    [DataField]
    public SoundSpecifier AccessDeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    /// <summary>
    /// Cooldown between opening portal / closing.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The time at which the portal was last opened.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastOpen;
}
