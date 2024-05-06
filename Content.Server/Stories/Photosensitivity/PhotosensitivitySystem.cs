using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Damage;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Stories.Photosensitivity;

public sealed partial class PhotosensitivitySystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    private const float UpdateTimer = 2f;
    private float _timer;
    public override void Update(float frameTime)
    {
        _timer += frameTime;

        if (_timer < UpdateTimer)
            return;

        _timer -= UpdateTimer;

        var ents = EntityQueryEnumerator<PhotosensitivityComponent>();

        while (ents.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled)
                continue;

            var illumination = Math.Min(GetIllumination(uid), 10);

            if (illumination > 1.5)
            {
                _damageable.TryChangeDamage(uid, comp.Damage * illumination, true, false);
                _popup.PopupEntity("Свет выжигает вас!", uid, uid);
            }

            if (illumination < 1)
            {
                _damageable.TryChangeDamage(uid, comp.DarknessHealing, true, false);
            }
        }
    }

    // If it will'be used for something else than shadowlings then it should be rewritten
    // to calculate tiles lightness every time some light point appears, disappears or moving
    // due to performance issues this can cause
    public float GetIllumination(EntityUid uid)
    {
        var destTrs = Transform(uid);

        var lightPoints = _entityLookup.GetEntitiesInRange<PointLightComponent>(_transform.GetMapCoordinates(destTrs), 20f);
        var destination = _transform.GetWorldPosition(destTrs);

        var illumination = 0f;

        foreach (var lightPoint in lightPoints)
        {
            if (!lightPoint.Comp.Enabled)
                continue;

            var sourceTrs = Transform(lightPoint);
            var source = _transform.GetWorldPosition(sourceTrs);

            var box = Box2.FromTwoPoints(_transform.GetWorldPosition(sourceTrs), _transform.GetWorldPosition(destTrs));
            var grids = new List<Entity<MapGridComponent>>();
            _mapManager.FindGridsIntersecting(sourceTrs.MapID, box, ref grids, true);

            var dir = destination - source;
            var dist = dir.Length();

            if (dist > lightPoint.Comp.Radius)
                continue;

            var lightDirInterrupted = false;

            foreach (var grid in grids)
            {
                var gridTrs = Transform(grid);

                Vector2 srcLocal = sourceTrs.ParentUid == grid.Owner
                    ? sourceTrs.LocalPosition
                    : gridTrs.InvLocalMatrix.Transform(source);

                Vector2 dstLocal = destTrs.ParentUid == grid.Owner
                    ? destTrs.LocalPosition
                    : gridTrs.InvLocalMatrix.Transform(destination);

                Vector2i sourceGrid = new(
                    (int) Math.Floor(srcLocal.X / grid.Comp.TileSize),
                    (int) Math.Floor(srcLocal.Y / grid.Comp.TileSize));

                Vector2i destGrid = new(
                    (int) Math.Floor(dstLocal.X / grid.Comp.TileSize),
                    (int) Math.Floor(dstLocal.Y / grid.Comp.TileSize));

                var line = new GridLineEnumerator(sourceGrid, destGrid);

                while (line.MoveNext())
                {
                    foreach (var entity in _mapSystem.GetAnchoredEntities(grid, grid.Comp, line.Current))
                    {
                        if (TryComp<OccluderComponent>(entity, out var occluder) && occluder.Enabled)
                        {
                            lightDirInterrupted = true;
                            break;
                        }
                    }
                    if (lightDirInterrupted) break;
                }
            }

            if (lightDirInterrupted) continue;

            if (lightPoint.Comp.MaskPath is { } maskPath && maskPath.EndsWith("cone.png"))
            {
                var lightPointRotation = _transform.GetWorldRotation(lightPoint);
                var entityVector = destination - source;
                var entityAngle = entityVector.ToWorldAngle();

                if (entityAngle > lightPointRotation + 45 || entityAngle < lightPointRotation - 45)
                    continue;
            }

            illumination = Math.Max(illumination, lightPoint.Comp.Radius - lightPoint.Comp.Energy * dist);
        }

        return illumination;
    }
}
