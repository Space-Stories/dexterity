using Content.Server.Body.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Body.Organ;
using Content.Shared.Chemistry.Containers.Components;
using Content.Shared.Chemistry.Solutions;
using Content.Shared.Chemistry.Solutions.EntitySystems;
using Robust.Shared.Utility;

namespace Content.Server.Body.Systems
{
    public sealed class StomachSystem : EntitySystem
    {
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly SolutionSystem _solutionSystem = default!;

        public const string DefaultSolutionName = "stomach";

        public override void Initialize()
        {
            SubscribeLocalEvent<StomachComponent, ApplyMetabolicMultiplierEvent>(OnApplyMetabolicMultiplier);
        }

        public override void Update(float frameTime)
        {
            var query = EntityQueryEnumerator<StomachComponent, OrganComponent, SolutionContainerComponent>();
            while (query.MoveNext(out var uid, out var stomach, out var organ, out var sol))
            {
                stomach.AccumulatedFrameTime += frameTime;

                if (stomach.AccumulatedFrameTime < stomach.UpdateInterval)
                    continue;

                stomach.AccumulatedFrameTime -= stomach.UpdateInterval;

                // Get our solutions
                if (!_solutionContainerSystem.TryGetSolution((uid, sol), DefaultSolutionName, out var stomachSoln, out var stomachSolution))
                    continue;

                if (organ.Body is not { } body || !_solutionContainerSystem.TryGetSolution(body, stomach.BodySolutionName, out var bodySolution, out _))
                    continue;

                var transferSolution = new Solution();

                var queue = new RemQueue<StomachComponent.ReagentDelta>();
                foreach (var delta in stomach.ReagentDeltas)
                {
                    delta.Increment(stomach.UpdateInterval);
                    if (delta.Lifetime > stomach.DigestionDelay)
                    {
                        if (stomachSolution.TryGetReagent(delta.ReagentQuantity.Reagent, out var reagent))
                        {
                            if (reagent.Quantity > delta.ReagentQuantity.Quantity)
                                reagent = new(reagent.Reagent, delta.ReagentQuantity.Quantity);

                            stomachSolution.RemoveReagent(reagent);
                            transferSolution.AddReagent(reagent);
                        }

                        queue.Add(delta);
                    }
                }

                foreach (var item in queue)
                {
                    stomach.ReagentDeltas.Remove(item);
                }

                _solutionSystem.UpdateChemicals(stomachSoln);

                // Transfer everything to the body solution!
                _solutionSystem.TryAddSolution(bodySolution, transferSolution);
            }
        }

        private void OnApplyMetabolicMultiplier(EntityUid uid, StomachComponent component,
            ApplyMetabolicMultiplierEvent args)
        {
            if (args.Apply)
            {
                component.UpdateInterval *= args.Multiplier;
                return;
            }

            // This way we don't have to worry about it breaking if the stasis bed component is destroyed
            component.UpdateInterval /= args.Multiplier;
            // Reset the accumulator properly
            if (component.AccumulatedFrameTime >= component.UpdateInterval)
                component.AccumulatedFrameTime = component.UpdateInterval;
        }

        public bool CanTransferSolution(EntityUid uid, Solution solution,
            SolutionContainerComponent? solutions = null)
        {
            if (!Resolve(uid, ref solutions, false))
                return false;

            if (!_solutionContainerSystem.TryGetSolution((uid, solutions), DefaultSolutionName, out _, out var stomachSolution))
                return false;

            // TODO: For now no partial transfers. Potentially change by design
            if (!stomachSolution.CanAddSolution(solution))
                return false;

            return true;
        }

        public bool TryTransferSolution(EntityUid uid, Solution solution,
            StomachComponent? stomach = null,
            SolutionContainerComponent? solutions = null)
        {
            if (!Resolve(uid, ref stomach, ref solutions, false))
                return false;

            if (!_solutionContainerSystem.TryGetSolution((uid, solutions), DefaultSolutionName, out var stomachSolution, out _)
                || !CanTransferSolution(uid, solution, solutions))
                return false;

            _solutionSystem.TryAddSolution(stomachSolution, solution);
            // Add each reagent to ReagentDeltas. Used to track how long each reagent has been in the stomach
            foreach (var reagent in solution.Contents)
            {
                stomach.ReagentDeltas.Add(new StomachComponent.ReagentDelta(reagent));
            }

            return true;
        }
    }
}
