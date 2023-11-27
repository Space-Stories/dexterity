using Content.Server.Body.Components;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Containers.Components;
using Content.Shared.Chemistry.Containers.Events;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Solutions;
using Content.Shared.Chemistry.Solutions.Components;
using Content.Shared.Chemistry.Solutions.EntitySystems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameStates;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Content.Shared.Stacks;
using Robust.Shared.Player;

namespace Content.Server.Chemistry.EntitySystems;

public sealed partial class ChemistrySystem
{

    /// <summary>
    ///     Default transfer amounts for the set-transfer verb.
    /// </summary>
    public static readonly List<int> TransferAmounts = new() { 1, 5, 10, 15 };
    private void InitializeInjector()
    {
        SubscribeLocalEvent<InjectorComponent, GetVerbsEvent<AlternativeVerb>>(AddSetTransferVerbs);
        SubscribeLocalEvent<InjectorComponent, SolutionChangedEvent>(OnSolutionChange);
        SubscribeLocalEvent<InjectorComponent, InjectorDoAfterEvent>(OnInjectDoAfter);
        SubscribeLocalEvent<InjectorComponent, ComponentStartup>(OnInjectorStartup);
        SubscribeLocalEvent<InjectorComponent, UseInHandEvent>(OnInjectorUse);
        SubscribeLocalEvent<InjectorComponent, AfterInteractEvent>(OnInjectorAfterInteract);
        SubscribeLocalEvent<InjectorComponent, ComponentGetState>(OnInjectorGetState);
    }

    private void AddSetTransferVerbs(EntityUid uid, InjectorComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (!EntityManager.TryGetComponent(args.User, out ActorComponent? actor))
            return;

        // Add specific transfer verbs according to the container's size
        var priority = 0;
        foreach (var amount in TransferAmounts)
        {
            if (amount < component.MinimumTransferAmount.Int() || amount > component.MaximumTransferAmount.Int())
                continue;

            AlternativeVerb verb = new();
            verb.Text = Loc.GetString("comp-solution-transfer-verb-amount", ("amount", amount));
            verb.Category = VerbCategory.SetTransferAmount;
            verb.Act = () =>
            {
                component.TransferAmount = FixedPoint2.New(amount);
                _popup.PopupEntity(Loc.GetString("comp-solution-transfer-set-amount", ("amount", amount)), args.User, args.User);
            };

            // we want to sort by size, not alphabetically by the verb text.
            verb.Priority = priority;
            priority--;

            args.Verbs.Add(verb);
        }
    }

