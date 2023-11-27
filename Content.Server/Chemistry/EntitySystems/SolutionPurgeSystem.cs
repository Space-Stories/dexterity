using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Chemistry.Containers.Components;
using Content.Shared.Chemistry.Solutions.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server.Chemistry.EntitySystems;

public sealed class SolutionPurgeSystem : EntitySystem
{
    [Dependency] private readonly SolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SolutionSystem _solution = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionPurgeComponent, EntityUnpausedEvent>(OnUnpaused);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SolutionPurgeComponent, SolutionContainerComponent>();
        while (query.MoveNext(out var uid, out var purge, out var manager))
        {
            if (_timing.CurTime < purge.NextPurgeTime)
                continue;

            // timer ignores if it's empty, it's just a fixed cycle
            purge.NextPurgeTime += purge.Duration;
            if (_solutionContainer.TryGetSolution((uid, manager), purge.Solution, out var solution, out _))
                _solution.SplitSolutionWithout(solution, purge.Quantity, purge.Preserve.ToArray());
        }
    }

    private void OnUnpaused(EntityUid uid, SolutionPurgeComponent comp, ref EntityUnpausedEvent args)
    {
        comp.NextPurgeTime += args.PausedTime;
    }
}
