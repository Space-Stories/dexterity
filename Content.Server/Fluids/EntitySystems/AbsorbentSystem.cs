using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Fluids.EntitySystems;

/// <inheritdoc/>
public sealed class AbsorbentSystem : SharedAbsorbentSystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popups = default!;
    [Dependency] private readonly PuddleSystem _puddleSystem = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly SolutionSystem _solutionSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AbsorbentComponent, ComponentInit>(OnAbsorbentInit);
        SubscribeLocalEvent<AbsorbentComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<AbsorbentComponent, InteractNoHandEvent>(OnInteractNoHand);
        SubscribeLocalEvent<AbsorbentComponent, SolutionContainerChangedEvent>(OnAbsorbentSolutionChange);
    }

    private void OnAbsorbentInit(EntityUid uid, AbsorbentComponent component, ComponentInit args)
    {
        // TODO: I know dirty on init but no prediction moment.
        UpdateAbsorbent(uid, component);
    }

    private void OnAbsorbentSolutionChange(EntityUid uid, AbsorbentComponent component, SolutionContainerChangedEvent args)
    {
        UpdateAbsorbent(uid, component);
    }

    private void UpdateAbsorbent(EntityUid uid, AbsorbentComponent component)
    {
        if (!_solutionContainerSystem.TryGetSolution(uid, AbsorbentComponent.SolutionName, out _, out var solution))
            return;

        var oldProgress = component.Progress.ShallowClone();
        component.Progress.Clear();

        var water = solution.GetTotalPrototypeQuantity(PuddleSystem.EvaporationReagents);
        if (water > FixedPoint2.Zero)
        {
            component.Progress[solution.GetColorWithOnly(_prototype, PuddleSystem.EvaporationReagents)] = water.Float();
        }

        var otherColor = solution.GetColorWithout(_prototype, PuddleSystem.EvaporationReagents);
        var other = (solution.Volume - water).Float();

        if (other > 0f)
        {
            component.Progress[otherColor] = other;
        }

        var remainder = solution.AvailableVolume;

        if (remainder > FixedPoint2.Zero)
        {
            component.Progress[Color.DarkGray] = remainder.Float();
        }

        if (component.Progress.Equals(oldProgress))
            return;

        Dirty(uid, component);
    }

    private void OnInteractNoHand(EntityUid uid, AbsorbentComponent component, InteractNoHandEvent args)
    {
        if (args.Handled || args.Target == null)
            return;

        Mop(uid, args.Target.Value, uid, component);
        args.Handled = true;
    }

    private void OnAfterInteract(EntityUid uid, AbsorbentComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Handled || args.Target == null)
            return;

        Mop(args.User, args.Target.Value, args.Used, component);
        args.Handled = true;
    }

    private void Mop(EntityUid user, EntityUid target, EntityUid used, AbsorbentComponent component)
    {
        if (!_solutionContainerSystem.TryGetSolution(used, AbsorbentComponent.SolutionName, out var absorberSoln, out _))
            return;

        if (_useDelay.ActiveDelay(used))
            return;

        // If it's a puddle try to grab from
        if (!TryPuddleInteract(user, used, target, component, absorberSoln))
        {
            // Do a transfer, try to get water onto us and transfer anything else to them.

            // If it's anything else transfer to
            if (!TryTransferAbsorber(user, used, target, component, absorberSoln))
                return;
        }
    }

    /// <summary>
    ///     Attempt to fill an absorber from some refillable solution.
    /// </summary>
    private bool TryTransferAbsorber(EntityUid user, EntityUid used, EntityUid target, AbsorbentComponent component, Entity<SolutionComponent> absorberSoln)
    {
        if (!TryComp(target, out RefillableSolutionComponent? refillable))
            return false;

        if (!_solutionContainerSystem.TryGetRefillableSolution((target, refillable, null), out var refillableSoln, out var refillableSolution))
            return false;

        if (refillableSolution.Volume <= 0)
        {
            var msg = Loc.GetString("mopping-system-target-container-empty", ("target", target));
            _popups.PopupEntity(msg, user, user);
            return false;
        }

        // Remove the non-water reagents.
        // Remove water on target
        // Then do the transfer.
        var nonWater = _solutionSystem.SplitSolutionWithout(absorberSoln, component.PickupAmount, PuddleSystem.EvaporationReagents);
        var absorberSolution = absorberSoln.Comp.Solution;

        if (nonWater.Volume == FixedPoint2.Zero && absorberSolution.AvailableVolume == FixedPoint2.Zero)
        {
            _popups.PopupEntity(Loc.GetString("mopping-system-puddle-space", ("used", used)), user, user);
            return false;
        }

        var transferAmount = component.PickupAmount < absorberSolution.AvailableVolume ?
            component.PickupAmount :
            absorberSolution.AvailableVolume;

        var water = refillableSolution.SplitSolutionWithOnly(transferAmount, PuddleSystem.EvaporationReagents);
        _solutionSystem.UpdateChemicals(refillableSoln);

        if (water.Volume == FixedPoint2.Zero && nonWater.Volume == FixedPoint2.Zero)
        {
            _popups.PopupEntity(Loc.GetString("mopping-system-target-container-empty-water", ("target", target)), user, user);
            return false;
        }

        if (water.Volume > 0 && !_solutionSystem.TryAddSolution(absorberSoln, water))
        {
            _popups.PopupEntity(Loc.GetString("mopping-system-full", ("used", used)), used, user);
        }

        // Attempt to transfer the full nonWater solution to the bucket.
        if (nonWater.Volume > 0)
        {
            bool fullTransferSuccess = _solutionSystem.TryAddSolution(refillableSoln, nonWater);

            // If full transfer was unsuccessful, try a partial transfer.
            if (!fullTransferSuccess)
            {
                var partiallyTransferSolution = nonWater.SplitSolution(refillableSolution.AvailableVolume);

                // Try to transfer the split solution to the bucket.
                if (_solutionSystem.TryAddSolution(refillableSoln, partiallyTransferSolution))
                {
                    // The transfer was successful. nonWater now contains the amount that wasn't transferred.
                    // If there's any leftover nonWater solution, add it back to the mop.
                    if (nonWater.Volume > 0)
                    {
                        _solutionSystem.AddSolution(absorberSoln, nonWater);
                    }
                }
                else
                {
                    // If the transfer was unsuccessful, combine both solutions and return them to the mop.
                    nonWater.AddSolution(partiallyTransferSolution, _prototype);
                    _solutionSystem.AddSolution(absorberSoln, nonWater);
                }
            }
        }
        else
        {
            _popups.PopupEntity(Loc.GetString("mopping-system-full", ("used", target)), user, user);
        }

        _audio.PlayPvs(component.TransferSound, target);
        _useDelay.BeginDelay(used);
        return true;
    }

    /// <summary>
    ///     Logic for an absorbing entity interacting with a puddle.
    /// </summary>
    private bool TryPuddleInteract(EntityUid user, EntityUid used, EntityUid target, AbsorbentComponent absorber, Entity<SolutionComponent> absorberSoln)
    {
        if (!TryComp(target, out PuddleComponent? puddle))
            return false;

        if (!_solutionContainerSystem.TryGetSolution(target, puddle.SolutionName, out var puddleSoln, out var puddleSolution) || puddleSolution.Volume <= 0)
            return false;

        // Check if the puddle has any non-evaporative reagents
        if (_puddleSystem.CanFullyEvaporate(puddleSolution))
        {
            _popups.PopupEntity(Loc.GetString("mopping-system-puddle-evaporate", ("target", target)), user, user);
            return true;
        }

        // Check if we have any evaporative reagents on our absorber to transfer
        var absorberSolution = absorberSoln.Comp.Solution;
        var available = absorberSolution.GetTotalPrototypeQuantity(PuddleSystem.EvaporationReagents);

        // No material
        if (available == FixedPoint2.Zero)
        {
            _popups.PopupEntity(Loc.GetString("mopping-system-no-water", ("used", used)), user, user);
            return true;
        }

        var transferMax = absorber.PickupAmount;
        var transferAmount = available > transferMax ? transferMax : available;

        var puddleSplit = puddleSolution.SplitSolutionWithout(transferAmount, PuddleSystem.EvaporationReagents);
        var absorberSplit = absorberSolution.SplitSolutionWithOnly(puddleSplit.Volume, PuddleSystem.EvaporationReagents);

        // Do tile reactions first
        var coordinates = Transform(target).Coordinates;
        if (_mapManager.TryGetGrid(coordinates.GetGridUid(EntityManager), out var mapGrid))
        {
            _puddleSystem.DoTileReactions(mapGrid.GetTileRef(coordinates), absorberSplit);
        }

        _solutionSystem.AddSolution(puddleSoln, absorberSplit);
        _solutionSystem.AddSolution(absorberSoln, puddleSplit);

        _audio.PlayPvs(absorber.PickupSound, target);
        _useDelay.BeginDelay(used);

        var userXform = Transform(user);
        var targetPos = _transform.GetWorldPosition(target);
        var localPos = _transform.GetInvWorldMatrix(userXform).Transform(targetPos);
        localPos = userXform.LocalRotation.RotateVec(localPos);

        _melee.DoLunge(user, used, Angle.Zero, localPos, null, false);

        return true;
    }
}
