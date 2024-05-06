namespace Content.Server.Stories.BlockMeleeAttack;

[RegisterComponent]
public sealed partial class BlockMeleeAttackUserComponent : Component
{
    [DataField("blockingItem")]
    public EntityUid? BlockingItem;
}
