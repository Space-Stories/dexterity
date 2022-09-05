using Content.Shared.DeviceNetwork;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client.NetworkConfigurator;

public sealed class NetworkConfiguratorLinkOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private readonly DeviceListSystem _deviceListSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NetworkConfiguratorLinkOverlay()
    {
        IoCManager.InjectDependencies(this);

        _deviceListSystem = _entityManager.System<DeviceListSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var tracker in _entityManager.EntityQuery<NetworkConfiguratorActiveLinkOverlayComponent>())
        {
            if (_entityManager.Deleted(tracker.Owner) || !_entityManager.TryGetComponent(tracker.Owner, out DeviceListComponent? deviceList))
            {
                _entityManager.RemoveComponentDeferred<NetworkConfiguratorActiveLinkOverlayComponent>(tracker.Owner);
                continue;
            }

            var sourceTransform = _entityManager.GetComponent<TransformComponent>(tracker.Owner);

            foreach (var device in _deviceListSystem.GetAllDevices(tracker.Owner, deviceList))
            {
                if (_entityManager.Deleted(device))
                {
                    continue;
                }

                var linkTransform = _entityManager.GetComponent<TransformComponent>(device);

                args.WorldHandle.DrawLine(sourceTransform.WorldPosition, linkTransform.WorldPosition, Color.Blue);
            }
        }
    }
}
