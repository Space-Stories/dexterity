using Content.Shared.Lube;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Lube;

public sealed class LubedSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LubedComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LubedComponent, ContainerGettingInsertedAttemptEvent>(OnHandPickUp);
    }

    private void OnInit(EntityUid uid, LubedComponent component, ComponentInit args)
    {
        var meta = MetaData(uid);
        var name = meta.EntityName;
        component.BeforeLubedEntityName = meta.EntityName;
        _metaData.SetEntityName(uid, Loc.GetString("lubed-name-prefix", ("target", name)));
    }

    private void OnHandPickUp(EntityUid uid, LubedComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        if (component.SlipsLeft <= 0)
        {
            RemComp<LubedComponent>(uid);
            _metaData.SetEntityName(uid, component.BeforeLubedEntityName);
            return;
        }
        component.SlipsLeft--;
        args.Cancel();
        _transform.SetCoordinates(uid, Transform(args.Container.Owner).Coordinates);
        _throwing.TryThrow(uid, _random.NextVector2(), strength: component.SlipStrength);
    }
}
