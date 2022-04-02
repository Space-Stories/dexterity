using Robust.Client.GameObjects;

using static Content.Shared.Paper.SharedPaperComponent;

namespace Content.Client.Paper;

public sealed class PaperSystem : VisualizerSystem<PaperVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, PaperVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        if (args.Component.TryGetData(PaperVisuals.Status , out PaperStatus writingStatus))
            sprite.LayerSetVisible(PaperVisualLayers.Writing, writingStatus == PaperStatus.Written);

        if (args.Component.TryGetData(PaperVisuals.Stamped, out bool isStamped))
            sprite.LayerSetVisible(PaperVisualLayers.Stamp, isStamped);
    }
}

public enum PaperVisualLayers
{
    Stamp,
    Writing
}
