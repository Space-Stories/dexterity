using Content.Shared.Gateway;
using Robust.Client.GameObjects;

namespace Content.Client.Gateway.UI;

public sealed class GatewayBoundUserInterface : BoundUserInterface
{
    private GatewayWindow? _window;

    public GatewayBoundUserInterface(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new GatewayWindow();
        _window.OpenPortal += destination =>
        {
            SendMessage(new GatewayOpenPortalMessage()
            {
                Destination = destination
            });
        };
        _window.OnClose += Close;
        _window?.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Dispose();
        _window = null;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not GatewayBoundUserInterfaceState current)
            return;

        _window?.UpdateState(current);
    }
}
