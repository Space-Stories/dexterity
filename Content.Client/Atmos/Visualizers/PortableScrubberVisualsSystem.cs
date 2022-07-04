using Robust.Client.GameObjects;
using Content.Shared.Atmos.Visuals;
using Content.Client.Power;

namespace Content.Client.Atmos.Visualizers
{
    /// <summary>
    /// Controls client-side visuals for the
    /// disease machines.
    /// </summary>
    public sealed class PortableScrubberSystem : VisualizerSystem<PortableScrubberVisualsComponent>
    {
        protected override void OnAppearanceChange(EntityUid uid, PortableScrubberVisualsComponent component, ref AppearanceChangeEvent args)
        {
            if (args.Sprite == null)
                return;

            if (args.Component.TryGetData(PortableScrubberVisuals.IsFull, out bool isFull)
                && args.Component.TryGetData(PortableScrubberVisuals.IsRunning, out bool isRunning))
            {
                var runningState = isRunning ? component.RunningState : component.IdleState;
                args.Sprite.LayerSetState(PortableScrubberVisualLayers.IsRunning, runningState);

                var fullState = isFull ? component.FullState : component.ReadyState;
                args.Sprite.LayerSetState(PowerDeviceVisualLayers.Powered, fullState);
            }
        }
    }
}
public enum PortableScrubberVisualLayers : byte
{
    IsRunning
}
