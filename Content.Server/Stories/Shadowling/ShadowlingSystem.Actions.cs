using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Emp;
using Content.Server.Flash;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Lightning;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.RoundEnd;
using Content.Server.Stories.Lib.TemporalLightOff;
using Content.Server.Stunnable;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Stories.Shadowling;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Standing;
using Content.Shared.Mindshield.Components;
using Content.Shared.Body.Components;
using Robust.Shared.Audio;
using Content.Shared.Silicons.Borgs.Components;
using Content.Server.Chemistry.Containers.EntitySystems;

namespace Content.Server.Stories.Shadowling;
public sealed partial class ShadowlingSystem
{
    [Dependency] private readonly TemporalLightOffSystem _temporalLightOff = default!;
    [Dependency] private readonly ShadowlingSystem _shadowling = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SolutionContainerSystem _solution = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly EmpSystem _emp = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly FlashSystem _flash = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    public readonly List<string> DefaultAbilities = new()
    {
        "ActionShadowlingGlare",
        "ActionShadowlingVeil",
        "ActionShadowlingShadowWalk",
        "ActionShadowlingIcyVeins",
        "ActionShadowlingCollectiveMind",
        "ActionShadowlingRapidReHatch",
    };
    private void InitializeActions()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingVeilEvent>(OnVeilEvent); // Пелена
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingSonicScreechEvent>(OnSonicScreechEvent); // Визг
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingShadowWalkEvent>(OnShadowWalkEvent); // Теневой шаг
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingRapidReHatchEvent>(OnRapidReHatchEvent); // Быстрое перераскрытие
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingLightningStormEvent>(OnLightningStormEvent); // Электро шторм
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingIcyVeinsEvent>(OnIcyVeinsEvent); // Ледяное масло
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingHatchEvent>(OnHatch); // Расскрытие
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingBlindnessSmokeEvent>(OnBlindnessSmokeEvent); // Дым
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingCollectiveMindEvent>(OnCollectiveEvent); // Подсчет слуг
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingAscendanceEvent>(OnAscendance); // Вознесение

        SubscribeLocalEvent<ShadowlingGlareEvent>(OnGlareEvent); // Ослепление
        SubscribeLocalEvent<ShadowlingComponent, ShadowlingEnthrallEvent>(OnEnthrallEvent); // Вербовка

