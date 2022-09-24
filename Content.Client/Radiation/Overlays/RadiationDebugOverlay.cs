using System.Linq;
using Content.Shared.Radiation.Systems;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client.Radiation.Overlays;

public sealed class RadiationDebugOverlay : Overlay
{
    [Dependency] private readonly IMapManager _mapManager = default!;

    private readonly Font _font;

    public List<RadiationRay>? Rays;
    public Dictionary<EntityUid, Dictionary<Vector2i, float>>? ResistanceGrids;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public RadiationDebugOverlay()
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
                DrawScreenRays(args);
                DrawScreenResistance(args);
                break;
            case OverlaySpace.WorldSpace:
                DrawWorld(args);
                break;
        }
    }

    private void DrawScreenRays(OverlayDrawArgs args)
    {
        if (Rays == null || args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        foreach (var ray in Rays)
        {
            if (ray.MapId != args.MapId)
                continue;

            if (ray.ReachedDestination)
            {
                var screenCenter = args.ViewportControl.WorldToScreen(ray.Destination);
                handle.DrawString(_font, screenCenter, ray.Rads.ToString("F2"), 2f, Color.White);
            }

            foreach (var (gridUid, blockers) in ray.Blockers)
            {
                if (!_mapManager.TryGetGrid(gridUid, out var grid))
                    continue;

                foreach (var (tile, rads) in blockers)
                {
                    var worldPos = grid.GridTileToWorldPos(tile);
                    var screenCenter = args.ViewportControl.WorldToScreen(worldPos);
                    handle.DrawString(_font, screenCenter, rads.ToString("F2"), 1.5f, Color.White);
                }
            }
        }
    }

    private void DrawScreenResistance(OverlayDrawArgs args)
    {
        if (ResistanceGrids == null || args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        foreach (var (gridUid, resMap) in ResistanceGrids)
        {
            if (!_mapManager.TryGetGrid(gridUid, out var grid))
                continue;
            if (grid.ParentMapId != args.MapId)
                continue;

            var offset = new Vector2(grid.TileSize, -grid.TileSize) * 0.25f;
            foreach (var (tile, value) in resMap)
            {
                var localPos = grid.GridTileToLocal(tile).Position + offset;
                var worldPos = grid.LocalToWorld(localPos);
                var screenCenter = args.ViewportControl.WorldToScreen(worldPos);
                handle.DrawString(_font, screenCenter, value.ToString("F2"), color: Color.White);
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

            if (ray.ReachedDestination)
            {
                handle.DrawLine(ray.Source, ray.Destination, Color.Red);
                continue;
            }

            foreach (var (gridUid, blockers) in ray.Blockers)
            {
                if (!_mapManager.TryGetGrid(gridUid, out var grid))
                    continue;
                var (destTile, _) = blockers.Last();
                var destWorld = grid.GridTileToWorldPos(destTile);
                handle.DrawLine(ray.Source, destWorld, Color.Red);
            }
        }
    }
}
