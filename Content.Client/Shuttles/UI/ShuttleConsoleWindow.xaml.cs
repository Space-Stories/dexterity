using Content.Client.Computer;
using Content.Client.Shuttles.Systems;
using Content.Client.UserInterface;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Shuttles.UI;

[GenerateTypedNameReferences]
public sealed partial class ShuttleConsoleWindow : FancyWindow,
    IComputerWindow<ShuttleConsoleBoundInterfaceState>
{
    private readonly ShuttleConsoleSystem _system;
    private readonly IEntityManager _entManager;

    /// <summary>
    /// EntityUid of the open console.
    /// </summary>
    private EntityUid? _entity;

    public ShuttleConsoleWindow()
    {
        RobustXamlLoader.Load(this);
        _entManager = IoCManager.Resolve<IEntityManager>();
        _system = _entManager.EntitySysManager.GetEntitySystem<ShuttleConsoleSystem>();
        IFFToggle.OnPressed += OnIFFTogglePressed;
        IFFToggle.Pressed = RadarScreen.ShowIFF;

        ShuttleMode.OnPressed += OnShuttleModePressed;
    }

    private void OnShuttleModePressed(BaseButton.ButtonEventArgs obj)
    {
        ShuttleMode.Pressed ^= true;
        _system.SendShuttleMode(ShuttleMode.Pressed ? Shared.Shuttles.Components.ShuttleMode.Strafing : Shared.Shuttles.Components.ShuttleMode.Cruise);
    }

    private void OnIFFTogglePressed(BaseButton.ButtonEventArgs args)
    {
        RadarScreen.ShowIFF ^= true;
        args.Button.Pressed = RadarScreen.ShowIFF;
    }

    public void UpdateState(ShuttleConsoleBoundInterfaceState scc)
    {
        _entity = scc.Entity;
        RadarScreen.UpdateState(scc);
        RadarRange.Text = $"{scc.Range:0}";
        ShuttleMode.Pressed = scc.Mode == Shared.Shuttles.Components.ShuttleMode.Strafing;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (!_entManager.TryGetComponent<TransformComponent>(_entity, out var entXform) ||
            !_entManager.TryGetComponent<PhysicsComponent>(entXform.GridEntityId, out var gridBody) ||
            !_entManager.TryGetComponent<TransformComponent>(entXform.GridEntityId, out var gridXform))
        {
            return;
        }

        var (_, worldRot, worldMatrix) = gridXform.GetWorldPositionRotationMatrix();
        var worldPos = worldMatrix.Transform(gridBody.LocalCenter);

        // Get the positive reduced angle.
        var displayRot = -worldRot.Reduced();

        GridPosition.Text = $"{worldPos.X:0.0}, {worldPos.Y:0.0}";
        GridOrientation.Text = $"{displayRot.Degrees:0.0}";

        var gridVelocity = gridBody.LinearVelocity;
        gridVelocity = displayRot.RotateVec(gridVelocity);
        // Get linear velocity relative to the console entity
        GridLinearVelocity.Text = $"{gridVelocity.X:0.0}, {gridVelocity.Y:0.0}";
        GridAngularVelocity.Text = $"{gridBody.AngularVelocity:0.0}";
    }
}
