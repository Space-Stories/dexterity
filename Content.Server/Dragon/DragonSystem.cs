using System.Linq;
using Content.Server.Body.Systems;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.CharacterAppearance.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.MobState;
using Content.Shared.MobState.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using System.Threading;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Dragon;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Dragon
{
    public sealed class DragonSystem : EntitySystem
    {
        [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
        [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly FlammableSystem _flammableSystem = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        /// <summary>
        ///     Tracks breath attacks performed per-entity.
        /// </summary>
        private readonly Dictionary<EntityUid, BreathAttack> _breathAttacks = new();

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<DragonComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<DragonComponent, DragonDevourComplete>(OnDragonDevourComplete);
            SubscribeLocalEvent<DragonComponent, DragonDevourActionEvent>(OnDevourAction);
            SubscribeLocalEvent<DragonComponent, DragonSpawnActionEvent>(OnDragonSpawnAction);
            SubscribeLocalEvent<DragonComponent, DragonBreatheFireActionEvent>(OnDragonBreathFire);

            SubscribeLocalEvent<DragonComponent, DragonStructureDevourComplete>(OnDragonStructureDevourComplete);
            SubscribeLocalEvent<DragonComponent, DragonDevourCancelledEvent>(OnDragonDevourCancelled);
            SubscribeLocalEvent<DragonComponent, MobStateChangedEvent>(OnMobStateChanged);
        }

        private void OnMobStateChanged(EntityUid uid, DragonComponent component, MobStateChangedEvent args)
        {
            //Empties the stomach upon death
            //TODO: Do this when the dragon gets butchered instead
            if (args.CurrentMobState.IsDead())
            {
                if (component.SoundDeath != null)
                    SoundSystem.Play(component.SoundDeath.GetSound(), Filter.Pvs(uid, entityManager: EntityManager), uid, component.SoundDeath.Params);

                component.DragonStomach.EmptyContainer();
            }
        }

        private void OnDragonDevourCancelled(EntityUid uid, DragonComponent component, DragonDevourCancelledEvent args)
        {
            component.CancelToken = null;
        }

        private void OnDragonDevourComplete(EntityUid uid, DragonComponent component, DragonDevourComplete args)
        {
            component.CancelToken = null;
            var ichorInjection = new Solution(component.DevourChem, component.DevourHealRate);

            //Humanoid devours allow dragon to get eggs, corpses included
            if (EntityManager.HasComponent<HumanoidAppearanceComponent>(args.Target))
            {
                // Add a spawn for a consumed humanoid
                component.SpawnsLeft = Math.Min(component.SpawnsLeft + 1, component.MaxSpawns);
            }
            //Non-humanoid mobs can only heal dragon for half the normal amount, with no additional spawn tickets
            else
            {
                ichorInjection.ScaleSolution(0.5f);
            }

            _bloodstreamSystem.TryAddToChemicals(uid, ichorInjection);
            component.DragonStomach.Insert(args.Target);

            if (component.SoundDevour != null)
                SoundSystem.Play(component.SoundDevour.GetSound(), Filter.Pvs(uid, entityManager: EntityManager), uid, component.SoundDevour.Params);
        }

        private void OnDragonStructureDevourComplete(EntityUid uid, DragonComponent component, DragonStructureDevourComplete args)
        {
            component.CancelToken = null;
            //TODO: Figure out a better way of removing structures via devour that still entails standing still and waiting for a DoAfter. Somehow.
            EntityManager.QueueDeleteEntity(args.Target);

            if (component.SoundDevour != null)
                SoundSystem.Play(component.SoundDevour.GetSound(), Filter.Pvs(args.User, entityManager: EntityManager), uid, component.SoundDevour.Params);
        }

        private void OnStartup(EntityUid uid, DragonComponent component, ComponentStartup args)
        {
            component.SpawnsLeft = Math.Min(component.SpawnsLeft, component.MaxSpawns);

            //Dragon doesn't actually chew, since he sends targets right into his stomach.
            //I did it mom, I added ERP content into upstream. Legally!
            component.DragonStomach = _containerSystem.EnsureContainer<Container>(uid, "dragon_stomach");

            if (component.DevourAction != null)
                _actionsSystem.AddAction(uid, component.DevourAction, null);

            if (component.SpawnAction != null)
                _actionsSystem.AddAction(uid, component.SpawnAction, null);

            if(component.BreatheFireAction != null)
                _actionsSystem.AddAction(uid, component.BreatheFireAction, null);

            if (component.SoundRoar != null)
                SoundSystem.Play(component.SoundRoar.GetSound(), Filter.Pvs(uid, 4f, EntityManager), uid, component.SoundRoar.Params);
        }

        /// <summary>
        /// The devour action
        /// </summary>
        private void OnDevourAction(EntityUid uid, DragonComponent component, DragonDevourActionEvent args)
        {
            if (component.CancelToken != null ||
                args.Handled ||
                component.DevourWhitelist?.IsValid(args.Target, EntityManager) != true) return;

            args.Handled = true;
            var target = args.Target;

            // Structure and mob devours handled differently.
            if (EntityManager.TryGetComponent(target, out MobStateComponent? targetState))
            {
                switch (targetState.CurrentState)
                {
                    case DamageState.Critical:
                    case DamageState.Dead:
                        component.CancelToken = new CancellationTokenSource();

                        _doAfterSystem.DoAfter(new DoAfterEventArgs(uid, component.DevourTime, component.CancelToken.Token, target)
                        {
                            UserFinishedEvent = new DragonDevourComplete(uid, target),
                            UserCancelledEvent = new DragonDevourCancelledEvent(),
                            BreakOnTargetMove = true,
                            BreakOnUserMove = true,
                            BreakOnStun = true,
                        });
                        break;
                    default:
                        _popupSystem.PopupEntity(Loc.GetString("devour-action-popup-message-fail-target-alive"), uid, Filter.Entities(uid));
                        break;
                }

                return;
            }

            _popupSystem.PopupEntity(Loc.GetString("devour-action-popup-message-structure"), uid, Filter.Entities(uid));

            if (component.SoundStructureDevour != null)
                SoundSystem.Play(component.SoundStructureDevour.GetSound(), Filter.Pvs(uid, entityManager: EntityManager), uid, component.SoundStructureDevour.Params);

            component.CancelToken = new CancellationTokenSource();

            _doAfterSystem.DoAfter(new DoAfterEventArgs(uid, component.StructureDevourTime, component.CancelToken.Token, target)
            {
                UserFinishedEvent = new DragonStructureDevourComplete(uid, target),
                UserCancelledEvent = new DragonDevourCancelledEvent(),
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                BreakOnStun = true,
            });
        }

        private void OnDragonSpawnAction(EntityUid dragonuid, DragonComponent component, DragonSpawnActionEvent args)
        {
            if (component.SpawnPrototype == null) return;

            // If dragon has spawns then add one.
            if (component.SpawnsLeft > 0)
            {
                Spawn(component.SpawnPrototype, Transform(dragonuid).Coordinates);
                component.SpawnsLeft--;
                return;
            }

            _popupSystem.PopupEntity(Loc.GetString("dragon-spawn-action-popup-message-fail-no-eggs"), dragonuid, Filter.Entities(dragonuid));
        }

        private void OnDragonBreathFire(EntityUid dragonuid, DragonComponent component,
            DragonBreatheFireActionEvent args)
        {
            if (args.Handled || component.BreatheFireAction == null)
                return;

            // Check if dragon has a breath attack currently processing.
            if (_breathAttacks.TryGetValue(dragonuid, out var breathAttack) && !breathAttack.Finished)
                return;

            var dragonXform = Transform(dragonuid);
            var dragonPos = dragonXform.MapPosition;

            var breathDirection = (args.Target.Position - dragonPos.Position).Normalized;

            _breathAttacks.Add(dragonuid, new BreathAttack(this,
                dragonuid,
                dragonPos,
                (int)component.BreatheFireAction.Range,
                breathDirection.ToWorldAngle(),
                TimeSpan.FromMilliseconds(150),
                component.BreathDamage,
                EntityManager,
                _mapManager));

            args.Handled = true;

            if(component.SoundBreathFire != null)
                SoundSystem.Play(component.SoundBreathFire.GetSound(), Filter.Pvs(args.Performer, 4f, EntityManager), dragonuid, component.SoundBreathFire.Params);
        }

        public override void Update(float frameTime)
        {
            foreach (var entry in _breathAttacks)
            {
                if (entry.Value.Finished)
                    _breathAttacks.Remove(entry.Key);
                else
                    entry.Value.Update(frameTime, _gameTiming);
            }

        }

        /// <summary>
        ///     Determines whether an entity is blocking a tile or not. (whether it should prevent the line from continuing).
        /// </summary>
        /// <remarks>
        ///     Used for a variation of <see cref="TurfHelpers.IsBlockedTurf()"/> that makes use of the fact that we have
        ///     already done an entity lookup on a tile, and don't need to do so again.
        /// </remarks>
        public bool IsBlockingTurf(EntityUid uid, EntityQuery<PhysicsComponent> physicsQuery)
        {
            if (EntityManager.IsQueuedForDeletion(uid))
                return false;

            if (!physicsQuery.TryGetComponent(uid, out var physics))
                return false;

            return physics.CanCollide && physics.Hard && (physics.CollisionLayer & (int)CollisionGroup.BulletImpassable) != 0;
        }

        /// <summary>
        /// Apply damage to the entity
        /// </summary>
        public void ProcessEntity(
            EntityUid uid,
            DamageSpecifier? damage,
            EntityQuery<DamageableComponent> damageableQuery,
            EntityQuery<FlammableComponent> flammableQuery)
        {
            if (damage == null || !damageableQuery.HasComponent(uid))
                return;

            _damageableSystem.TryChangeDamage(uid, damage);

            // Ignite flammable entities that were hit?
            if (flammableQuery.TryGetComponent(uid, out var flammable))
            {
                _flammableSystem.AdjustFireStacks(uid, 2, flammable);
                _flammableSystem.Ignite(uid, flammable);
            }
        }

        public void ProcessTile(Vector2i tile, IMapGrid grid, string effectPrototype)
        {
            _atmosphereSystem.HotspotExpose(grid.GridEntityId, tile, 700f, 50f, true);

            var coords = grid.GridTileToLocal(tile);
            Spawn(effectPrototype, coords);
        }

        /// <summary>
        ///     Looks for the largest grid near the start of the breath attack to use as the basis of the tile coordinates.
        /// </summary>
        /// <param name="position">Origin position of the breath attack.</param>
        /// <param name="distance">Number of tiles the attack will propagate.</param>
        /// <param name="physicsQuery"></param>
        /// <returns></returns>
        public EntityUid? getReferenceGrid(MapCoordinates position, int distance, EntityQuery<PhysicsComponent> physicsQuery)
        {
            EntityUid? referenceGrid = null;
            var mass = 0f;

            var boxSize = distance * 2;
            var box = Box2.CenteredAround(position.Position, (boxSize, boxSize));

            foreach (var grid in _mapManager.FindGridsIntersecting(position.MapId, box))
            {
                if (physicsQuery.TryGetComponent(grid.GridEntityId, out var physics) &&
                    physics.Mass > mass)
                {
                    mass = physics.Mass;
                    referenceGrid = grid.GridEntityId;
                }
            }

            return referenceGrid;
        }

        private sealed class DragonDevourComplete : EntityEventArgs
        {
            public EntityUid User { get; }
            public EntityUid Target { get; }

            public DragonDevourComplete(EntityUid user, EntityUid target)
            {
                User = user;
                Target = target;
            }
        }

        private sealed class DragonStructureDevourComplete : EntityEventArgs
        {
            public EntityUid User { get; }
            public EntityUid Target { get; }

            public DragonStructureDevourComplete(EntityUid user, EntityUid target)
            {
                 User = user;
                 Target = target;
            }
        }

        private sealed class DragonDevourCancelledEvent : EntityEventArgs {}
    }
}

internal sealed class BreathAttack
{
    /// <summary>
    ///     Used to avoid applying damage to the same entity multiple times.
    /// </summary>
    private readonly HashSet<EntityUid> _processedEntities = new();

    /// <summary>
    ///     Tiles that have been processed.
    /// </summary>
    private readonly List<Vector2i> _tileProcessedList = new();


    /// <summary>
    ///     The current tile position awaiting processing or being processed.
    /// </summary>
    private Vector2i _currentTile = default!;

    /// <summary>
    ///     Track the point offset within the tile, relative to the tile's center.
    /// </summary>
    private Vector2 _currentOffset = Vector2.Zero;

    private readonly IEnumerator<Vector2i> _tileMover;

    /// <summary>
    ///     Delay between each tile propagation.
    /// </summary>
    private readonly TimeSpan _delay;

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    private TimeSpan _lastTimeProcessed;

    /// <summary>
    ///     The range of the breath attack in "tiles".
    /// </summary>
    private readonly int _range;

    /// <summary>
    ///     The direction the attack should go, as a normalized vector.
    /// </summary>
    private readonly Vector2 _direction;

    /// <summary>
    ///
    /// </summary>
    private readonly DamageSpecifier _damageSpecifier;

    /// <summary>
    ///     The current grid that is being processed.
    /// </summary>
    private readonly IMapGrid? _referenceGrid;

    /// <summary>
    ///     "Tile-size" for space when there are no nearby grids to use as a reference.
    /// </summary>
    private const ushort DefaultTileSize = 1;

    public bool Finished { get; private set; }

    // Entity Queries
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<PhysicsComponent> _physicsQuery;
    private readonly EntityQuery<DamageableComponent> _damageQuery;
    private readonly EntityQuery<MobStateComponent> _mobStateQuery;

    private readonly IEntityManager _entMan;
    private readonly DragonSystem _system;
    private readonly EntityQuery<FlammableComponent> _flammableQuery;

    public BreathAttack(DragonSystem system,
        EntityUid creator,
        MapCoordinates origin,
        int range,
        Angle dir,
        TimeSpan delay,
        DamageSpecifier damageSpec,
        IEntityManager entMan,
        IMapManager mapMan)
    {
        _system = system;
        _entMan = entMan;

        _xformQuery = _entMan.GetEntityQuery<TransformComponent>();
        _physicsQuery = _entMan.GetEntityQuery<PhysicsComponent>();
        _damageQuery = _entMan.GetEntityQuery<DamageableComponent>();
        _mobStateQuery = _entMan.GetEntityQuery<MobStateComponent>();
        _flammableQuery = _entMan.GetEntityQuery<FlammableComponent>();

        _delay = delay;
        _range = range;
        _damageSpecifier = damageSpec;

        Finished = false;

        // Add the initiator of the breath attack to processed entities to prevent self-attacking.
        if(creator.Valid)
            _processedEntities.Add(creator);

        var creatorXform = _xformQuery.GetComponent(creator);

        if (creatorXform.GridUid != null)
            _referenceGrid = mapMan.GetGrid(creatorXform.GridUid.Value);
        else
        {
            var refGridUid = _system.getReferenceGrid(origin, range, _physicsQuery);
            if (refGridUid != null)
                _referenceGrid = mapMan.GetGrid(refGridUid.Value);
        }



        // Check if the attack is starting from a grid.
        var tileSize = _referenceGrid?.TileSize ?? DefaultTileSize;

        if (_referenceGrid != null)
        {
            var angle = dir - _referenceGrid.WorldRotation;
            _direction = angle.ToWorldVec();
        }

        var originTile = _referenceGrid?.CoordinatesToTile(creatorXform.Coordinates) ?? Vector2i.Zero;

        _tileMover = TileMover(originTile, _direction).GetEnumerator();
        _tileMover.MoveNext();
        _currentTile = _tileMover.Current;
    }

    /// <summary>
    ///    Handle the tile and any entities inside of it.
    /// </summary>
    private void ProcessTile(Vector2i tile, IMapGrid grid)
    {
        var tileBox = new Box2(tile * grid.TileSize, (tile + 1) * grid.TileSize);
        var lookup = _entMan.GetComponent<EntityLookupComponent>(grid.GridEntityId);

        // Get the entities on the tile.
        List<TransformComponent> list = new();

        var state = (list, ProcessedEntities: _processedEntities, _xformQuery, _mobStateQuery);
        lookup.Tree.QueryAabb(ref state, GridQueryCallback, tileBox);

        // Process the entities in the tile.
        foreach(var xform in list)
            _system.ProcessEntity(xform.Owner, _damageSpecifier, _damageQuery, _flammableQuery);

        _system.ProcessTile(tile, grid, "FireBreathEffect");
    }

    /// <summary>
    /// Callback function to check if an entity should have further processing performed.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="uid"></param>
    /// <returns></returns>
    private static bool GridQueryCallback(
        ref (List<TransformComponent> List, HashSet<EntityUid> Processed, EntityQuery<TransformComponent> XformQuery, EntityQuery<MobStateComponent> mobStateQuery) state,
        in EntityUid uid)
    {
        // Add entities with transform components to the list of entities to process.
        if (state.Processed.Add(uid) &&
            state.mobStateQuery.HasComponent(uid) &&
            state.XformQuery.TryGetComponent(uid, out var xform))
        {
            state.List.Add(xform);
        }

        return true;
    }

    public void Update(float frameTime, IGameTiming gameTiming)
    {
        if (_referenceGrid == null || _tileProcessedList.Count >= _range)
        {
            // TODO: Add support for gridless breath attacks.
            Finished = true;
            return;
        }

        if (_tileProcessedList.Contains(_currentTile))
        {
            Finished = true;
            return; // Already added this tile. Something went wrong.
        }

        // Check if this movement has intersected with any collidable.
        var anchored = _referenceGrid.GetAnchoredEntities(_currentTile).ToList();

        var blocked = false;
        foreach (var a in anchored)
        {
            _processedEntities.Add(a);
            blocked |= _system.IsBlockingTurf(a, _physicsQuery);
        }

        if (blocked)
        {
            Finished = true;
            return;
        }

        // Check if it's time to process the tile.
        if (gameTiming.CurTime < _lastTimeProcessed + _delay)
            return;

        _lastTimeProcessed = gameTiming.CurTime;

        ProcessTile(_currentTile, _referenceGrid);
        _tileProcessedList.Add(_currentTile);

        // Proceed to the next tile.
        if (!_tileMover.MoveNext())
        {
            Finished = true;
            return;
        }

        _currentTile = _tileMover.Current;
    }

    /// <summary>
    /// Produces tile coordinates to form a line following a direction vector.
    /// </summary>
    /// <param name="start">The starting tile</param>
    /// <param name="dir">The direction to move in.</param>
    /// <returns></returns>
    private static IEnumerable<Vector2i> TileMover(Vector2i start, Vector2 dir)
    {
        var current = start;
        var nx = Math.Abs(dir.X);
        var ny = Math.Abs(dir.Y);
        var signX = dir.X > 0 ? 1 : -1;
        var signY = dir.Y > 0 ? 1 : -1;

        int ix = 0, iy = 0;
        while(true)
        {
            if ((1 + 2 * ix) * ny < (1 + 2 * iy) * nx)
            {
                current.X += signX;
                ix++;
            }
            else
            {
                current.Y += signY;
                iy++;
            }

            yield return current;
        }
    }
}
