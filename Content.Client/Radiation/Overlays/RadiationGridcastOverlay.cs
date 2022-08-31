using System.Linq;
using Content.Shared.Radiation.Systems;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client.Radiation.Overlays;

public sealed class RadiationGridcastOverlay : Overlay
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;

    private readonly Font _font;

    public List<RadiationRay>? Rays;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public RadiationGridcastOverlay()
    {
        IoCManager.InjectDependencies(this);

        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 8);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        switch (args.Space)
        {
            case OverlaySpace.ScreenSpace:
                DrawScreen(args);
                break;
            case OverlaySpace.WorldSpace:
                DrawWorld(args);
                break;
        }
    }

    private void DrawScreen(OverlayDrawArgs args)
    {
        if (Rays == null)
            return;

        var handle = args.ScreenHandle;
        foreach (var ray in Rays)
        {
            if (ray.MapId != args.MapId)
                continue;

            if (ray.ReachedDestination)
            {
                var screenCenter = _eyeManager.WorldToScreen(ray.Destination);
                handle.DrawString(_font, screenCenter, ray.Rads.ToString("F2"), 2f, Color.White);
            }

            foreach (var (blockerPos, rads) in ray.Blockers)
            {
                var screenCenter = _eyeManager.WorldToScreen(blockerPos);
                handle.DrawString(_font, screenCenter, rads.ToString("F2"), 1.5f, Color.White);
            }

            if (_mapManager.TryGetGrid(ray.Grid, out var grid))
            {
                foreach (var (tile, rads) in ray.VisitedTiles)
                {
                    if (rads == null)
                        continue;

                    var worldPos = grid.GridTileToWorldPos(tile);
                    var screenCenter = _eyeManager.WorldToScreen(worldPos);
                    handle.DrawString(_font, screenCenter, rads.Value.ToString("F2"), 1.5f, Color.White);
                }
            }
        }
    }

    private void DrawWorld(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        if (Rays == null)
            return;

        // draw lines for raycasts
        foreach (var ray in Rays)
        {
            if (ray.MapId != args.MapId)
                continue;

            if (!ray.IsGridcast)
            {
                var lastPos = ray.Destination;
                if (!ray.ReachedDestination)
                {
                    var (lastBlocker, _) = ray.Blockers.LastOrDefault();
                    lastPos = lastBlocker;
                }

                handle.DrawLine(ray.Source, lastPos, Color.Red);
            }

        }

        // draw tiles for gridcasts
        foreach (var ray in Rays)
        {
            if (ray.Grid == null || !_mapManager.TryGetGrid(ray.Grid, out var grid))
                continue;
            var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
            var gridXform = xformQuery.GetComponent(grid.GridEntityId);
            var (_, _, worldMatrix, invWorldMatrix) = gridXform.GetWorldPositionRotationMatrixWithInv(xformQuery);
            var gridBounds = invWorldMatrix.TransformBox(args.WorldBounds);
            handle.SetTransform(worldMatrix);

            DrawTiles(handle, gridBounds, ray.VisitedTiles);
        }

        handle.SetTransform(Matrix3.Identity);
    }

    private void DrawTiles(DrawingHandleWorld handle, Box2 gridBounds,
        List<(Vector2i, float?)> tiles, ushort tileSize = 1)
    {
        var color = Color.Green;
        color.A = 0.5f;

        foreach (var (tile, _) in tiles)
        {
            var centre = ((Vector2) tile + 0.5f) * tileSize;

            // is the center of this tile visible to the user?
            if (!gridBounds.Contains(centre))
                continue;

            var box = Box2.UnitCentered.Translated(centre);
            handle.DrawRect(box, color);
        }
    }
}
