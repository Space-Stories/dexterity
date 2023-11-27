using Content.Shared.Chemistry.Containers.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Solutions;
using Content.Shared.Chemistry.Solutions.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Content.Shared.Chemistry.Containers.EntitySystems;

public abstract partial class SharedSolutionContainerSystem
{
    public bool TryGetRefillableSolution(Entity<RefillableSolutionComponent?, SolutionContainerComponent?> entity, [MaybeNullWhen(false)] out Entity<SolutionComponent> soln, [MaybeNullWhen(false)] out Solution solution)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
        {
            (soln, solution) = (default!, null);
            return false;
        }

        return TryGetSolution((entity.Owner, entity.Comp2), entity.Comp1.Solution, out soln, out solution);
    }

    public bool TryGetDrainableSolution(Entity<DrainableSolutionComponent?, SolutionContainerComponent?> entity, [MaybeNullWhen(false)] out Entity<SolutionComponent> soln, [MaybeNullWhen(false)] out Solution solution)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
        {
            (soln, solution) = (default!, null);
            return false;
        }

        return TryGetSolution((entity.Owner, entity.Comp2), entity.Comp1.Solution, out soln, out solution);
    }

    public bool TryGetDumpableSolution(Entity<DumpableSolutionComponent?, SolutionContainerComponent?> entity, [MaybeNullWhen(false)] out Entity<SolutionComponent> soln, [MaybeNullWhen(false)] out Solution solution)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
        {
            (soln, solution) = (default!, null);
            return false;
        }

        return TryGetSolution((entity.Owner, entity.Comp2), entity.Comp1.Solution, out soln, out solution);
    }

    public bool TryGetDrawableSolution(Entity<DrawableSolutionComponent?, SolutionContainerComponent?> entity, [MaybeNullWhen(false)] out Entity<SolutionComponent> soln, [MaybeNullWhen(false)] out Solution solution)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
        {
            (soln, solution) = (default!, null);
            return false;
        }

        return TryGetSolution((entity.Owner, entity.Comp2), entity.Comp1.Solution, out soln, out solution);
    }

    public bool TryGetInjectableSolution(Entity<InjectableSolutionComponent?, SolutionContainerComponent?> entity, [MaybeNullWhen(false)] out Entity<SolutionComponent> soln, [MaybeNullWhen(false)] out Solution solution)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
        {
            (soln, solution) = (default!, null);
            return false;
        }

        return TryGetSolution((entity.Owner, entity.Comp2), entity.Comp1.Solution, out soln, out solution);
    }

    public bool TryGetFitsInDispenser(Entity<FitsInDispenserComponent?, SolutionContainerComponent?> entity, [MaybeNullWhen(false)] out Entity<SolutionComponent> soln, [MaybeNullWhen(false)] out Solution solution)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
        {
            (soln, solution) = (default!, null);
            return false;
        }

        return TryGetSolution((entity.Owner, entity.Comp2), entity.Comp1.Solution, out soln, out solution);
    }

    public bool TryGetMixableSolution(Entity<SolutionContainerComponent?> container, [NotNullWhen(true)] out Entity<SolutionComponent> solution)
    {
        var getMixableSolutionAttempt = new GetMixableSolutionAttemptEvent(container);
        RaiseLocalEvent(container, ref getMixableSolutionAttempt);
        if (getMixableSolutionAttempt.MixedSolution != null)
        {
            solution = getMixableSolutionAttempt.MixedSolution.Value;
            return true;
        }

        if (!Resolve(container, ref container.Comp, false))
        {
            solution = default!;
            return false;
        }

        var tryGetSolution = EnumerateSolutions(container).FirstOrNull(x => x.Solution.Comp.Solution.CanMix);
        if (tryGetSolution.HasValue)
        {
            solution = tryGetSolution.Value.Solution;
            return true;
        }

        solution = default!;
        return false;
    }


    public void Refill(Entity<RefillableSolutionComponent?> entity, Entity<SolutionComponent> soln, Solution refill)
    {
        if (!Resolve(entity, ref entity.Comp, logMissing: false))
            return;

        SolutionSystem.AddSolution(soln, refill);
    }

    public void Inject(Entity<InjectableSolutionComponent?> entity, Entity<SolutionComponent> soln, Solution inject)
    {
        if (!Resolve(entity, ref entity.Comp, logMissing: false))
            return;

        SolutionSystem.AddSolution(soln, inject);
    }

    public Solution Drain(Entity<DrainableSolutionComponent?> entity, Entity<SolutionComponent> soln, FixedPoint2 quantity)
    {
        if (!Resolve(entity, ref entity.Comp, logMissing: false))
            return new();

        return SolutionSystem.SplitSolution(soln, quantity);
    }

    public Solution Draw(Entity<DrawableSolutionComponent?> entity, Entity<SolutionComponent> soln, FixedPoint2 quantity)
    {
        if (!Resolve(entity, ref entity.Comp, logMissing: false))
            return new();

        return SolutionSystem.SplitSolution(soln, quantity);
    }


    public float PercentFull(EntityUid uid)
    {
        if (!TryGetDrainableSolution(uid, out _, out var solution) || solution.MaxVolume.Equals(FixedPoint2.Zero))
            return 0;

        return solution.FillFraction * 100;
    }


    public static string ToPrettyString(Solution solution)
    {
        var sb = new StringBuilder();
        if (solution.Name == null)
            sb.Append("[");
        else
            sb.Append($"{solution.Name}:[");
        var first = true;
        foreach (var (id, quantity) in solution.Contents)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                sb.Append(", ");
            }

            sb.AppendFormat("{0}: {1}u", id, quantity);
        }

        sb.Append(']');
        return sb.ToString();
    }
}
