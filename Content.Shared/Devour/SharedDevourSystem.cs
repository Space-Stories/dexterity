using Content.Server.Devour.Components;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Devour;

public abstract class SharedDevourSystem : EntitySystem
{
    [Dependency] protected readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevourerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DevourerComponent, DevourActionEvent>(OnDevourAction);
    }

    protected void OnStartup(EntityUid uid, DevourerComponent component, ComponentStartup args)
    {
        //Devourer doesn't actually chew, since he sends targets right into his stomach.
        //I did it mom, I added ERP content into upstream. Legally!
        component.Stomach = _containerSystem.EnsureContainer<Container>(uid, "stomach");

        if (component.DevourAction != null)
            _actionsSystem.AddAction(uid, component.DevourAction, null);
    }

    /// <summary>
    /// The devour action
    /// </summary>
    protected void OnDevourAction(EntityUid uid, DevourerComponent component, DevourActionEvent args)
    {
        if (args.Handled || component.Whitelist?.IsValid(args.Target, EntityManager) != true)
            return;

        args.Handled = true;
        var target = args.Target;

        // Structure and mob devours handled differently.
        if (TryComp(target, out MobStateComponent? targetState))
        {
            switch (targetState.CurrentState)
            {
                case MobState.Critical:
                case MobState.Dead:

                    _doAfterSystem.TryStartDoAfter(new DoAfterArgs(uid, component.DevourTime, new DevourDoAfterEvent(), uid, target: target, used: uid)
                    {
                        BreakOnTargetMove = true,
                        BreakOnUserMove = true,
                    });
                    break;
                default:
                    if (_net.IsClient && _timing.IsFirstTimePredicted)
                    {
                        _popupSystem.PopupEntity(Loc.GetString("devour-action-popup-message-fail-target-alive"), uid,
                            uid);
                    }

                    break;
            }

            return;
        }

        if (_net.IsClient && _timing.IsFirstTimePredicted)
        {
            _popupSystem.PopupEntity(Loc.GetString("devour-action-popup-message-structure"), uid, uid);
        }

        if (_net.IsServer)
        {
            if (component.SoundStructureDevour != null)
                _audioSystem.PlayPvs(component.SoundStructureDevour, uid, component.SoundStructureDevour.Params);
        }

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(uid, component.StructureDevourTime, new DevourDoAfterEvent(), uid, target: target, used: uid)
        {
            BreakOnTargetMove = true,
            BreakOnUserMove = true,
        });
    }
}

public sealed partial class DevourActionEvent : EntityTargetActionEvent { }

[Serializable, NetSerializable]
public sealed partial class DevourDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public enum FoodPreference : byte
{
    Humanoid = 0,
    All = 1
}
