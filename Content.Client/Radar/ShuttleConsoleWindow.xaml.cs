using Content.Client.Computer;
using Content.Client.UserInterface;
using Content.Shared.Radar;
using Content.Shared.Shuttles.Components;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Utility;

namespace Content.Client.Radar;

[GenerateTypedNameReferences]
public sealed partial class ShuttleConsoleWindow : FancyWindow, IComputerWindow<RadarConsoleBoundInterfaceState>
{
    public ShuttleConsoleWindow()
    {
        RobustXamlLoader.Load(this);
    }

    public void SetupComputerWindow(ComputerBoundUserInterfaceBase cb)
    {

    }

    public void UpdateState(RadarConsoleBoundInterfaceState scc)
    {
        ShuttleConsole.UpdateState(scc);
    }

    public void SetLinearVelocity(Vector2 value)
    {
        LinearVelocity.Text = value.ToString();
    }
}


public sealed class RadarControl : Control
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private const int MinimapRadius = 256;
    private const int MinimapMargin = 4;
    private const float GridLinesDistance = 32f;

    /// <summary>
    /// Entity used to transform all of the radar objects.
    /// </summary>
    private EntityUid? _entity;

    private float _radarRange = 256f;

    private int SizeFull => (int) ((MinimapRadius + MinimapMargin) * 2 * UIScale);
    private int ScaledMinimapRadius => (int) (MinimapRadius * UIScale);
    private float MinimapScale => _radarRange != 0 ? ScaledMinimapRadius / _radarRange : 0f;

    private Dictionary<EntityUid, Label> _labels = new();

    public RadarControl()
    {
        IoCManager.InjectDependencies(this);
        MinSize = (SizeFull, SizeFull);
    }

    public void UpdateState(RadarConsoleBoundInterfaceState ls)
    {
        _radarRange = ls.Range;
        _entity = ls.Entity;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        // TODO: Just draw shuttles in range on fixture normals.
        var point = SizeFull / 2;
        var fakeAA = new Color(0.08f, 0.08f, 0.08f);

        handle.DrawCircle((point, point), ScaledMinimapRadius + 1, fakeAA);
        handle.DrawCircle((point, point), ScaledMinimapRadius, Color.Black);

        // No data
        if (_entity == null)
        {
            foreach (var (_, label) in _labels)
            {
                label.Dispose();
            }

            _labels.Clear();
            return;
        }


        var gridLines = new Color(0.08f, 0.08f, 0.08f);
        var gridLinesRadial = 8;
        var gridLinesEquatorial = (int) Math.Floor(_radarRange / GridLinesDistance);

        for (var i = 1; i < gridLinesEquatorial + 1; i++)
        {
            handle.DrawCircle((point, point), GridLinesDistance * MinimapScale * i, gridLines, false);
        }

        for (var i = 0; i < gridLinesRadial; i++)
        {
            Angle angle = (Math.PI / gridLinesRadial) * i;
            var aExtent = angle.ToVec() * ScaledMinimapRadius;
            handle.DrawLine((point, point) - aExtent, (point, point) + aExtent, gridLines);
        }

        var xform = _entManager.GetComponent<TransformComponent>(_entity.Value);
        var mapPosition = xform.MapPosition;
        var matrix = xform.InvWorldMatrix;

        // Draw our grid in detail
        var ourGridId = xform.GridID;
        var ourGridFixtures = _entManager.GetComponent<FixturesComponent>(ourGridId);
        var ourGridBody = _entManager.GetComponent<PhysicsComponent>(ourGridId);

        // Can also use ourGridBody.LocalCenter
        var offset = xform.Coordinates.Position;

        var invertedPosition = xform.Coordinates.Position - offset;
        invertedPosition.Y = -invertedPosition.Y;
        var offsetMatrix = Matrix3.CreateTranslation(-offset);

        // Draw our grid; use non-filled boxes so it doesn't look awful.
        DrawGrid(handle, offsetMatrix, ourGridFixtures, point, Color.Yellow);

        // Don't need to transform the InvWorldMatrix again as it's already offset to its position.

        // Draw docks
        // TODO:

        // Draw radar position on the station
        handle.DrawCircle(invertedPosition * MinimapScale + point, 5f, Color.Lime);

        // Draw other grids... differently
        foreach (var grid in _mapManager.FindGridsIntersecting(mapPosition.MapId,
                     new Box2(mapPosition.Position - _radarRange, mapPosition.Position + _radarRange)))
        {
            if (grid.Index == ourGridId) continue;

            var gridBody = _entManager.GetComponent<PhysicsComponent>(grid.GridEntityId);
            if (gridBody.Mass < 10f)
            {
                ClearLabel(grid.GridEntityId);
                continue;
            }

            var gridXform = _entManager.GetComponent<TransformComponent>(grid.GridEntityId);
            var gridFixtures = _entManager.GetComponent<FixturesComponent>(grid.GridEntityId);
            var gridMatrix = gridXform.WorldMatrix;
            Matrix3.Multiply(ref gridMatrix, ref matrix, out var matty);

            if (!_labels.TryGetValue(grid.GridEntityId, out var label))
            {
                label = new Label();
                _labels[grid.GridEntityId] = label;
                AddChild(label);
            }

            var gridCentre = matty.Transform(gridBody.LocalCenter);
            gridCentre.Y = -gridCentre.Y;
            label.Text = $"Dork ({gridCentre.Length:0.0} m)";
            label.Visible = true;
            handle.DrawCircle(gridCentre * MinimapScale + point, 2f, Color.Red);
            // LayoutContainer.SetPosition(label, gridCentre * MinimapScale + point);

            // Detailed view
            DrawGrid(handle, matty, gridFixtures, point, Color.Aquamarine);
        }
    }

    private void ClearLabel(EntityUid uid)
    {
        if (!_labels.TryGetValue(uid, out var label)) return;
        label.Dispose();
        _labels.Remove(uid);
    }

    private void DrawGrid(DrawingHandleScreen handle, Matrix3 matrix, FixturesComponent component, int point, Color color)
    {
        foreach (var (_, fixture) in component.Fixtures)
        {
            // If the fixture has any points out of range we won't draw any of it.
            var invalid = false;
            var poly = (PolygonShape) fixture.Shape;
            var verts = new Vector2[poly.VertexCount + 1];

            for (var i = 0; i < poly.VertexCount; i++)
            {
                var vert = matrix.Transform(poly.Vertices[i]);

                if (vert.Length > _radarRange)
                {
                    invalid = true;
                    break;
                }

                vert.Y = -vert.Y;
                verts[i] = vert * MinimapScale + point;
            }

            if (invalid) continue;

            // Closed list
            verts[poly.VertexCount] = verts[0];
            handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, verts, color);
        }
    }

    private void DrawObscuredLine(DrawingHandleScreen handle, Vector2 start, Vector2 end)
    {
        handle.DrawCircle(start + (end - start) / 2f, 1f, Color.Aqua, false);

        // handle.DrawLine(start, end, Color.Aqua);
    }
}

[UsedImplicitly]
public sealed class RadarConsoleBoundUserInterface : ComputerBoundUserInterface<ShuttleConsoleWindow, RadarConsoleBoundInterfaceState>
{
    public RadarConsoleBoundUserInterface(ClientUserInterfaceComponent owner, object uiKey) : base(owner, uiKey) {}
}
