using System.Threading;
using Content.Server.Administration.Logs;
using Content.Server.DoAfter;
using Content.Server.Medical.Components;
using Content.Server.Stack;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Helpers;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.Medical;

public sealed class HealingSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AdminLogSystem _logs = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly StackSystem _stacks = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HealingComponent, AfterInteractEvent>(OnHealingAfterInteract);
        SubscribeLocalEvent<HealingCancelledEvent>(OnHealingCancelled);
        SubscribeLocalEvent<DamageableComponent, HealingCompleteEvent>(OnHealingComplete);
    }

    private void OnHealingComplete(EntityUid uid, DamageableComponent component, HealingCompleteEvent args)
    {
        if (TryComp<StackComponent>(args.Component.Owner, out var stack) && stack.Count < 1) return;

        if (component.DamageContainerID is not null &&
            !component.DamageContainerID.Equals(component.DamageContainerID)) return;

        var healed = _damageable.TryChangeDamage(uid, args.Component.Damage, true);

        // Reverify that we can heal the damage.
        if (healed == null)
            return;

        _stacks.Use(args.Component.Owner, 1, stack);

        if (uid != args.User)
            _logs.Add(LogType.Healed, $"{EntityManager.ToPrettyString(args.User):user} healed {EntityManager.ToPrettyString(uid):target} for {healed.Total:damage} damage");
        else
            _logs.Add(LogType.Healed, $"{EntityManager.ToPrettyString(args.User):user} healed themselves for {healed.Total:damage} damage");
    }

    private static void OnHealingCancelled(HealingCancelledEvent ev)
    {
        ev.Component.CancelToken = null;
    }

    private void OnHealingAfterInteract(EntityUid uid, HealingComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach) return;

        if (component.CancelToken != null)
        {
            component.CancelToken?.Cancel();
            component.CancelToken = null;
            args.Handled = true;
            return;
        }

        if (args.Target == null)
        {
            return;
        }

        args.Handled = true;

        if (!TryComp<DamageableComponent>(args.Target.Value, out var targetDamage))
            return;

        if (component.DamageContainerID is not null && !component.DamageContainerID.Equals(targetDamage.DamageContainerID))
            return;

        if (!_blocker.CanInteract(args.User))
            return;

        if (args.User != args.Target &&
            !args.User.InRangeUnobstructed(args.Target.Value, ignoreInsideBlocker: true, popup: true))
        {
            return;
        }

        if (TryComp<SharedStackComponent>(uid, out var stack) && stack.Count < 1)
            return;

        component.CancelToken = new CancellationTokenSource();

        _doAfter.DoAfter(new DoAfterEventArgs(args.User, component.Delay, component.CancelToken.Token, args.Target)
        {
            BreakOnUserMove = true,
            BreakOnTargetMove = true,
            // Didn't break on damage as they may be trying to prevent it and
            // not being able to heal your own ticking damage would be frustrating.
            BreakOnStun = true,
            NeedHand = true,
            TargetFinishedEvent = new HealingCompleteEvent
            {
                User = args.User,
                Component = component,
            },
            BroadcastCancelledEvent = new HealingCancelledEvent
            {
                Component = component,
            },
            // Juusstt in case damageble gets removed it avoids having to re-cancel the token. Won't need this when DoAfterEvent<T> gets added.
            PostCheck = () =>
            {
                component.CancelToken = null;
                return true;
            },
        });
    }

    private sealed class HealingCompleteEvent : EntityEventArgs
    {
        public EntityUid User;
        public HealingComponent Component = default!;
    }

    private sealed class HealingCancelledEvent : EntityEventArgs
    {
        public HealingComponent Component = default!;
    }
}
