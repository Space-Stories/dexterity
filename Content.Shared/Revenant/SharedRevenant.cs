using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Revenant;

public sealed class SoulSearchDoAfterComplete : EntityEventArgs
{
    public readonly EntityUid Target;

    public SoulSearchDoAfterComplete(EntityUid target)
    {
        Target = target;
    }
}

public sealed class SoulSearchDoAfterCancelled : EntityEventArgs { }

public sealed class HarvestDoAfterComplete : EntityEventArgs
{
    public readonly EntityUid Target;

    public HarvestDoAfterComplete(EntityUid target)
    {
        Target = target;
    }
}

public sealed class HarvestDoAfterCancelled : EntityEventArgs { }
public sealed partial class RevenantShopActionEvent : InstantActionEvent { }
public sealed partial class RevenantDefileActionEvent : InstantActionEvent { }
public sealed partial class RevenantOverloadLightsActionEvent : InstantActionEvent { }
public sealed partial class RevenantBlightActionEvent : InstantActionEvent { }
public sealed partial class RevenantMalfunctionActionEvent : InstantActionEvent { }

[NetSerializable, Serializable]
public enum RevenantVisuals : byte
{
    Corporeal,
    Stunned,
    Harvesting,
}