        SubscribeLocalEvent<ShadowlingAnnihilateEvent>(OnAnnihilateEvent); // Аннигиляция
        SubscribeLocalEvent<ShadowlingBlackRecuperationEvent>(OnBlackRecuperationEvent); // Возрождение
    }
    private void OnCollectiveEvent(EntityUid uid, ShadowlingComponent component, ShadowlingCollectiveMindEvent args)
    {
        if (args.Handled)
            return;

        RefreshActions(uid);
        _popup.PopupEntity($"У вас {GetThralls(uid)} живых порабощённых", uid, uid);

        args.Handled = true;
    }
    private void OnEnthrallEvent(EntityUid uid, ShadowlingComponent component, ShadowlingEnthrallEvent args)
    {
        if (args.Handled)
            return;

        EnsureComp<ShadowlingThrallComponent>(args.Target);
        component.Thralls.Add(args.Target);

        args.Handled = true;
    }
    private void OnBlindnessSmokeEvent(EntityUid uid, ShadowlingComponent component, ShadowlingBlindnessSmokeEvent args)
    {
        if (args.Handled)
            return;

        var solution = new Solution();
        solution.AddReagent("ShadowlingSmokeReagent", 100);

        var smokeEnt = Spawn("Smoke", _transform.GetMapCoordinates(uid));
        _smoke.StartSmoke(smokeEnt, solution, 30, 7);

        args.Handled = true;
    }
    private void OnBlackRecuperationEvent(ShadowlingBlackRecuperationEvent args)
    {
        if (args.Handled)
            return;

        if (!_mobState.IsIncapacitated(args.Target) || !HasComp<ShadowlingThrallComponent>(args.Target))
            return;

        _popup.PopupEntity("Ваши раны покрываются тенью и затягиваются...", args.Target, args.Target);
        RaiseLocalEvent(args.Target, new RejuvenateEvent());

        args.Handled = true;
    }
    private void OnAnnihilateEvent(ShadowlingAnnihilateEvent args)
    {
        if (args.Handled)
            return;

        _body.GibBody(args.Target);

        args.Handled = true;
    }
    private void OnAscendance(EntityUid uid, ShadowlingComponent component, ShadowlingAscendanceEvent args)
    {
        if (args.Handled)
            return;

        var solution = new Solution();
        solution.AddReagent("ShadowlingSmokeReagent", 100);

        var smokeEnt = Spawn("Smoke", _transform.GetMapCoordinates(uid));
        _smoke.StartSmoke(smokeEnt, solution, 5, 7);

        var ent = _polymorph.PolymorphEntity(uid, "Ascended");

        var announcementString = "Сканерами дальнего действия было зафиксировано превознесение тенеморфа, к вам будет отправлен экстренный эвакуационный шаттл.";

        _chat.DispatchGlobalAnnouncement(announcementString, playSound: true, colorOverride: Color.Red);
        _audio.PlayGlobal("/Audio/Stories/Misc/tear_of_veil.ogg", Filter.Broadcast(), true, AudioParams.Default.WithVolume(-2f));
        _roundEnd.RequestRoundEnd(TimeSpan.FromMinutes(3), ent, false);

        args.Handled = true;
    }
    private void OnGlareEvent(ShadowlingGlareEvent args)
    {
        if (args.Handled)
            return;

        _flash.Flash(args.Target, args.Performer, null, 15000, 0.8f, false);
        _stun.TryStun(args.Target, TimeSpan.FromSeconds(7), false);

        args.Handled = true;
    }
    private void OnHatch(EntityUid uid, ShadowlingComponent component, ShadowlingHatchEvent args)
    {
        if (args.Handled)
            return;

        var solution = new Solution();
        solution.AddReagent("ShadowlingSmokeReagent", 100);

        var smokeEnt = Spawn("Smoke", _transform.GetMapCoordinates(uid));
        _smoke.StartSmoke(smokeEnt, solution, 5, 7);

        var ent = _polymorph.PolymorphEntity(uid, "Shadowling");

        if (ent != null)
            _stun.TryStun(ent.Value, TimeSpan.FromSeconds(5), true);

        args.Handled = true;
    }
    private void OnIcyVeinsEvent(EntityUid uid, ShadowlingComponent component, ShadowlingIcyVeinsEvent args)
    {
        if (args.Handled)
            return;

        var targets = _lookup.GetEntitiesInRange<BodyComponent>(_transform.GetMapCoordinates(args.Performer), 7.5f);

        foreach (var target in targets)
        {
            if (!_solution.TryGetSolution(target.Owner, "chemicals", out var solution))
                continue;

            var toAdd = new Solution();
            toAdd.AddReagent("IceOil", 10);

            _solution.TryAddSolution(solution.Value, toAdd);
        }

        args.Handled = true;
    }
    private void OnLightningStormEvent(EntityUid uid, ShadowlingComponent component, ShadowlingLightningStormEvent args)
    {
        if (args.Handled)
            return;

        var targets = _lookup.GetEntitiesInRange<BodyComponent>(_transform.GetMapCoordinates(args.Performer), 15);

        foreach (var target in targets)
        {
            if (HasComp<ShadowlingComponent>(target) || HasComp<ShadowlingThrallComponent>(target))
                continue;

            _lightning.ShootLightning(uid, target);
        }

        _emp.EmpPulse(_transform.GetMapCoordinates(Transform(uid)), 12, 10000, 30);

        args.Handled = true;
    }
    private void OnRapidReHatchEvent(EntityUid uid, ShadowlingComponent component, ShadowlingRapidReHatchEvent args)
    {
        if (args.Handled)
            return;

        RaiseLocalEvent(uid, new RejuvenateEvent());

        args.Handled = true;
    }
    private void OnShadowWalkEvent(EntityUid uid, ShadowlingComponent component, ShadowlingShadowWalkEvent args)
    {
        if (args.Handled)
            return;

        _polymorph.PolymorphEntity(uid, "ShadowlingWalk");

        args.Handled = true;
    }
    private void OnVeilEvent(EntityUid uid, ShadowlingComponent component, ShadowlingVeilEvent args)
    {
        if (args.Handled)
            return;

        _temporalLightOff.DisableLightsInRange(args.Performer, 5f, TimeSpan.FromMinutes(2));

        args.Handled = true;
    }
    private void OnSonicScreechEvent(EntityUid uid, ShadowlingComponent component, ShadowlingSonicScreechEvent args)
    {
        if (args.Handled)
            return;

        var targets = _lookup.GetEntitiesInRange<BodyComponent>(_transform.GetMapCoordinates(args.Performer), 15);

        foreach (var target in targets)
        {
            _stun.TryParalyze(target, TimeSpan.FromSeconds(args.StunTime), true);
            _popup.PopupEntity("Волна визга оглушает вас!", target, target);

            if (HasComp<BorgChassisComponent>(target))
                _emp.DoEmpEffects(target, 50000, 15); // Huh
        }

        args.Handled = true;
    }
}
