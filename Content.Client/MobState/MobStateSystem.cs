using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.MobState;
using Content.Shared.MobState.Components;
using Content.Shared.MobState.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameStates;

namespace Content.Client.MobState;

public sealed partial class MobStateSystem : SharedMobStateSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    private Overlays.DamageOverlay _overlay = default!;

    public const short Levels = 7;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new Overlays.DamageOverlay();
        IoCManager.Resolve<IOverlayManager>().AddOverlay(_overlay);

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttach);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetach);
        SubscribeLocalEvent<MobStateComponent, ComponentHandleState>(OnMobHandleState);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        IoCManager.Resolve<IOverlayManager>().RemoveOverlay(_overlay);
    }

    private void OnMobHandleState(EntityUid uid, MobStateComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not MobStateComponentState state) return;

        if (component.CurrentThreshold == state.CurrentThreshold)
           return;

        if (state.CurrentThreshold == null)
        {
            RemoveState(component);
        }
        else
        {
            UpdateState(component, state.CurrentThreshold.Value);
        }
    }

    private void OnPlayerAttach(PlayerAttachedEvent ev)
    {
        if (TryComp<MobStateComponent>(ev.Entity, out var mobState) && TryComp<DamageableComponent>(ev.Entity, out var damageable))
        {
            SetLevel(mobState, damageable.TotalDamage);
        }
    }

    private void OnPlayerDetach(PlayerDetachedEvent ev)
    {
        _overlay.Level = 0;
    }

    protected override void UpdateState(MobStateComponent component, DamageState? state, FixedPoint2 threshold)
    {
        base.UpdateState(component, state, threshold);
        SetLevel(component, threshold);
    }

    private void SetLevel(MobStateComponent stateComponent, FixedPoint2 threshold)
    {
        var uid = stateComponent.Owner;

        if (_playerManager.LocalPlayer?.ControlledEntity != uid) return;

        short modifier = 0;
        _overlay.Level = modifier;

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        switch (stateComponent.CurrentState)
        {
            case DamageState.Critical:
                _overlay.Level = Levels - 1;
                return;
            case DamageState.Dead:
                _overlay.Level = Levels;
                return;
        }

        if (TryGetEarliestIncapacitatedState(stateComponent, threshold, out _, out var earliestThreshold) && damageable.TotalDamage != 0)
        {
            modifier = (short)(damageable.TotalDamage / (earliestThreshold / Levels - 2) + 1);
        }

        _overlay.Level = modifier;
    }
}
