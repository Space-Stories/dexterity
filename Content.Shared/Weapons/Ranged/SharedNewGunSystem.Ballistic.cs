using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Barrels.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged;

public abstract partial class SharedNewGunSystem
{
    protected virtual void InitializeBallistic()
    {
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ComponentInit>(OnBallisticInit);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, TakeAmmoEvent>(OnBallisticTakeAmmo);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ComponentGetState>(OnBallisticGetState);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ComponentHandleState>(OnBallisticHandleState);

        SubscribeLocalEvent<BallisticAmmoProviderComponent, ExaminedEvent>(OnBallisticExamine);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, GetVerbsEvent<Verb>>(OnBallisticVerb);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, InteractUsingEvent>(OnBallisticInteractUsing);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ActivateInWorldEvent>(OnBallisticActivate);
    }

    private void OnBallisticActivate(EntityUid uid, BallisticAmmoProviderComponent component, ActivateInWorldEvent args)
    {
        if (!component.Cycled)
        {
            component.Cycled = true;
            return;
        }

        ManualCycle(component, Transform(uid).MapPosition);
    }

    private void OnBallisticInteractUsing(EntityUid uid, BallisticAmmoProviderComponent component, InteractUsingEvent args)
    {
        if (args.Handled || component.Whitelist?.IsValid(args.Used, EntityManager) != true) return;

        if (GetBallisticShots(component) >= component.Capacity) return;

        component.Entities.Push(args.Used);
        component.Container.Insert(args.Used);
        // Not predicted so
        PlaySound(uid, component.SoundInsert?.GetSound(), args.User);
        args.Handled = true;
        UpdateBallisticAppearance(component);
        Dirty(component);
    }

    private void OnBallisticVerb(EntityUid uid, BallisticAmmoProviderComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract) return;

        args.Verbs.Add(new Verb()
        {
            Text = "Cycle",
            Disabled = GetBallisticShots(component) == 0,
            Act = () => ManualCycle(component, Transform(uid).MapPosition),
        });
    }

    private void OnBallisticExamine(EntityUid uid, BallisticAmmoProviderComponent component, ExaminedEvent args)
    {
        args.PushMarkup($"It has [color={AmmoExamineColor}]{GetBallisticShots(component)}[/yellow] ammo.");
    }

    // Not implemented here as the client will replay it a billion times.
    public abstract void ManualCycle(BallisticAmmoProviderComponent component, MapCoordinates coordinates);

    private void OnBallisticGetState(EntityUid uid, BallisticAmmoProviderComponent component, ref ComponentGetState args)
    {
        args.State = new BallisticAmmoProviderComponentState()
        {
            UnspawnedCount = component.UnspawnedCount,
            Entities = component.Entities,
            Cycled = component.Cycled,
        };
    }

    private void OnBallisticHandleState(EntityUid uid, BallisticAmmoProviderComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not BallisticAmmoProviderComponentState state) return;

        component.UnspawnedCount = state.UnspawnedCount;
        component.Entities = state.Entities;
        component.Cycled = state.Cycled;
    }

    private void OnBallisticInit(EntityUid uid, BallisticAmmoProviderComponent component, ComponentInit args)
    {
        component.Container = Containers.EnsureContainer<Container>(uid, "ballistic-ammo");
        component.UnspawnedCount = component.Capacity;

        if (component.FillProto != null)
        {
            component.UnspawnedCount -= Math.Min(component.UnspawnedCount, component.Container.ContainedEntities.Count);
        }
        else
        {
            component.UnspawnedCount = 0;
        }
    }

    protected int GetBallisticShots(BallisticAmmoProviderComponent component)
    {
        return component.Entities.Count + component.UnspawnedCount;
    }

    private void OnBallisticTakeAmmo(EntityUid uid, BallisticAmmoProviderComponent component, TakeAmmoEvent args)
    {
        for (var i = 0; i < args.Shots; i++)
        {
            if (!component.Cycled) break;

            if (component.Entities.TryPop(out var existing))
            {
                component.Container.Remove(existing);
                args.Ammo.Add(EnsureComp<NewAmmoComponent>(existing));
            }
            else if (component.UnspawnedCount > 0)
            {
                component.UnspawnedCount--;
                var ent = Spawn(component.FillProto, args.Coordinates);
                args.Ammo.Add(EnsureComp<NewAmmoComponent>(ent));
            }

            if (!component.AutoCycle)
            {
                component.Cycled = false;
            }
        }

        UpdateBallisticAppearance(component);
        Dirty(component);
    }

    protected void UpdateBallisticAppearance(BallisticAmmoProviderComponent component)
    {
        if (Timing.IsFirstTimePredicted && TryComp<AppearanceComponent>(component.Owner, out var appearance))
        {
            appearance.SetData(AmmoVisuals.AmmoCount, GetBallisticShots(component));
            appearance.SetData(AmmoVisuals.AmmoMax, component.Capacity);
        }
    }

    [Serializable, NetSerializable]
    private sealed class BallisticAmmoProviderComponentState : ComponentState
    {
        public int UnspawnedCount;
        public Stack<EntityUid> Entities = default!;
        public bool Cycled;
    }
}
