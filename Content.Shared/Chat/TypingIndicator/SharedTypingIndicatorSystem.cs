using Content.Shared.Clothing;

namespace Content.Shared.Chat.TypingIndicator;

/// <summary>
///     Sync typing indicator icon between client and server.
/// </summary>
public abstract class SharedTypingIndicatorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypingIndicatorClothingComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<TypingIndicatorClothingComponent, ClothingGotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(EntityUid uid, TypingIndicatorClothingComponent component, ClothingGotEquippedEvent args)
    {
        if (!TryComp<TypingIndicatorComponent>(args.Wearer, out var indicator))
            return;
        indicator.Override = true;
        indicator.OverrideIndicator = component.Prototype;
    }

    private void OnGotUnequipped(EntityUid uid, TypingIndicatorClothingComponent component, ClothingGotUnequippedEvent args)
    {
        if (!TryComp<TypingIndicatorComponent>(args.Wearer, out var indicator))
            return;

        indicator.Override = false;
        indicator.OverrideIndicator = "";
    }
}
