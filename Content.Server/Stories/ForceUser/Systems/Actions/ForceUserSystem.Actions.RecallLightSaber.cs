using Content.Shared.Stories.ForceUser;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Timing;
using Robust.Shared.Timing;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Misc;
using Content.Server.Stories.TetherGun;
using Content.Shared.Stories.ForceUser.Actions.Events;
using Content.Shared.Stories.PullTo;
using Content.Shared.Stories.Force.LightSaber;

namespace Content.Server.Stories.ForceUser;
public sealed partial class ForceUserSystem
{
    public void InitializeRecall()
    {
        SubscribeLocalEvent<ForceUserComponent, RecallLightSaberEvent>(OnRecall);
        SubscribeLocalEvent<LightSaberComponent, PulledToTimeOutEvent>(OnTimeOut);
    }
    private void OnTimeOut(EntityUid uid, LightSaberComponent comp, PulledToTimeOutEvent args)
    {
        if (args.Handled || comp.LightSaberOwner != args.Component.PulledTo || args.Component.PulledTo == null)
            return;

        _popup.PopupEntity(Loc.GetString(_hands.TryPickupAnyHand(args.Component.PulledTo.Value, uid) ? "Ваш световой меч телепортируется вам в руку!" : "ninja-hands-full"), args.Component.PulledTo.Value, args.Component.PulledTo.Value);
    }
    private void OnRecall(EntityUid uid, ForceUserComponent comp, RecallLightSaberEvent args)
    {
        if (args.Handled || comp.LightSaber == null)
            return;

        if (_container.IsEntityInContainer(comp.LightSaber.Value) && !_container.TryRemoveFromContainer(comp.LightSaber.Value))
            return;

        if (TryComp<TetheredComponent>(comp.LightSaber.Value, out var tetheredComponent))
            _tetherGunSystem.StopTether(tetheredComponent.Tetherer, EnsureComp<TetherGunComponent>(tetheredComponent.Tetherer));

        _pullTo.TryPullTo(comp.LightSaber.Value, uid, PulledToOnEnter.PickUp, duration: 10f);

        args.Handled = true;
    }
}
