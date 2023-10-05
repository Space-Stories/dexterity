using Content.Shared.Ghost.Roles;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Ghost.Controls.Roles
{
    [GenerateTypedNameReferences]
    public sealed partial class GhostRolesWindow : DefaultWindow
    {
        public event Action<GhostRoleInfo>? OnRoleRequested;
        public event Action<GhostRoleInfo>? OnRoleFollow;

        public void ClearEntries()
        {
            NoRolesMessage.Visible = true;
            EntryContainer.DisposeAllChildren();
        }

        public void AddEntry(string name, string description, bool hasAccess, FormattedMessage? reason, IEnumerable<GhostRoleInfo> roles, SpriteSystem spriteSystem)
        {
            NoRolesMessage.Visible = false;

            var entry = new GhostRolesEntry(name, description, hasAccess, reason, roles, spriteSystem);
            entry.OnRoleSelected += OnRoleRequested;
            entry.OnRoleFollow += OnRoleFollow;
            EntryContainer.AddChild(entry);
        }
    }
}
