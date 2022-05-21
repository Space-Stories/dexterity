using System.Linq;
using Content.Client.Resources;
using Content.Client.Viewport;
using Content.Shared.DeviceNetwork;
using Content.Shared.SurveillanceCamera;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;

namespace Content.Client.SurveillanceCamera.UI;

[GenerateTypedNameReferences]
public sealed partial class SurveillanceCameraMonitorWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public event Action<string>? CameraSelected;
    public event Action<string>? SubnetOpened;
    public event Action? CameraRefresh;
    public event Action? SubnetRefresh;
    public event Action? CameraSwitchTimer;
    public event Action? CameraDisconnect;

    private string _currentAddress = string.Empty;
    private readonly FixedEye _defaultEye = new();
    private readonly Dictionary<string, int> _subnetMap = new();

    private string? SelectedSubnet
    {
        get
        {
            if (SubnetSelector.ItemCount == 0
                || SubnetSelector.SelectedMetadata == null)
            {
                return null;
            }

            return (string) SubnetSelector.SelectedMetadata;
        }
    }

    public SurveillanceCameraMonitorWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        // This could be done better. I don't want to deal with stylesheets at the moment.
        var texture = IoCManager.Resolve<IResourceCache>().GetTexture("/Textures/Interface/Nano/square_black.png");
        var shader = IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>("CameraStatic").Instance().Duplicate();

        CameraView.ViewportSize = new Vector2i(500, 500);
        CameraView.Eye = _defaultEye; // sure
        CameraViewBackground.Stretch = TextureRect.StretchMode.Scale;
        CameraViewBackground.Texture = texture;
        CameraViewBackground.ShaderOverride = shader;

        SubnetList.OnItemSelected += OnSubnetListSelect;

        SubnetSelector.OnItemSelected += args =>
        {
            // piss
            SubnetOpened!((string) args.Button.GetItemMetadata(args.Id)!);
        };
        SubnetRefreshButton.OnPressed += _ => SubnetRefresh!();
        CameraRefreshButton.OnPressed += _ => CameraRefresh!();
        CameraDisconnectButton.OnPressed += _ => CameraDisconnect!();
    }


    // The UI class should get the eye from the entity, and then
    // pass it here so that the UI can change its view.
    public void UpdateState(IEye? eye, HashSet<string> subnets, string activeAddress, string activeSubnet, Dictionary<string, string> cameras)
    {
        _currentAddress = activeAddress;
        SetCameraView(eye);

        if (subnets.Count == 0)
        {
            SubnetSelector.Visible = false;
            return;
        }

        SubnetSelector.Visible = true;

        // That way, we have *a* subnet selected if this is ever opened.
        if (string.IsNullOrEmpty(activeSubnet))
        {
            SubnetOpened!(subnets.First());
            return;
        }

        // if the subnet count is unequal, that means
        // we have to rebuild the subnet selector
        if (SubnetSelector.ItemCount != subnets.Count)
        {
            SubnetSelector.Clear();
            _subnetMap.Clear();

            foreach (var subnet in subnets)
            {
                var id = AddSubnet(subnet);
                _subnetMap.Add(subnet, id);
            }
        }

        if (_subnetMap.TryGetValue(activeSubnet, out var subnetId))
        {
            SubnetSelector.Select(subnetId);
        }

        PopulateCameraList(cameras);
    }

    private void PopulateCameraList(Dictionary<string, string> cameras)
    {
        SubnetList.Clear();

        foreach (var (address, name) in cameras)
        {
            AddCameraToList(name, address);
        }

        SubnetList.SortItemsByText();
    }

    private void SetCameraView(IEye? eye)
    {
        CameraView.Eye = eye ?? _defaultEye;
        CameraView.Visible = eye != null;
        CameraViewBackground.Visible = true;
        CameraDisconnectButton.Disabled = eye == null;

        if (eye == null)
        {
            CameraStatus.Text = Loc.GetString("surveillance-camera-monitor-ui-status",
                    ("status", Loc.GetString("surveillance-camera-monitor-ui-status-connecting")),
                    ("address", _currentAddress));
        }

        if (eye != null)
        {
            CameraStatus.Text = Loc.GetString("surveillance-camera-monitor-ui-status",
                ("status", Loc.GetString("surveillance-camera-monitor-ui-status-disconnected")));
            CameraSwitchTimer!();
        }
    }

    public void OnSwitchTimerComplete()
    {
        CameraViewBackground.Visible = false;
        CameraStatus.Text = Loc.GetString("surveillance-camera-monitor-ui-status",
                            ("status", Loc.GetString("surveillance-camera-monitor-ui-status-connected")),
                            ("address", _currentAddress));
    }

    private int AddSubnet(string subnet)
    {
        var name = subnet;
        if (_prototypeManager.TryIndex<DeviceFrequencyPrototype>(subnet, out var frequency))
        {
            name = Loc.GetString(frequency.Name ?? subnet);
        }

        SubnetSelector.AddItem(name);
        SubnetSelector.SetItemMetadata(SubnetSelector.ItemCount - 1, subnet);

        return SubnetSelector.ItemCount - 1;
    }

    private void AddCameraToList(string name, string address)
    {
        // var button = CreateCameraButton(name, address);
        var item = SubnetList.AddItem($"{name} - {address}");
        item.Metadata = address;
    }

    private void OnSubnetListSelect(ItemList.ItemListSelectedEventArgs args)
    {
        CameraSelected!((string) SubnetList[args.ItemIndex].Metadata!);
    }
}
