using System.Diagnostics.CodeAnalysis;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.PowerCell;

public abstract class SharedPowerCellSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly ItemSlotsSystem ItemSlots = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PowerCellSlotComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<PowerCellSlotComponent, EntInsertedIntoContainerMessage>(OnCellInserted);
        SubscribeLocalEvent<PowerCellSlotComponent, EntRemovedFromContainerMessage>(OnCellRemoved);
        SubscribeLocalEvent<PowerCellSlotComponent, ContainerIsInsertingAttemptEvent>(OnCellInsertAttempt);

        SubscribeLocalEvent<PowerCellComponent, ExaminedEvent>(OnCellExamined);
        SubscribeLocalEvent<PowerCellSlotComponent, ExaminedEvent>(OnCellSlotExamined);
    }

    private void OnRejuvenate(EntityUid uid, PowerCellSlotComponent component, RejuvenateEvent args)
    {
        if (!ItemSlots.TryGetSlot(uid, component.CellSlotId, out var itemSlot) || !itemSlot.Item.HasValue)
            return;

        // charge entity batteries and remove booby traps.
        RaiseLocalEvent(itemSlot.Item.Value, args);
    }

    private void OnCellInsertAttempt(EntityUid uid, PowerCellSlotComponent component, ContainerIsInsertingAttemptEvent args)
    {
        if (!component.Initialized)
            return;

        if (args.Container.ID != component.CellSlotId)
            return;

        if (!HasComp<PowerCellComponent>(args.EntityUid))
        {
            args.Cancel();
        }
    }

    private void OnCellInserted(EntityUid uid, PowerCellSlotComponent component, EntInsertedIntoContainerMessage args)
    {
        if (!component.Initialized)
            return;

        if (args.Container.ID != component.CellSlotId)
            return;
        _appearance.SetData(uid, PowerCellSlotVisuals.Enabled, true);
        RaiseLocalEvent(uid, new PowerCellChangedEvent(false), false);
    }

    protected virtual void OnCellRemoved(EntityUid uid, PowerCellSlotComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.CellSlotId)
            return;
        _appearance.SetData(uid, PowerCellSlotVisuals.Enabled, false);
        RaiseLocalEvent(uid, new PowerCellChangedEvent(true), false);
    }

    public void SetPowerCellDrawEnabled(EntityUid uid, bool enabled, PowerCellDrawComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) || enabled == component.Drawing)
            return;

        component.Drawing = enabled;
        component.NextUpdateTime = Timing.CurTime;
    }

    /// <summary>
    /// Returns whether the entity has a slotted battery and <see cref="PowerCellDrawComponent.UseRate"/> charge.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="battery"></param>
    /// <param name="cell"></param>
    /// <param name="user">Popup to this user with the relevant detail if specified.</param>
    public abstract bool HasActivatableCharge(
        EntityUid uid,
        PowerCellDrawComponent? battery = null,
        PowerCellSlotComponent? cell = null,
        EntityUid? user = null);

    /// <summary>
    /// Whether the power cell has any power at all for the draw rate.
    /// </summary>
    public abstract bool HasDrawCharge(
        EntityUid uid,
        PowerCellDrawComponent? battery = null,
        PowerCellSlotComponent? cell = null,
        EntityUid? user = null);


    public bool TryGetBatteryFromSlot(EntityUid uid, [NotNullWhen(true)] out BatteryComponent? battery, PowerCellSlotComponent? component = null)
    {
        return TryGetBatteryFromSlot(uid, out _, out battery, component);
    }

    public bool TryGetBatteryFromSlot(EntityUid uid,
        [NotNullWhen(true)] out EntityUid? batteryEnt,
        [NotNullWhen(true)] out BatteryComponent? battery,
        PowerCellSlotComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
        {
            batteryEnt = null;
            battery = null;
            return false;
        }

        if (ItemSlots.TryGetSlot(uid, component.CellSlotId, out var slot))
        {
            batteryEnt = slot.Item;
            return TryComp(slot.Item, out battery);
        }

        batteryEnt = null;
        battery = null;
        return false;
    }

    private void OnCellExamined(EntityUid uid, PowerCellComponent component, ExaminedEvent args)
    {
        TryComp<BatteryComponent>(uid, out var battery);
        OnBatteryExamined(uid, battery, args);
    }
    private void OnCellSlotExamined(EntityUid uid, PowerCellSlotComponent component, ExaminedEvent args)
    {
        TryGetBatteryFromSlot(uid, out var battery);
        OnBatteryExamined(uid, battery, args);
    }

    private void OnBatteryExamined(EntityUid uid, BatteryComponent? component, ExaminedEvent args)
    {
        if (component != null)
        {
            var charge = component.CurrentCharge / component.MaxCharge * 100;
            args.PushMarkup(Loc.GetString("power-cell-component-examine-details", ("currentCharge", $"{charge:F0}")));
        }
        else
        {
            args.PushMarkup(Loc.GetString("power-cell-component-examine-details-no-battery"));
        }
    }
}
