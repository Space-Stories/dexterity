using Content.Server.Explosion.EntitySystems;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Emp;

public sealed class EmpSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public const string EmpPulseEffectPrototype = "EffectEmpPulse";
    public const string EmpDisabledEffectPrototype = "EffectEmpDisabled";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmpDisabledComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<EmpDisabledComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<EmpOnTriggerComponent, TriggerEvent>(HandleEmpTrigger);
    }

    public void EmpPulse(MapCoordinates coordinates, float range, float energyConsumption, float duration)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(coordinates, range))
        {
            var ev = new EmpPulseEvent(energyConsumption, false, false);
            RaiseLocalEvent(uid, ref ev);
            if (ev.Affected)
            {
                Spawn(EmpDisabledEffectPrototype, Transform(uid).Coordinates);
            }
            if (ev.Disabled)
            {
                var disabled = EnsureComp<EmpDisabledComponent>(uid);
                disabled.DisabledUntil = _timing.CurTime + TimeSpan.FromSeconds(duration);
            }
        }
        Spawn(EmpPulseEffectPrototype, coordinates);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EmpDisabledComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var transform))
        {
            if (comp.DisabledUntil < _timing.CurTime)
            {
                RemComp<EmpDisabledComponent>(uid);
                var ev = new EmpDisabledRemoved();
                RaiseLocalEvent(uid, ref ev);
                continue;
            }

            if (_timing.CurTime > comp.TargetTime)
            {
                comp.TargetTime = _timing.CurTime + _random.NextFloat(0.8f, 1.2f) * TimeSpan.FromSeconds(comp.EffectCooldown);
                Spawn(EmpDisabledEffectPrototype, transform.Coordinates);
            }
        }
    }

    private void OnUnpaused(EntityUid uid, EmpDisabledComponent component, ref EntityUnpausedEvent args)
    {
        component.DisabledUntil += args.PausedTime;
        component.TargetTime += args.PausedTime;
    }

    private void OnExamine(EntityUid uid, EmpDisabledComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("emp-disabled-comp-on-examine"));
    }

    private void HandleEmpTrigger(EntityUid uid, EmpOnTriggerComponent comp, TriggerEvent args)
    {
        EmpPulse(Transform(uid).MapPosition, comp.Range, comp.EnergyConsumption, comp.DisableDuration);
        args.Handled = true;
    }
}

[ByRefEvent]
public record struct EmpPulseEvent(float EnergyConsumption, bool Affected, bool Disabled);

[ByRefEvent]
public record struct EmpDisabledRemoved();
