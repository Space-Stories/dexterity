using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Robust.Shared.GameStates;

namespace Content.Shared.Dice;

public abstract class SharedDiceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiceComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DiceComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<DiceComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DiceComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<DiceComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, DiceComponent component, ref ComponentHandleState args)
    {
        if (args.Current is DiceComponent.DiceState state)
        {
            if (IsValidValue(state.CurrentValue, component) && IsValidSide(ValueToSide(state.CurrentValue, component), component))
            {
                SetCurrentValue(uid, state.CurrentValue, component);
            }
        }

        UpdateVisuals(uid, component);
    }

    private void OnGetState(EntityUid uid, DiceComponent component, ref ComponentGetState args)
    {
        args.State = new DiceComponent.DiceState(component.CurrentValue);
    }

    private void OnUseInHand(EntityUid uid, DiceComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Roll(uid, component);
    }

    private void OnLand(EntityUid uid, DiceComponent component, ref LandEvent args)
    {
        Roll(uid, component);
    }

    private void OnExamined(EntityUid uid, DiceComponent dice, ExaminedEvent args)
    {
        //No details check, since the sprite updates to show the side.
        args.PushMarkup(Loc.GetString("dice-component-on-examine-message-part-1", ("sidesAmount", dice.Sides)));
        args.PushMarkup(Loc.GetString("dice-component-on-examine-message-part-2", ("currentSide", dice.CurrentValue)));
    }

    public void SetCurrentSide(EntityUid uid, int side, DiceComponent? die = null)
    {
        if (!Resolve(uid, ref die))
            return;

        if (side < 1 || side > die.Sides)
        {
            Log.Error($"Attempted to set die {ToPrettyString(uid)} to an invalid side ({side}).");
            return;
        }

        die.CurrentValue = SideToValue(side, die);
        Dirty(die);
        UpdateVisuals(uid, die);
    }

    public void SetCurrentValue(EntityUid uid, int value, DiceComponent? die = null)
    {
        if (!Resolve(uid, ref die))
            return;

        if (!IsValidValue(value, die))
        {
            Log.Error($"Attempted to set die {ToPrettyString(uid)} to an invalid value ({value}).");
            return;
        }

        SetCurrentSide(uid, ValueToSide(value, die), die);
    }

    private int SideToValue(int side, DiceComponent die)
    {
        return (side - die.Offset) * die.Multiplier;
    }

    private int ValueToSide(int value, DiceComponent die)
    {
        return value / die.Multiplier + die.Offset;
    }

    private bool IsValidValue(int value, DiceComponent die)
    {
        return !(value % die.Multiplier != 0 || value / die.Multiplier + die.Offset < 1);
    }

    private bool IsValidSide(int side, DiceComponent die)
    {
        return !(side < 1 || side > die.Sides);
    }

    protected virtual void UpdateVisuals(EntityUid uid, DiceComponent? die = null)
    {
        // See client system.
    }

    public virtual void Roll(EntityUid uid, DiceComponent? die = null)
    {
        // See the server system, client cannot predict rolling.
    }
}