    private void UseInjector(Entity<InjectorComponent> injector, EntityUid target, EntityUid user)
    {
        // Handle injecting/drawing for solutions
        if (injector.Comp.ToggleState == SharedInjectorComponent.InjectorToggleMode.Inject)
        {
            if (_solutionContainers.TryGetInjectableSolution(target, out var injectableSolution, out _))
            {
                TryInject(injector, target, injectableSolution, user, false);
            }
            else if (_solutionContainers.TryGetRefillableSolution(target, out var refillableSolution, out _))
            {
                TryInject(injector, target, refillableSolution, user, true);
            }
            else if (TryComp<BloodstreamComponent>(target, out var bloodstream))
            {
                TryInjectIntoBloodstream(injector, (target, bloodstream), user);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("injector-component-cannot-transfer-message",
                    ("target", Identity.Entity(target, EntityManager))), injector, user);
            }
        }
        else if (injector.Comp.ToggleState == SharedInjectorComponent.InjectorToggleMode.Draw)
        {
            // Draw from a bloodstream, if the target has that
            if (TryComp<BloodstreamComponent>(target, out var stream) &&
                _solutionContainers.TryGetSolution(target, stream.BloodSolutionName, out var bloodSolution, out _))
            {
                TryDraw(injector, (target, stream), bloodSolution, user);
                return;
            }

            // Draw from an object (food, beaker, etc)
            if (_solutionContainers.TryGetDrawableSolution(target, out var drawableSolution, out _))
            {
                TryDraw(injector, target, drawableSolution, user);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("injector-component-cannot-draw-message",
                    ("target", Identity.Entity(target, EntityManager))), injector.Owner, user);
            }
        }
    }

    private void OnSolutionChange(EntityUid uid, InjectorComponent component, SolutionChangedEvent args)
    {
        Dirty(uid, component);
    }

    private void OnInjectorGetState(EntityUid uid, InjectorComponent component, ref ComponentGetState args)
    {
        _solutionContainers.TryGetSolution(uid, InjectorComponent.SolutionName, out _, out var solution);

        var currentVolume = solution?.Volume ?? FixedPoint2.Zero;
        var maxVolume = solution?.MaxVolume ?? FixedPoint2.Zero;

        args.State = new SharedInjectorComponent.InjectorComponentState(currentVolume, maxVolume, component.ToggleState);
    }

    private void OnInjectDoAfter(EntityUid uid, InjectorComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        UseInjector((uid, component), args.Args.Target.Value, args.Args.User);
        args.Handled = true;
    }

    private void OnInjectorAfterInteract(EntityUid uid, InjectorComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        //Make sure we have the attacking entity
        if (args.Target is not { Valid: true } target || !HasComp<SolutionContainerComponent>(uid))
            return;

        // Is the target a mob? If yes, use a do-after to give them time to respond.
        if (HasComp<MobStateComponent>(target) || HasComp<BloodstreamComponent>(target))
        {
            // Are use using an injector capible of targeting a mob?
            if (component.IgnoreMobs)
                return;

            InjectDoAfter((uid, component), target, args.User);
            args.Handled = true;
            return;
        }

        UseInjector((uid, component), target, args.User);
        args.Handled = true;
    }

    private void OnInjectorStartup(EntityUid uid, InjectorComponent component, ComponentStartup args)
    {
        // ???? why ?????
        Dirty(uid, component);
    }

    private void OnInjectorUse(EntityUid uid, InjectorComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        Toggle(component, args.User, uid);
        args.Handled = true;
    }

    /// <summary>
    /// Toggle between draw/inject state if applicable
    /// </summary>
    private void Toggle(InjectorComponent component, EntityUid user, EntityUid injector)
    {
        if (component.InjectOnly)
        {
            return;
        }

        string msg;
        switch (component.ToggleState)
        {
            case SharedInjectorComponent.InjectorToggleMode.Inject:
                component.ToggleState = SharedInjectorComponent.InjectorToggleMode.Draw;
                msg = "injector-component-drawing-text";
                break;
            case SharedInjectorComponent.InjectorToggleMode.Draw:
                component.ToggleState = SharedInjectorComponent.InjectorToggleMode.Inject;
                msg = "injector-component-injecting-text";
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _popup.PopupEntity(Loc.GetString(msg), injector, user);
    }

    /// <summary>
    /// Send informative pop-up messages and wait for a do-after to complete.
    /// </summary>
    private void InjectDoAfter(Entity<InjectorComponent> injector, EntityUid target, EntityUid user)
    {
        // Create a pop-up for the user
        _popup.PopupEntity(Loc.GetString("injector-component-injecting-user"), target, user);

        if (!_solutionContainers.TryGetSolution(injector.Owner, InjectorComponent.SolutionName, out _, out var solution))
            return;

        var actualDelay = MathF.Max(injector.Comp.Delay, 1f);

        // Injections take 0.5 seconds longer per additional 5u
        actualDelay += (float) injector.Comp.TransferAmount / injector.Comp.Delay - 0.5f;

        var isTarget = user != target;

        if (isTarget)
        {
            // Create a pop-up for the target
            var userName = Identity.Entity(user, EntityManager);
            _popup.PopupEntity(Loc.GetString("injector-component-injecting-target",
                ("user", userName)), user, target);

            // Check if the target is incapacitated or in combat mode and modify time accordingly.
            if (_mobState.IsIncapacitated(target))
            {
                actualDelay /= 2.5f;
            }
            else if (_combat.IsInCombatMode(target))
            {
                // Slightly increase the delay when the target is in combat mode. Helps prevents cheese injections in
                // combat with fast syringes & lag.
                actualDelay += 1;
            }

            // Add an admin log, using the "force feed" log type. It's not quite feeding, but the effect is the same.
            if (injector.Comp.ToggleState == SharedInjectorComponent.InjectorToggleMode.Inject)
            {
                _adminLogger.Add(LogType.ForceFeed,
                    $"{EntityManager.ToPrettyString(user):user} is attempting to inject {EntityManager.ToPrettyString(target):target} with a solution {SolutionContainerSystem.ToPrettyString(solution):solution}");
            }
        }
        else
        {
            // Self-injections take half as long.
            actualDelay /= 2;

            if (injector.Comp.ToggleState == SharedInjectorComponent.InjectorToggleMode.Inject)
                _adminLogger.Add(LogType.Ingestion, $"{EntityManager.ToPrettyString(user):user} is attempting to inject themselves with a solution {SolutionContainerSystem.ToPrettyString(solution):solution}.");
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, actualDelay, new InjectorDoAfterEvent(), injector.Owner, target: target, used: injector.Owner)
        {
            BreakOnUserMove = true,
            BreakOnDamage = true,
            BreakOnTargetMove = true,
            MovementThreshold = 0.1f,
        });
    }

    private void TryInjectIntoBloodstream(Entity<InjectorComponent> injector, Entity<BloodstreamComponent> target, EntityUid user)
    {
        // Get transfer amount. May be smaller than _transferAmount if not enough room
        if (!_solutionContainers.TryGetSolution(target.Owner, target.Comp.ChemicalSolutionName, out var chemSoln, out var chemSolution))
        {
            _popup.PopupEntity(Loc.GetString("injector-component-cannot-inject-message", ("target", Identity.Entity(target, EntityManager))), injector.Owner, user);
            return;
        }

        var realTransferAmount = FixedPoint2.Min(injector.Comp.TransferAmount, chemSolution.AvailableVolume);
        if (realTransferAmount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("injector-component-cannot-inject-message", ("target", Identity.Entity(target, EntityManager))), injector.Owner, user);
            return;
        }

        // Move units from attackSolution to targetSolution
        var removedSolution = _solutions.SplitSolution(chemSoln, realTransferAmount);

        _blood.TryAddToChemicals(target, removedSolution, target.Comp);

        _reactiveSystem.DoEntityReaction(target, removedSolution, ReactionMethod.Injection);

        _popup.PopupEntity(Loc.GetString("injector-component-inject-success-message",
                ("amount", removedSolution.Volume),
                ("target", Identity.Entity(target, EntityManager))), injector.Owner, user);

        Dirty(injector);
        AfterInject(injector);
    }

    private void TryInject(Entity<InjectorComponent> injector, EntityUid targetEntity, Entity<SolutionComponent> targetSolution, EntityUid user, bool asRefill)
    {
        if (!_solutionContainers.TryGetSolution(injector.Owner, InjectorComponent.SolutionName, out var soln, out var solution) || solution.Volume == 0)
            return;

        // Get transfer amount. May be smaller than _transferAmount if not enough room
        var realTransferAmount = FixedPoint2.Min(injector.Comp.TransferAmount, targetSolution.Comp.Solution.AvailableVolume);

        if (realTransferAmount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("injector-component-target-already-full-message", ("target", Identity.Entity(targetEntity, EntityManager))),
                injector.Owner, user);
            return;
        }

        // Move units from attackSolution to targetSolution
        Solution removedSolution;
        if (TryComp<StackComponent>(targetEntity, out var stack))
            removedSolution = _solutions.SplitStackSolution(soln, realTransferAmount, stack.Count);
        else
            removedSolution = _solutions.SplitSolution(soln, realTransferAmount);

        _reactiveSystem.DoEntityReaction(targetEntity, removedSolution, ReactionMethod.Injection);

        if (!asRefill)
            _solutionContainers.Inject(targetEntity, targetSolution, removedSolution);
        else
            _solutionContainers.Refill(targetEntity, targetSolution, removedSolution);

        _popup.PopupEntity(Loc.GetString("injector-component-transfer-success-message",
                ("amount", removedSolution.Volume),
                ("target", Identity.Entity(targetEntity, EntityManager))), injector.Owner, user);

        Dirty(injector);
        AfterInject(injector);
    }

    private void AfterInject(Entity<InjectorComponent> injector)
    {
        // Automatically set syringe to draw after completely draining it.
        if (_solutionContainers.TryGetSolution(injector.Owner, InjectorComponent.SolutionName, out _, out var solution) && solution.Volume == 0)
        {
            injector.Comp.ToggleState = SharedInjectorComponent.InjectorToggleMode.Draw;
        }
    }

    private void AfterDraw(Entity<InjectorComponent> injector)
    {
        // Automatically set syringe to inject after completely filling it.
        if (_solutionContainers.TryGetSolution(injector.Owner, InjectorComponent.SolutionName, out _, out var solution) && solution.AvailableVolume == 0)
        {
            injector.Comp.ToggleState = SharedInjectorComponent.InjectorToggleMode.Inject;
        }
    }

    private void TryDraw(Entity<InjectorComponent> injector, Entity<BloodstreamComponent?> target, Entity<SolutionComponent> targetSolution, EntityUid user)
    {
        if (!_solutionContainers.TryGetSolution(injector.Owner, InjectorComponent.SolutionName, out var soln, out var solution) || solution.AvailableVolume == 0)
        {
            return;
        }

        // Get transfer amount. May be smaller than _transferAmount if not enough room, also make sure there's room in the injector
        var realTransferAmount = FixedPoint2.Min(injector.Comp.TransferAmount, targetSolution.Comp.Solution.Volume, solution.AvailableVolume);

        if (realTransferAmount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("injector-component-target-is-empty-message", ("target", Identity.Entity(target, EntityManager))),
                injector.Owner, user);
            return;
        }

        // We have some snowflaked behavior for streams.
        if (target.Comp != null)
        {
            DrawFromBlood(injector, (target.Owner, target.Comp), soln, realTransferAmount, user);
            return;
        }

        // Move units from attackSolution to targetSolution
        var removedSolution = _solutionContainers.Draw(target.Owner, targetSolution, realTransferAmount);

        if (!_solutions.TryAddSolution(soln, removedSolution))
        {
            return;
        }

        _popup.PopupEntity(Loc.GetString("injector-component-draw-success-message",
                ("amount", removedSolution.Volume),
                ("target", Identity.Entity(target, EntityManager))), injector.Owner, user);

        Dirty(injector);
        AfterDraw(injector);
    }

    private void DrawFromBlood(Entity<InjectorComponent> injector, Entity<BloodstreamComponent> target, Entity<SolutionComponent> injectorSolution, FixedPoint2 transferAmount, EntityUid user)
    {
        var drawAmount = (float) transferAmount;

        if (_solutionContainers.TryGetSolution(target.Owner, target.Comp.ChemicalSolutionName, out var chemSolution, out _))
        {
            var chemTemp = _solutions.SplitSolution(chemSolution, drawAmount * 0.15f);
            _solutions.TryAddSolution(injectorSolution, chemTemp);
            drawAmount -= (float) chemTemp.Volume;
        }

        if (_solutionContainers.TryGetSolution(target.Owner, target.Comp.BloodSolutionName, out var bloodSolution, out _))
        {
            var bloodTemp = _solutions.SplitSolution(bloodSolution, drawAmount);
            _solutions.TryAddSolution(injectorSolution, bloodTemp);
        }

        _popup.PopupEntity(Loc.GetString("injector-component-draw-success-message",
                ("amount", transferAmount),
                ("target", Identity.Entity(target, EntityManager))), injector.Owner, user);

        Dirty(injector);
        AfterDraw(injector);
    }

}
