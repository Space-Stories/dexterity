using Content.Server.Actions;
using Content.Server.Popups;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Stories.Shadowling;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server.Stories.Shadowling;

public sealed partial class ShadowlingSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowlingComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ShadowlingComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<ShadowlingThrallComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ShadowlingThrallComponent, ComponentInit>(OnInit);

        InitializeActions();
    }


    private void OnInit(EntityUid uid, ShadowlingThrallComponent component, ComponentInit args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        humanoid.EyeColor = Color.Red;
    }

    private void OnExamine(EntityUid uid, ShadowlingThrallComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (Identity.Name(args.Examined, EntityManager, args.Examiner) == MetaData(uid).EntityName)
            args.PushMarkup(Loc.GetString("thrall-examine"));
    }

    private void RefreshActions(EntityUid uid, ShadowlingComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        foreach (var act in component.GrantedActions)
        {
            Del(act);
        }

        component.GrantedActions.Clear();

        foreach (var (action, tharlls) in component.Actions)
        {
            if (GetThralls(uid) < tharlls)
                continue;

            EntityUid? act = null;
            if (_actions.AddAction(uid, ref act, action, uid))
                component.GrantedActions.Add(act.Value);
        }
    }

    private void OnInit(EntityUid uid, ShadowlingComponent component, ComponentInit args)
    {
        RefreshActions(uid);
    }

    private void OnShotAttempted(EntityUid uid, ShadowlingComponent comp, ref ShotAttemptedEvent args)
    {
        _popup.PopupEntity(Loc.GetString("gun-disabled"), uid, uid);
        args.Cancel();
    }

    private int GetThralls(EntityUid uid, ShadowlingComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return 0;

        var tharlls = 0;

        foreach (var tharll in component.Thralls)
        {
            if (!Deleted(tharll) && !_mobState.IsIncapacitated(tharll) && HasComp<ShadowlingThrallComponent>(tharll))
                tharlls++;
        }

        return tharlls;
    }
}
