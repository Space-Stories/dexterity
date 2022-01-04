using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Content.Shared.MobState.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

public class SpawnArtifactSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpawnArtifactComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SpawnArtifactComponent, ArtifactActivatedEvent>(OnActivate);
    }
    private void OnInit(EntityUid uid, SpawnArtifactComponent component, ComponentInit args)
    {
        ChooseRandomPrototype(uid, component);
    }

    private void OnActivate(EntityUid uid, SpawnArtifactComponent component, ArtifactActivatedEvent args)
    {
        if (component.Prototype == null)
            return;
        if (component.SpawnsCount >= component.MaxSpawns)
            return;

        // select spawn position near artifact
        var artifactCord = Transform(uid).Coordinates;
        var dx = _random.NextFloat(-component.Range, component.Range);
        var dy = _random.NextFloat(-component.Range, component.Range);
        var spawnCord = artifactCord.Offset(new Vector2(dx, dy));

        // spawn entity
        EntityManager.SpawnEntity(component.Prototype, spawnCord);
        component.SpawnsCount++;
    }

    private void ChooseRandomPrototype(EntityUid uid, SpawnArtifactComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!component.RandomPrototype)
            return;
        if (component.PossiblePrototypes.Length == 0)
            return;

        var proto = _random.Pick(component.PossiblePrototypes);
        component.Prototype = proto;
    }
}
