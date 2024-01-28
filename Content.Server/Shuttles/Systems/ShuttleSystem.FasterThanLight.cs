using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Body.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    /*
     * This is a way to move a shuttle from one location to another, via an intermediate map for fanciness.
     */

    public const float DefaultStartupTime = 5.5f;
    public const float DefaultTravelTime = 20f;
    public const float DefaultArrivalTime = 5f;
    private const float FTLCooldown = 10f;

    // I'm too lazy to make CVars.

    private readonly SoundSpecifier _startupSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    private readonly SoundSpecifier _arrivalSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    private readonly TimeSpan _hyperspaceKnockdownTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Left-side of the station we're allowed to use
    /// </summary>
    private float _index;

    /// <summary>
    /// Space between grids within hyperspace.
    /// </summary>
    private const float Buffer = 5f;

    /// <summary>
    /// How many times we try to proximity warp close to something before falling back to map-wideAABB.
    /// </summary>
    private const int FTLProximityIterations = 3;

    private readonly HashSet<EntityUid> _lookupEnts = new();

    private EntityQuery<BodyComponent> _bodyQuery;
    private EntityQuery<BuckleComponent> _buckleQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<StatusEffectsComponent> _statusQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private void InitializeFTL()
    {
        _bodyQuery = GetEntityQuery<BodyComponent>();
        _buckleQuery = GetEntityQuery<BuckleComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _statusQuery = GetEntityQuery<StatusEffectsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    /// <summary>
    /// Ensures the FTL map exists and returns it.
    /// </summary>
    private EntityUid EnsureFTLMap()
    {
        var query = AllEntityQuery<FTLMapComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            return uid;
        }

        var mapId = _mapManager.CreateMap();
        var mapUid = _mapManager.GetMapEntityId(mapId);
        var ftlMap = AddComp<FTLMapComponent>(mapUid);

        _metadata.SetEntityName(mapUid, "FTL");
        Log.Debug($"Setup hyperspace map at {mapUid}");
        DebugTools.Assert(!_mapManager.IsMapPaused(mapId));
        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = ftlMap.Parallax;

        return mapUid;
    }

    /// <summary>
    /// Returns true if the grid can FTL. Used to block protected shuttles like the emergency shuttle.
    /// </summary>
    public bool CanFTL(EntityUid? uid, [NotNullWhen(false)] out string? reason)
    {
        if (HasComp<PreventPilotComponent>(uid))
        {
            reason = Loc.GetString("shuttle-console-prevent");
            return false;
        }

        if (uid != null)
        {
            var ev = new ConsoleFTLAttemptEvent(uid.Value, false, string.Empty);
            RaiseLocalEvent(uid.Value, ref ev, true);

            if (ev.Cancelled)
            {
                reason = ev.Reason;
                return false;
            }
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Updates the whitelist for this FTL destination.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="whitelist"></param>
    public void SetFTLWhitelist(Entity<FTLDestinationComponent?> entity, EntityWhitelist? whitelist)
    {
        if (!Resolve(entity, ref entity.Comp))
            return;

        if (entity.Comp.Whitelist == whitelist)
            return;

        entity.Comp.Whitelist = whitelist;
        _console.RefreshShuttleConsoles();
    }

    /// <summary>
    /// Adds the target map as available for FTL.
    /// </summary>
    public bool TryAddFTLDestination(MapId mapId, bool enabled, [NotNullWhen(true)] out FTLDestinationComponent? component)
    {
        var mapUid = _mapManager.GetMapEntityId(mapId);
        component = null;

        if (!Exists(mapUid))
            return false;

        component = EnsureComp<FTLDestinationComponent>(mapUid);

        if (component.Enabled == enabled)
            return true;

        component.Enabled = enabled;
        _console.RefreshShuttleConsoles();
        return true;
    }

    [PublicAPI]
    public void RemoveFTLDestination(EntityUid uid)
    {
        if (!RemComp<FTLDestinationComponent>(uid))
            return;

        _console.RefreshShuttleConsoles();
    }

    /// <summary>
    /// Moves a shuttle from its current position to the target one without any checks. Goes through the hyperspace map while the timer is running.
    /// </summary>
    public void FTLToCoordinates(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityCoordinates coordinates,
        float startupTime = DefaultStartupTime,
        float hyperspaceTime = DefaultTravelTime,
        string? priorityTag = null)
    {
        var mapCoordinates = coordinates.ToMap(EntityManager, _transform);
        FTLToCoordinates((shuttleUid, component), mapCoordinates, startupTime, hyperspaceTime, priorityTag);
    }

    /// <summary>
    /// Moves a shuttle from its current position to the target one without any checks. Goes through the hyperspace map while the timer is running.
    /// </summary>
    public void FTLToCoordinates(Entity<ShuttleComponent> entity, MapCoordinates coordinates,
        float startupTime = DefaultStartupTime, float hyperspaceTime = DefaultTravelTime, string? priorityTag = null)
    {
        if (!TrySetupFTL(entity.Owner, entity.Comp, out var hyperspace))
            return;

        hyperspace.StartupTime = startupTime;
        hyperspace.TravelTime = hyperspaceTime;
        hyperspace.Accumulator = hyperspace.StartupTime;
        hyperspace.TargetCoordinates = coordinates;
        hyperspace.Dock = false;
        hyperspace.PriorityTag = priorityTag;
        _console.RefreshShuttleConsoles(entity);
        var ev = new FTLRequestEvent(_mapManager.GetMapEntityId(coordinates.MapId));
        RaiseLocalEvent(entity.Owner, ref ev, true);
    }

    /// <summary>
    /// Moves a shuttle from its current position to docked on the target one.
    /// Dock will be determined during the arrival timer and if none are found will position it nearby.
    /// </summary>
    public void FTLToDock(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityUid target,
        float startupTime = DefaultStartupTime,
        float hyperspaceTime = DefaultTravelTime,
        string? priorityTag = null)
    {
        if (!TrySetupFTL(shuttleUid, component, out var hyperspace))
            return;

        hyperspace.StartupTime = startupTime;
        hyperspace.TravelTime = hyperspaceTime;
        hyperspace.Accumulator = hyperspace.StartupTime;
        hyperspace.TargetUid = target;
        hyperspace.PriorityTag = priorityTag;
        _console.RefreshShuttleConsoles(shuttleUid);
    }

    private bool TrySetupFTL(EntityUid uid, ShuttleComponent shuttle, [NotNullWhen(true)] out FTLComponent? component)
    {
        component = null;

        if (HasComp<FTLComponent>(uid))
        {
            Log.Warning($"Tried queuing {ToPrettyString(uid)} which already has {nameof(FTLComponent)}?");
            return false;
        }

        _thruster.DisableLinearThrusters(shuttle);
        _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.North);
        _thruster.SetAngularThrust(shuttle, false);
        _dockSystem.SetDocks(uid, false);

        component = AddComp<FTLComponent>(uid);
        component.State = FTLState.Starting;
        var audio = _audio.PlayPvs(_startupSound, uid);
        audio.Value.Component.Flags |= AudioFlags.GridAudio;

        if (_physicsQuery.TryGetComponent(uid, out var gridPhysics))
        {
            _transform.SetLocalPosition(audio.Value.Entity, gridPhysics.LocalCenter);
        }

        // Make sure the map is setup before we leave to avoid pop-in (e.g. parallax).
        EnsureFTLMap();
        return true;
    }

    /// <summary>
    /// Transitions shuttle to FTL map.
    /// </summary>
    private void UpdateFTLStarting(Entity<FTLComponent> entity)
    {
        var uid = entity.Owner;
        var comp = entity.Comp;
        var xform = _xformQuery.GetComponent(entity);
        DoTheDinosaur(xform);

        comp.State = FTLState.Travelling;
        var fromMapUid = xform.MapUid;
        var fromMatrix = _transform.GetWorldMatrix(xform);
        var fromRotation = _transform.GetWorldRotation(xform);

        var width = Comp<MapGridComponent>(uid).LocalAABB.Width;
        var ftlMap = EnsureFTLMap();
        var body = _physicsQuery.GetComponent(entity);
        var shuttleCenter = body.LocalCenter;

        // Offset the start by buffer range just to avoid overlap.
        var ftlStart = new EntityCoordinates(ftlMap, new Vector2(_index + width / 2f, 0f) - shuttleCenter);

        _transform.SetCoordinates(entity.Owner, ftlStart);

        // Reset rotation so they always face the same direction.
        xform.LocalRotation = Angle.Zero;
        _index += width + Buffer;
        comp.Accumulator += comp.TravelTime - DefaultArrivalTime;

        Enable(uid, component: body);
        _physics.SetLinearVelocity(uid, new Vector2(0f, 20f), body: body);
        _physics.SetAngularVelocity(uid, 0f, body: body);
        _physics.SetLinearDamping(body, 0f);
        _physics.SetAngularDamping(body, 0f);

        _dockSystem.SetDockBolts(uid, true);
        _console.RefreshShuttleConsoles(uid);
        var target = comp.TargetUid != null
            ? new MapCoordinates(comp.TargetUid.Value, Vector2.Zero)
            : comp.TargetCoordinates;

        var ev = new FTLStartedEvent(uid, target, fromMapUid, fromMatrix, fromRotation);
        RaiseLocalEvent(uid, ref ev, true);

        // Audio
        var wowdio = _audio.PlayPvs(comp.TravelSound, uid);
        comp.TravelStream = wowdio?.Entity;
        if (wowdio?.Component != null)
        {
            wowdio.Value.Component.Flags |= AudioFlags.GridAudio;

            if (_physicsQuery.TryGetComponent(uid, out var gridPhysics))
            {
                _transform.SetLocalPosition(wowdio.Value.Entity, gridPhysics.LocalCenter);
            }
        }
    }

    /// <summary>
    /// Shuttle arriving.
    /// </summary>
    private void UpdateFTLTravelling(Entity<FTLComponent> entity)
    {
        comp.Accumulator += DefaultArrivalTime;
        comp.State = FTLState.Arriving;
        // TODO: Arrival effects
        // For now we'll just use the ss13 bubbles but we can do fancier.

        if (shuttle != null)
        {
            _thruster.DisableLinearThrusters(shuttle);
            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.South);
        }

        _console.RefreshShuttleConsoles(uid);
    }

    /// <summary>
    ///  Shuttle arrived.
    /// </summary>
    private void UpdateFTLArriving(Entity<FTLComponent> entity)
    {
        DoTheDinosaur(xform);
        SetDockBolts(uid, false);
        SetDocks(uid, true);

        if (TryComp(uid, out body))
        {
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(uid, 0f, body: body);
            if (shuttle != null)
            {
                _physics.SetLinearDamping(body, shuttle.LinearDamping);
                _physics.SetAngularDamping(body, shuttle.AngularDamping);
            }
        }

        MapId mapId;

        if (comp.TargetUid != null && shuttle != null)
        {
            if (!Deleted(comp.TargetUid))
            {
                if (comp.Dock)
                    TryFTLDock(uid, shuttle, comp.TargetUid.Value, comp.PriorityTag);
                else
                    TryFTLProximity(uid, comp.TargetUid.Value);

                mapId = Transform(comp.TargetUid.Value).MapID;
            }
            // oh boy, fallback time
            else
            {
                // Pick earliest map?
                var maps = EntityQuery<MapComponent>().Select(o => o.MapId).ToList();
                var map = maps.Min(o => o.GetHashCode());

                mapId = new MapId(map);
                TryFTLProximity(uid, _mapManager.GetMapEntityId(mapId));
            }
        }
        else
        {
            xform.Coordinates = comp.TargetCoordinates;
            mapId = comp.TargetCoordinates.GetMapId(EntityManager);
        }

        if (TryComp(uid, out body))
        {
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(uid, 0f, body: body);

            // Disable shuttle if it's on a planet; unfortunately can't do this in parent change messages due
            // to event ordering and awake body shenanigans (at least for now).
            if (HasComp<MapGridComponent>(xform.MapUid))
            {
                Disable(uid, component: body);
            }
            else if (shuttle != null)
            {
                Enable(uid, component: body, shuttle: shuttle);
            }
        }

        if (shuttle != null)
        {
            _thruster.DisableLinearThrusters(shuttle);
        }

        comp.TravelStream = _audio.Stop(comp.TravelStream);
        var audio = _audio.PlayPvs(_arrivalSound, uid);
        audio.Value.Component.Flags |= AudioFlags.GridAudio;
        // TODO: Shitcode til engine fix

        if (_physicsQuery.TryGetComponent(uid, out var gridPhysics))
        {
            _transform.SetLocalPosition(audio.Value.Entity, gridPhysics.LocalCenter);
        }

        if (TryComp<FTLDestinationComponent>(uid, out var dest))
        {
            dest.Enabled = true;
        }

        comp.State = FTLState.Cooldown;
        comp.Accumulator += FTLCooldown;
        _console.RefreshShuttleConsoles(uid);
        _mapManager.SetMapPaused(mapId, false);
        Smimsh(uid, xform: xform);

        var ftlEvent = new FTLCompletedEvent(uid, _mapManager.GetMapEntityId(mapId));
        RaiseLocalEvent(uid, ref ftlEvent, true);
    }

    private void UpdateFTLCooldown(Entity<FTLComponent> entity)
    {
        RemComp<FTLComponent>(entity);
        _console.RefreshShuttleConsoles(entity);
    }

    private void UpdateHyperspace(float frameTime)
    {
        var query = EntityQueryEnumerator<FTLComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Accumulator -= frameTime;

            if (comp.Accumulator > 0f)
                continue;

            var entity = (uid, comp);

            switch (comp.State)
            {
                // Startup time has elapsed and in hyperspace.
                case FTLState.Starting:
                    UpdateFTLStarting(entity);
                    break;
                // Arriving, play effects
                case FTLState.Travelling:
                    UpdateFTLTravelling(entity);
                    break;
                // Arrived
                case FTLState.Arriving:
                    UpdateFTLArriving(entity);
                    break;
                case FTLState.Cooldown:
                    UpdateFTLCooldown(entity);
                    break;
                default:
                    Log.Error($"Found invalid FTL state {comp.State} for {uid}");
                    RemCompDeferred<FTLComponent>(uid);
                    break;
            }
        }
    }

    private float GetSoundRange(EntityUid uid)
    {
        if (!_mapManager.TryGetGrid(uid, out var grid))
            return 4f;

        return MathF.Max(grid.LocalAABB.Width, grid.LocalAABB.Height) + 12.5f;
    }

    private void CleanupHyperspace()
    {
        _index = 0f;
        if (_hyperSpaceMap == null || !_mapManager.MapExists(_hyperSpaceMap.Value))
        {
            _hyperSpaceMap = null;
            return;
        }
        _mapManager.DeleteMap(_hyperSpaceMap.Value);
        _hyperSpaceMap = null;
    }

    /// <summary>
    /// Puts everyone unbuckled on the floor, paralyzed.
    /// </summary>
    private void DoTheDinosaur(TransformComponent xform)
    {
        // Get enumeration exceptions from people dropping things if we just paralyze as we go
        var toKnock = new ValueList<EntityUid>();
        KnockOverKids(xform, ref toKnock);
        TryComp<MapGridComponent>(xform.GridUid, out var grid);

        if (TryComp<PhysicsComponent>(xform.GridUid, out var shuttleBody))
        {
            foreach (var child in toKnock)
            {
                if (!_statusQuery.TryGetComponent(child, out var status))
                    continue;

                _stuns.TryParalyze(child, _hyperspaceKnockdownTime, true, status);

                // If the guy we knocked down is on a spaced tile, throw them too
                if (grid != null)
                    TossIfSpaced(grid, shuttleBody, child);
            }
        }
    }

    private void KnockOverKids(TransformComponent xform, ref ValueList<EntityUid> toKnock)
    {
        // Not recursive because probably not necessary? If we need it to be that's why this method is separate.
        var childEnumerator = xform.ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (!_buckleQuery.TryGetComponent(child, out var buckle) || buckle.Buckled)
                continue;

            toKnock.Add(child);
        }
    }

    /// <summary>
    /// Throws people who are standing on a spaced tile, tries to throw them towards a neighbouring space tile
    /// </summary>
    private void TossIfSpaced(MapGridComponent shuttleGrid, PhysicsComponent shuttleBody, EntityUid tossed)
    {
        if (!_xformQuery.TryGetComponent(tossed, out var childXform) )
            return;

        // only toss if its on lattice/space
        var tile = shuttleGrid.GetTileRef(childXform.Coordinates);

        if (!tile.IsSpace(_tileDefManager))
            return;

        var throwDirection = childXform.LocalPosition - shuttleBody.LocalCenter;

        if (throwDirection == Vector2.Zero)
            return;

        _throwing.TryThrow(tossed, throwDirection.Normalized() * 10.0f, 50.0f);
    }

    /// <summary>
    /// Tries to dock with the target grid, otherwise falls back to proximity.
    /// This bypasses FTL travel time.
    /// </summary>
    public bool TryFTLDock(EntityUid shuttleUid, ShuttleComponent component, EntityUid targetUid, string? priorityTag = null)
    {
        if (!TryComp<TransformComponent>(shuttleUid, out var shuttleXform) ||
            !TryComp<TransformComponent>(targetUid, out var targetXform) ||
            targetXform.MapUid == null ||
            !targetXform.MapUid.Value.IsValid())
        {
            return false;
        }

        var config = _dockSystem.GetDockingConfig(shuttleUid, targetUid, priorityTag);

        if (config != null)
        {
            FTLDock(config, shuttleXform);
            return true;
        }

        TryFTLProximity(shuttleUid, targetUid, shuttleXform, targetXform);
        return false;
    }

    /// <summary>
    /// Forces an FTL dock.
    /// </summary>
    public void FTLDock(Entity<TransformComponent> shuttle, DockingConfig config)
    {
        // Set position
        _transform.SetCoordinates(shuttle, config.Coordinates);
        _transform.SetWorldRotation(shuttle.Comp, config.Angle);

        // Connect everything
        foreach (var (dockAUid, dockBUid, dockA, dockB) in config.Docks)
        {
            _dockSystem.Dock(dockAUid, dockA, dockBUid, dockB);
        }
    }

    /// <summary>
    /// Tries to arrive nearby without overlapping with other grids.
    /// </summary>
    public bool TryFTLProximity(EntityUid shuttleUid, EntityUid targetUid, TransformComponent? xform = null, TransformComponent? targetXform = null)
    {
        if (!Resolve(targetUid, ref targetXform) ||
            targetXform.MapUid == null ||
            !targetXform.MapUid.Value.IsValid() ||
            !Resolve(shuttleUid, ref xform))
        {
            return false;
        }

        var xformQuery = GetEntityQuery<TransformComponent>();
        var shuttleAABB = Comp<MapGridComponent>(shuttleUid).LocalAABB;
        Box2 targetLocalAABB;

        // Spawn nearby.
        // We essentially expand the Box2 of the target area until nothing else is added then we know it's valid.
        // Can't just get an AABB of every grid as we may spawn very far away.
        if (TryComp<MapGridComponent>(targetXform.GridUid, out var targetGrid))
        {
            targetLocalAABB = targetGrid.LocalAABB;
        }
        else
        {
            targetLocalAABB = new Box2();
        }

        var targetAABB = _transform.GetWorldMatrix(targetXform, xformQuery)
            .TransformBox(targetLocalAABB).Enlarged(shuttleAABB.Size.Length());
        var nearbyGrids = new HashSet<EntityUid>();
        var iteration = 0;
        var lastCount = nearbyGrids.Count;
        var mapId = targetXform.MapID;
        var grids = new List<Entity<MapGridComponent>>();

        while (iteration < FTLProximityIterations)
        {
            grids.Clear();
            _mapManager.FindGridsIntersecting(mapId, targetAABB, ref grids);

            foreach (var grid in grids)
            {
                if (!nearbyGrids.Add(grid))
                    continue;

                targetAABB = targetAABB.Union(_transform.GetWorldMatrix(grid, xformQuery)
                    .TransformBox(Comp<MapGridComponent>(grid).LocalAABB));
            }

            // Can do proximity
            if (nearbyGrids.Count == lastCount)
            {
                break;
            }

            targetAABB = targetAABB.Enlarged(shuttleAABB.Size.Length() / 2f);
            iteration++;
            lastCount = nearbyGrids.Count;

            // Mishap moment, dense asteroid field or whatever
            if (iteration != FTLProximityIterations)
                continue;

            var query = AllEntityQuery<MapGridComponent>();
            while (query.MoveNext(out var uid, out var grid))
            {
                // Don't add anymore as it is irrelevant, but that doesn't mean we need to re-do existing work.
                if (nearbyGrids.Contains(uid))
                    continue;

                targetAABB = targetAABB.Union(_transform.GetWorldMatrix(uid, xformQuery)
                    .TransformBox(Comp<MapGridComponent>(uid).LocalAABB));
            }

            break;
        }

        Vector2 spawnPos;

        if (TryComp<PhysicsComponent>(shuttleUid, out var shuttleBody))
        {
            _physics.SetLinearVelocity(shuttleUid, Vector2.Zero, body: shuttleBody);
            _physics.SetAngularVelocity(shuttleUid, 0f, body: shuttleBody);
        }

        // TODO: This is pretty crude for multiple landings.
        if (nearbyGrids.Count > 1 || !HasComp<MapComponent>(targetXform.GridUid))
        {
            var minRadius = (MathF.Max(targetAABB.Width, targetAABB.Height) + MathF.Max(shuttleAABB.Width, shuttleAABB.Height)) / 2f;
            spawnPos = targetAABB.Center + _random.NextVector2(minRadius, minRadius + 64f);
        }
        else if (shuttleBody != null)
        {
            var (targetPos, targetRot) = _transform.GetWorldPositionRotation(targetXform, xformQuery);
            var transform = new Transform(targetPos, targetRot);
            spawnPos = Robust.Shared.Physics.Transform.Mul(transform, -shuttleBody.LocalCenter);
        }
        else
        {
            spawnPos = _transform.GetWorldPosition(targetXform, xformQuery);
        }

        xform.Coordinates = new EntityCoordinates(targetXform.MapUid.Value, spawnPos);

        if (!HasComp<MapComponent>(targetXform.GridUid))
        {
            _transform.SetLocalRotation(xform, _random.NextAngle());
        }
        else
        {
            _transform.SetLocalRotation(xform, Angle.Zero);
        }

        return true;
    }

    /// <summary>
    /// Flattens / deletes everything under the grid upon FTL.
    /// </summary>
    private void Smimsh(EntityUid uid, FixturesComponent? manager = null, MapGridComponent? grid = null, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref manager, ref grid, ref xform) || xform.MapUid == null)
            return;

        // Flatten anything not parented to a grid.
        var transform = _physics.GetPhysicsTransform(uid, xform);
        var aabbs = new List<Box2>(manager.Fixtures.Count);
        var immune = new HashSet<EntityUid>();
        var tileSet = new List<(Vector2i, Tile)>();

        foreach (var fixture in manager.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            var aabb = fixture.Shape.ComputeAABB(transform, 0);

            // Shift it slightly
            aabb = aabb.Translated(-grid.TileSizeHalfVector);
            // Create a small border around it.
            aabb = aabb.Enlarged(0.2f);
            aabbs.Add(aabb);

            // Handle clearing biome stuff as relevant.
            tileSet.Clear();
            _biomes.ReserveTiles(xform.MapUid.Value, aabb, tileSet);
            _lookupEnts.Clear();
            _lookup.GetEntitiesIntersecting(xform.MapUid.Value, aabb, _lookupEnts, LookupFlags.Uncontained);

            foreach (var ent in _lookupEnts)
            {
                if (ent == uid || immune.Contains(ent))
                {
                    continue;
                }

                if (_ghostQuery.HasComponent(ent))
                {
                    continue;
                }

                if (_bodyQuery.TryGetComponent(ent, out var mob))
                {
                    var gibs = _bobby.GibBody(ent, body: mob);
                    immune.UnionWith(gibs);
                    continue;
                }

                QueueDel(ent);
            }
        }

        var ev = new ShuttleFlattenEvent(xform.MapUid.Value, aabbs);
        RaiseLocalEvent(ref ev);
    }
}
