using Content.Shared.StatusIcon.Components;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Client.Player;
using Content.Shared.Ghost;
using Content.Shared.Stories.Empire.Components;
using Content.Client.Antag;

namespace Content.Client.Stories.Shadowling;
public sealed class EmpireSystem : SharedStatusIconSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmpireComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
        SubscribeLocalEvent<HypnotizedEmpireComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }
    private void OnGetStatusIconsEvent(EntityUid uid, EmpireComponent component, ref GetStatusIconsEvent args)
    {
        if (!HasComp<HypnotizedEmpireComponent>(uid))
            GetStatusIcon(component.StatusIcon, ref args, component.IconVisibleToGhost);
    }
    private void OnGetStatusIconsEvent(EntityUid uid, HypnotizedEmpireComponent component, ref GetStatusIconsEvent args)
    {
        GetStatusIcon(component.StatusIcon, ref args, component.IconVisibleToGhost);
    }
    private void GetStatusIcon(string antagStatusIcon, ref GetStatusIconsEvent args, bool visibleToGhost = true)
    {
        var ent = _player.LocalSession?.AttachedEntity;

        if (HasComp<GhostComponent>(ent) && !visibleToGhost)
            return;

        args.StatusIcons.Add(_prototype.Index<StatusIconPrototype>(antagStatusIcon));
    }
}
