using System.Linq;
using Content.Client.Storage.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Storage.Systems;

public sealed class ItemMapperSystem : SharedItemMapperSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemMapperComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ItemMapperComponent, AppearanceChangeEvent>(OnAppearance);
    }

    private void OnStartup(EntityUid uid, ItemMapperComponent component, ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            component.RSIPath ??= sprite.BaseRSI!.Path!;
        }
    }

    private void OnAppearance(EntityUid uid, ItemMapperComponent component, ref AppearanceChangeEvent args)
    {
        if (TryComp<SpriteComponent>(component.Owner, out var spriteComponent))
        {
            if (component.SpriteLayers.Count == 0)
            {
                InitLayers(component, spriteComponent, args.Component);
                return;
            }

            EnableLayers(component, spriteComponent, args.Component);
        }
    }

    private void InitLayers(ItemMapperComponent component, SpriteComponent spriteComponent, AppearanceComponent appearance)
    {
        if (!appearance.TryGetData<ShowLayerData>(StorageMapVisuals.InitLayers, out var wrapper))
            return;

        TryComp<StorageFillComponent>(component.Owner, out var storageFill);
        component.SpriteLayers.AddRange(wrapper.QueuedEntities);

        foreach (var sprite in component.SpriteLayers)
        {
            spriteComponent.LayerMapReserveBlank(sprite);
            spriteComponent.LayerSetSprite(sprite, new SpriteSpecifier.Rsi(component.RSIPath!, sprite));
            spriteComponent.LayerSetVisible(sprite, storageFill != null);
        }
    }

    private void EnableLayers(ItemMapperComponent component, SpriteComponent spriteComponent, AppearanceComponent appearance)
    {
        if (!appearance.TryGetData<ShowLayerData>(StorageMapVisuals.LayerChanged, out var wrapper))
            return;

        foreach (var layerName in component.SpriteLayers)
        {
            var show = wrapper.QueuedEntities.Contains(layerName);
            spriteComponent.LayerSetVisible(layerName, show);
        }
    }
}
