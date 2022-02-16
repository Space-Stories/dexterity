using Content.Server.Chemistry.EntitySystems;
using Content.Server.DoAfter;
using Content.Server.Fluids.Components;
using Content.Server.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Sound;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Map;
using JetBrains.Annotations;

namespace Content.Server.Fluids.EntitySystems;

[UsedImplicitly]
public sealed class MoppingSystem : EntitySystem
{
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private SolutionContainerSystem _solutionSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AbsorbentComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<TransferCancelledEvent>(OnTransferCancelled);
        SubscribeLocalEvent<TransferCompleteEvent>(OnTransferComplete);
    }

        private void OnAfterInteract(EntityUid uid, AbsorbentComponent component, AfterInteractEvent args)
    {
        var user = args.User;
        var used = args.Used;
        var target = args.Target;

        var solutionSystem = EntitySystem.Get<SolutionContainerSystem>();
        solutionSystem.TryGetSolution(used, "absorbed", out var absorbedSolution);

        var toolAvailableVolume = FixedPoint2.New(0);
        var toolCurrentVolume = FixedPoint2.New(0);
        var transferAmount = FixedPoint2.New(0);

        if (absorbedSolution is not null)
        {
            toolAvailableVolume = absorbedSolution.AvailableVolume;
            toolCurrentVolume = absorbedSolution.CurrentVolume;
        }

        if (!args.CanReach)
        {
            return;
        }

        // For adding liquid to an empty floor tile
        if (target is null // if a tile is clicked
            && !args.Handled)
        {
            ReleaseToFloor(args.ClickLocation, component, absorbedSolution);
            args.Handled = true;
            return;
        }
        else if (target is not null)
        {
            // Handle our do_after logic
            HandleDoAfter(user, used, target.Value, component, toolCurrentVolume, toolAvailableVolume);
        }

        args.Handled = true;
        return;
    }


        private void ReleaseToFloor(EntityCoordinates clickLocation, AbsorbentComponent absorbent, Solution? absorbedSolution)
    {

        if ((_mapManager.TryGetGrid(clickLocation.GetGridId(EntityManager), out var mapGrid)) // needs valid grid
            && absorbedSolution != null) // needs a solution to place on the tile
        {
            TileRef tile = mapGrid.GetTileRef(clickLocation);

            // Drop some of the absorbed liquid onto the ground
            var releaseAmount = FixedPoint2.Min(absorbent.ResidueAmount, absorbedSolution.CurrentVolume);
            var releasedSolution = _solutionSystem.SplitSolution(absorbent.Owner, absorbedSolution, releaseAmount);
            EntitySystem.Get<SpillableSystem>().SpillAt(tile, releasedSolution, "PuddleSmear");
        }
        return;
    }

    // Handles logic for our different types of valid target.
    // Checks for conditions that would prevent a doAfter from starting.
    private void HandleDoAfter(EntityUid user, EntityUid used, EntityUid target, AbsorbentComponent component, FixedPoint2 currentVolume, FixedPoint2 availableVolume)
    {
        // Below variables will be set within this function depending on what kind of target was clicked.
        // They will be passed to the OnTransferComplete if the doAfter succeeds.

        EntityUid donor = new(0);
        EntityUid acceptor = new(0);
        var donorSolutionName = "";
        var acceptorSolutionName = "";

        var transferAmount = FixedPoint2.New(0);

        var delay = 1.0f; //default do_after delay in seconds.
        var msg = "";
        SoundSpecifier sfx = new SoundPathSpecifier("");

        // For our purposes, if our target has a PuddleComponent, treat it as a puddle above all else.
        if (TryComp<PuddleComponent>(target, out var puddle))
        {
            // These return conditions will abort BEFORE the do_after is called:
            if(!_solutionSystem.TryGetSolution(target, puddle.SolutionName, out var puddleSolution)) // puddle Solution is null
            {
                return;
            }
            else if (availableVolume < 0) // mop is completely full
            {
                msg = "mopping-system-tool-full";
                user.PopupMessage(user, Loc.GetString(msg)); // play message now because we are aborting.
                return;
            }
            else if (puddleSolution.TotalVolume <= 0) // puddle is completely empty
            {
                return;
            }
            // adding to puddles
            else if (puddleSolution.TotalVolume <= component.MopLowerLimit // if the puddle is too small for the tool to effectively absorb any more solution from it
                    && currentVolume > 0) // tool needs a solution to dilute the puddle with.
                {
                    // Dilutes the puddle with some solution from the tool
                    transferAmount = FixedPoint2.Max(component.ResidueAmount, currentVolume);
                    TryTransfer(used, target, "absorbed", puddle.SolutionName, transferAmount); // Complete the transfer right away, with no doAfter.

                    sfx = component.TransferSound;
                    SoundSystem.Play(Filter.Pvs(user), sfx.GetSound(), used); // Give instant feedback for diluting puddle, so that it's clear that the player is adding to the puddle.
                    return; // Do not begin a doAfter.
                }
            else
            {
                // Taking from puddles:

                // Determine transferAmount:
                transferAmount = FixedPoint2.Min(component.PickupAmount, puddleSolution.TotalVolume, availableVolume);

                // TODO: consider onelining this with the above, using additional args on Min()?
                if ((puddleSolution.TotalVolume - transferAmount) < component.MopLowerLimit) // If the transferAmount would bring the puddle below the MopLowerLimit
                {
                    transferAmount = puddleSolution.TotalVolume - component.MopLowerLimit; // Then the transferAmount should bring the puddle down to the MopLowerLimit exactly
                }

                donor = target; // the puddle Uid
                donorSolutionName = puddle.SolutionName;

                acceptor  = used; // the mop/tool Uid
                acceptorSolutionName = "absorbed"; // by definition on AbsorbentComponent

                // Set delay/popup/sound if nondefault. Popup and sound will only played upon successful doAfter.
                delay = (component.PickupAmount.Float() / 10.0f) * component.MopSpeed;
                msg = "mopping-system-puddle-success";
                sfx = component.PickupSound;
            }



        }
        else if (currentVolume > 0) // tool is wet
        {
            // Can we put solution from the tool into the target?
            if (TryComp<RefillableSolutionComponent>(target, out var refillable))
            {
                // These return conditions will abort BEFORE the do_after is called:
                if (!_solutionSystem.TryGetRefillableSolution(target, out var refillableSolution)) // refillable Solution is null
                {
                    return;
                }
                else if (refillableSolution.AvailableVolume <= 0) // target container is full (liquid destination)
                {
                    msg = "mopping-system-target-container-full";
                    user.PopupMessage(user, Loc.GetString(msg)); // play message now because we are aborting.
                    return;
                }
                else
                {
                    // Determine transferAmount
                    transferAmount = FixedPoint2.Min(refillableSolution.AvailableVolume, currentVolume); //Transfer all liquid from the tool to the container, but only if it will fit.

                    // TODO: Make it so that if the tool is a mop, you can't dry it out completely using just your hands?
                    // Would need to set this threshold on Drainable as well so the player can refill the mop. Currently just hardcoded at zero.

                    donor = used; // the mop/tool Uid
                    donorSolutionName = "absorbed"; // by definition on AbsorbentComponent

                    acceptor  = target; // the refillable container's Uid
                    acceptorSolutionName = refillable.Solution;

                    // Set delay/popup/sound if nondefault. Popup and sound will only played upon successful doAfter.
                    delay = 1.0f; //TODO: Make this scale with how much liquid is in the tool, as well as if the tool needs a wringer for max effect.
                    msg = "mopping-system-refillable-message";
                    sfx = component.TransferSound;
                }
            }
        }
        else if (currentVolume <= 0) // mop is dry
        {
            if (TryComp<DrainableSolutionComponent>(target, out var drainable))
            {
                // These return conditions will abort BEFORE the do_after is called:
                if (!_solutionSystem.TryGetDrainableSolution(target, out var drainableSolution))
                {
                    return;
                }
                else if (drainableSolution.CurrentVolume <= 0) // target container is empty (liquid source)
                {
                    msg = "mopping-system-target-container-empty";
                    user.PopupMessage(user, Loc.GetString(msg)); // play message now because we are returning.
                    return;
                }
                else
                {
                    // Determine transferAmount
                    transferAmount = FixedPoint2.Min(availableVolume * 0.5, drainableSolution.CurrentVolume); // Let's transfer up to to half the tool's available capacity to the tool.

                    donor = target; // the drainable container's Uid
                    donorSolutionName = drainable.Solution;

                    acceptor  = used; // the mop/tool Uid
                    acceptorSolutionName = "absorbed"; // by definition on AbsorbentComponent

                    // set delay/popup/sound if nondefault
                    // default delay is fine
                    msg = "mopping-system-drainable-message";
                    sfx = component.TransferSound;
                }
            }
        }
        else return;

        var doAfterArgs = new DoAfterEventArgs(user, delay, target: target)
        {
            BreakOnUserMove = true,
            BreakOnStun = true,
            BreakOnDamage = true,
            MovementThreshold = 0.2f,
            BroadcastCancelledEvent = new TransferCancelledEvent()
            {
                Target = target,
                Component = component // (the AbsorbentComponent)
            },
            BroadcastFinishedEvent = new TransferCompleteEvent()
            {
                User = user,
                Tool = used,
                Target = target,
                Donor = donor,
                Acceptor = acceptor,
                Component = component,
                DonorSolutionName = donorSolutionName,
                AcceptorSolutionName = acceptorSolutionName,
                Message = msg,
                Sound = sfx,
                TransferAmount = transferAmount
            }
        };

        // Can't interact with too many entities at once.
        if (component.MaxInteractingEntities < component.InteractingEntities.Count + 1)
            return;

        // Can't interact with the same container multiple times at once.
        if (!component.InteractingEntities.Add(target))
            return;

        var result = _doAfterSystem.WaitDoAfter(doAfterArgs);

    }


    private void OnTransferComplete(TransferCompleteEvent ev)
    {
        SoundSystem.Play(Filter.Pvs(ev.User), ev.Sound.GetSound(), ev.Tool); // Play the After SFX

        ev.User.PopupMessage(ev.User, Loc.GetString(ev.Message)); // Play the After popup message

        TryTransfer(ev.Donor, ev.Acceptor, ev.DonorSolutionName, ev.AcceptorSolutionName, ev.TransferAmount);

        ev.Component.InteractingEntities.Remove(ev.Target); // Tell the absorbentComponent that we have stopped interacting with the target.
        return;
    }

    private void OnTransferCancelled(TransferCancelledEvent ev)
    {
        ev.Component.InteractingEntities.Remove(ev.Target); // Tell the absorbentComponent that we have stopped interacting with the target.
        return;
    }

    private void TryTransfer(EntityUid donor, EntityUid acceptor, string donorSolutionName, string acceptorSolutionName, FixedPoint2 transferAmount)
    {
        if (_solutionSystem.TryGetSolution(donor, donorSolutionName, out var donorSolution) // If the donor solution is valid
            && _solutionSystem.TryGetSolution(acceptor, acceptorSolutionName, out var acceptorSolution)) // And the acceptor solution is valid
        {
            var solutionToTransfer = _solutionSystem.SplitSolution(donor, donorSolution, transferAmount);   // Split a portion of the donor solution
            _solutionSystem.TryAddSolution(acceptor, acceptorSolution, solutionToTransfer);                 // And add it to the acceptor solution
        }
    }
}


public sealed class TransferCompleteEvent : EntityEventArgs
{
    public EntityUid User;
    public EntityUid Tool;
    public EntityUid Target;
    public EntityUid Donor;
    public EntityUid Acceptor;
    public AbsorbentComponent Component { get; init; } = default!;
    public string DonorSolutionName = "";
    public string AcceptorSolutionName = "";
    public string Message = "";
    public SoundSpecifier Sound { get; init; } = default!;
    public FixedPoint2 TransferAmount;

}

public sealed class TransferCancelledEvent : EntityEventArgs
{
    public EntityUid Target;
    public AbsorbentComponent Component { get; init; } = default!;

}
