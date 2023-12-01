using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Timing;

/// <summary>
/// Timer that creates a cooldown each time an object is activated/used
/// </summary>
[RegisterComponent]
[NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UseDelayComponent : Component
{
    /// <summary>
    /// When the delay ends.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("delayEnd", customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan DelayEndTime;

    /// <summary>
    /// Default delay
    /// </summary>
    [DataField("delay")]
    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);
}
