using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization;

namespace Content.Shared.Ninja.Components;

// basically emag but without immune tag, TODO: make the charge thing its own component and have emag use it too
[RegisterComponent, NetworkedComponent]
public sealed class EnergyKatanaComponent : Component
{
    public EntityUid? Ninja = null;

    /// <summary>
    /// Sound played when using dash action.
    /// </summary>
    [DataField("blinkSound")]
    public SoundSpecifier BlinkSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg");

    /// <summary>
    /// Volume control for katana dash action.
    /// </summary>
    [DataField("blinkVolume")]
    public float BlinkVolume = 5f;

    /// <summary>
    /// The maximum number of dash charges the katana can have
    /// </summary>
    [DataField("maxCharges"), ViewVariables(VVAccess.ReadWrite)]
    public int MaxCharges = 3;

    /// <summary>
    /// The current number of dash charges on the katana
    /// </summary>
    [DataField("charges"), ViewVariables(VVAccess.ReadWrite)]
    public int Charges = 3;

    /// <summary>
    /// Whether or not the katana automatically recharges over time.
    /// </summary>
    [DataField("autoRecharge"), ViewVariables(VVAccess.ReadWrite)]
    public bool AutoRecharge = true;

    /// <summary>
    /// The time it takes to regain a single dash charge
    /// </summary>
    [DataField("rechargeDuration"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RechargeDuration = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The time when the next dash charge will be added
    /// </summary>
    [DataField("nextChargeTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextChargeTime = TimeSpan.MaxValue;
}

public sealed class KatanaDashEvent : WorldTargetActionEvent { }

[Serializable, NetSerializable]
public sealed class EnergyKatanaComponentState : ComponentState
{
    public int MaxCharges;
    public int Charges;
    public bool AutoRecharge;
    public TimeSpan RechargeTime;
    public TimeSpan NextChargeTime;

    public EnergyKatanaComponentState(int maxCharges, int charges, TimeSpan rechargeTime, TimeSpan nextChargeTime, bool autoRecharge)
    {
        MaxCharges = maxCharges;
        Charges = charges;
        RechargeTime = rechargeTime;
        NextChargeTime = nextChargeTime;
        AutoRecharge = autoRecharge;
    }
}
