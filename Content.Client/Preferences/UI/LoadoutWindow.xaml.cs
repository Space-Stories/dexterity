using Content.Client.Lobby;
using Content.Client.UserInterface.Controls;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Preferences.UI;

[GenerateTypedNameReferences]
public sealed partial class LoadoutWindow : FancyWindow
{
    public event Action<ProtoId<LoadoutGroupPrototype>, ProtoId<LoadoutPrototype>?>? OnLoadoutPressed;

    private List<LoadoutGroupContainer> _groups = new();

    public LoadoutWindow(RoleLoadout loadout, RoleLoadoutPrototype proto, ICommonSession session, IDependencyCollection collection)
    {
        RobustXamlLoader.Load(this);
        var protoManager = collection.Resolve<IPrototypeManager>();

        foreach (var group in proto.Groups)
        {
            var container = new LoadoutGroupContainer(loadout, protoManager.Index(group), session, collection);
            LoadoutGroupsContainer.AddChild(container);
            _groups.Add(container);

            container.OnLoadoutPressed += args =>
            {
                OnLoadoutPressed?.Invoke(group, args);
            };
        }
    }

    public override void Close()
    {
        base.Close();
        var controller = UserInterfaceManager.GetUIController<LobbyUIController>();
        controller.SetDummyJob(null, null);
    }

    public void RefreshLoadouts(RoleLoadout loadout, ICommonSession session, IDependencyCollection collection)
    {
        foreach (var group in _groups)
        {
            group.RefreshLoadouts(loadout, session, collection);
        }
    }
}
