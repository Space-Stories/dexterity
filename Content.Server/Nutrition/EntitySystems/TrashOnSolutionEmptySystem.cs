using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Nutrition.Components;
using Content.Shared.Chemistry.Containers.Components;
using Content.Shared.Chemistry.Containers.Events;
using Content.Shared.Chemistry.Solutions;
using Content.Shared.Tag;

namespace Content.Server.Nutrition.EntitySystems
{
    public sealed class TrashOnSolutionEmptySystem : EntitySystem
    {
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TrashOnSolutionEmptyComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<TrashOnSolutionEmptyComponent, SolutionChangedEvent>(OnSolutionChange);
        }

        public void OnStartup(EntityUid uid, TrashOnSolutionEmptyComponent component, ComponentStartup args)
        {
            CheckSolutions(component);
        }

        public void OnSolutionChange(EntityUid uid, TrashOnSolutionEmptyComponent component, SolutionChangedEvent args)
        {
            CheckSolutions(component);
        }

        public void CheckSolutions(TrashOnSolutionEmptyComponent component)
        {
            if (!EntityManager.HasComponent<SolutionContainerComponent>((component).Owner))
                return;

            if (_solutionContainerSystem.TryGetSolution(component.Owner, component.Solution, out _, out var solution))
                UpdateTags(component, solution);
        }

        public void UpdateTags(TrashOnSolutionEmptyComponent component, Solution solution)
        {
            if (solution.Volume <= 0)
            {
                _tagSystem.AddTag(component.Owner, "Trash");
                return;
            }
            if (_tagSystem.HasTag(component.Owner, "Trash"))
                _tagSystem.RemoveTag(component.Owner, "Trash");
        }
    }
}
