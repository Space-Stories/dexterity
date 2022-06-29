using System.Linq;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Popups;
using Content.Server.Storage.Components;
using Content.Server.Tools.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Placeable;
using Content.Shared.Storage;
using Robust.Server.Containers;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Player;

namespace Content.Server.Storage.EntitySystems;

public sealed class EntityStorageSystem : EntitySystem
{
    [Dependency] private readonly ConstructionSystem _construction = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PlaceableSurfaceSystem _placeableSurface = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    public const string ContainerName = "entity_storage";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityStorageComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<EntityStorageComponent, ActivateInWorldEvent>(OnInteract);
        SubscribeLocalEvent<EntityStorageComponent, WeldableAttemptEvent>(OnWeldableAttempt);
        SubscribeLocalEvent<EntityStorageComponent, WeldableChangedEvent>(OnWelded);
        SubscribeLocalEvent<EntityStorageComponent, DestructionEventArgs>(OnDestroy);
    }

    private void OnInit(EntityUid uid, EntityStorageComponent component, ComponentInit args)
    {
        component.Contents = _container.EnsureContainer<Container>(uid, ContainerName);
        component.Contents.ShowContents = component.ShowContents;
        component.Contents.OccludesLight = component.OccludesLight;

        if (TryComp<ConstructionComponent>(uid, out var construction))
            _construction.AddContainer(uid, nameof(EntityStorageComponent), construction);

        if (TryComp<PlaceableSurfaceComponent>(uid, out var placeable))
            _placeableSurface.SetPlaceable(uid, component.Open, placeable);
    }

    private void OnInteract(EntityUid uid, EntityStorageComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ToggleOpen(args.User, uid, component);
    }

    private void OnWeldableAttempt(EntityUid uid, EntityStorageComponent component, WeldableAttemptEvent args)
    {
        if (component.Open)
        {
            args.Cancel();
            return;
        }

        if (component.Contents.Contains(args.User))
        {
            var msg = Loc.GetString("entity-storage-component-already-contains-user-message");
            _popupSystem.PopupEntity(msg, args.User, Filter.Entities(args.User));
            args.Cancel();
        }
    }

    private void OnWelded(EntityUid uid, EntityStorageComponent component, WeldableChangedEvent args)
    {
        component.IsWeldedShut = args.IsWelded;
    }

    private void OnDestroy(EntityUid uid, EntityStorageComponent component, DestructionEventArgs args)
    {
        component.Open = true;
        EmptyContents(uid, component);
    }

    public void ToggleOpen(EntityUid user, EntityUid target, EntityStorageComponent? component = null)
    {
        if (!Resolve(target, ref component))
            return;

        if (component.Open)
        {
            TryCloseStorage(target);
        }
        else
        {
            TryOpenStorage(user, target);
        }
    }

    public void EmptyContents(EntityUid uid, EntityStorageComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var containedArr = component.Contents.ContainedEntities.ToArray();
        foreach (var contained in containedArr)
        {
            if (component.Contents.Remove(contained))
            {
                Transform(contained).WorldPosition = Transform(uid).WorldPosition;
                if (TryComp(contained, out IPhysBody? physics))
                {
                    physics.CanCollide = true;
                }
            }
        }
    }

    public void OpenStorage(EntityUid uid, EntityStorageComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Open = true;
        EmptyContents(uid, component);
        ModifyComponents(uid, component);
        SoundSystem.Play(component.OpenSound.GetSound(), Filter.Pvs(component.Owner), component.Owner);
    }

    public void CloseStorage(EntityUid uid, EntityStorageComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Open = false;

        var count = 0;
        foreach (var entity in _lookup.GetEntitiesInRange(uid, component.EnteringRange, LookupFlags.Approximate))
        {
            if (_container.IsEntityInContainer(entity))
                continue;

            if (!CanFit(entity, uid))
                continue;

            if (!AddToContents(entity, uid, component))
                continue;

            count++;
            if (count >= component.StorageCapacityMax)
                break;
        }

        ModifyComponents(uid, component);
        SoundSystem.Play(component.CloseSound.GetSound(), Filter.Pvs(uid), uid);
        component.LastInternalOpenAttempt = default;
    }

    public bool Insert(EntityUid toInsert, EntityUid container, EntityStorageComponent? component = null)
    {
        if (!Resolve(container, ref component))
            return false;

        if (component.Open)
            Transform(toInsert).WorldPosition = Transform(container).WorldPosition;

        return component.Contents.Insert(toInsert, EntityManager);
    }

    public bool Remove(EntityUid toRemove, EntityUid container, EntityStorageComponent? component = null)
    {
        if (!Resolve(container, ref component))
            return false;

        return component.Contents.Remove(toRemove, EntityManager);
    }

    public bool CanInsert(EntityUid toInsert, EntityUid container, EntityStorageComponent? component = null)
    {
        if (!Resolve(container, ref component))
            return false;

        if (component.Open)
            return true;

        if (component.Contents.ContainedEntities.Count >= component.StorageCapacityMax)
            return false;

        return component.Contents.CanInsert(toInsert, EntityManager);
    }

    public bool TryOpenStorage(EntityUid user, EntityUid target)
    {
        if (!CanOpen(user, target))
            return false;

        OpenStorage(target);
        return true;
    }

    public bool TryCloseStorage(EntityUid target)
    {
        if (!CanClose(target))
        {
            return false;
        }

        CloseStorage(target);
        return true;
    }

    public bool CanOpen(EntityUid user, EntityUid target, bool silent = false, EntityStorageComponent? component = null)
    {
        if (!Resolve(target, ref component))
            return false;

        if (component.IsWeldedShut)
        {
            if (!silent && !component.Contents.Contains(user))
                _popupSystem.PopupEntity(Loc.GetString("entity-storage-component-welded-shut-message"), target, Filter.Pvs(target));

            return false;
        }

        if (TryComp<LockComponent>(target, out var lockComp) && lockComp.Locked)
        {
            if (!silent)
                _popupSystem.PopupEntity(Loc.GetString("entity-storage-component-locked-message"), target, Filter.Pvs(target));

            return false;
        }

        var ev = new StorageOpenAttemptEvent();
        RaiseLocalEvent(target, ev, true);

        return !ev.Cancelled;
    }

    public bool CanClose(EntityUid target, bool silent = false)
    {
        var ev = new StorageCloseAttemptEvent();
        RaiseLocalEvent(target, ev, silent);

        return !ev.Cancelled;
    }

    public bool AddToContents(EntityUid toAdd, EntityUid container, EntityStorageComponent? component = null)
    {
        if (!Resolve(container, ref component))
            return false;

        if (toAdd == container)
            return false;

        if (TryComp<IPhysBody>(toAdd, out var phys))
            if (component.MaxSize < phys.GetWorldAABB().Size.X || component.MaxSize < phys.GetWorldAABB().Size.Y)
                return false;

        return component.Contents.CanInsert(toAdd, EntityManager) && Insert(toAdd, container, component);
    }

    public bool CanFit(EntityUid toInsert, EntityUid container)
    {
        // conditions are complicated because of pizzabox-related issues, so follow this guide
        // 0. Accomplish your goals at all costs.
        // 1. AddToContents can block anything
        // 2. maximum item count can block anything
        // 3. ghosts can NEVER be eaten
        // 4. items can always be eaten unless a previous law prevents it
        // 5. if this is NOT AN ITEM, then mobs can always be eaten unless unless a previous law prevents it
        // 6. if this is an item, then mobs must only be eaten if some other component prevents pick-up interactions while a mob is inside (e.g. foldable)
        var attemptEvent = new InsertIntoEntityStorageAttemptEvent();
        RaiseLocalEvent(toInsert, attemptEvent);
        if (attemptEvent.Cancelled)
            return false;

        // checks
        // TODO: Make the others sub to it.
        var targetIsItem = HasComp<SharedItemComponent>(toInsert);
        var targetIsMob = HasComp<SharedBodyComponent>(toInsert);
        var storageIsItem = HasComp<SharedItemComponent>(container);

        var allowedToEat = targetIsItem;

        // BEFORE REPLACING THIS WITH, I.E. A PROPERTY:
        // Make absolutely 100% sure you have worked out how to stop people ending up in backpacks.
        // Seriously, it is insanely hacky and weird to get someone out of a backpack once they end up in there.
        // And to be clear, they should NOT be in there.
        // For the record, what you need to do is empty the backpack onto a PlacableSurface (table, rack)
        if (targetIsMob)
        {
            if (!storageIsItem)
                allowedToEat = true;
            else
            {
                var storeEv = new StoreThisAttemptEvent();
                RaiseLocalEvent(container, storeEv);
                allowedToEat = !storeEv.Cancelled;
            }
        }

        return allowedToEat;
    }

    public void ModifyComponents(EntityUid uid, EntityStorageComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!component.IsCollidableWhenOpen && TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.Fixtures.Count > 0)
        {
            // currently only works for single-fixture entities. If they have more than one fixture, then
            // RemovedMasks needs to be tracked separately for each fixture, using a fixture Id Dictionary. Also the
            // fixture IDs probably cant be automatically generated without causing issues, unless there is some
            // guarantee that they will get deserialized with the same auto-generated ID when saving+loading the map.
            var fixture = fixtures.Fixtures.Values.First();

            if (component.Open)
            {
                component.RemovedMasks = fixture.CollisionLayer & component.MasksToRemove;
                fixture.CollisionLayer &= ~component.MasksToRemove;
            }
            else
            {
                fixture.CollisionLayer |= component.RemovedMasks;
                component.RemovedMasks = 0;
            }
        }

        if (TryComp<PlaceableSurfaceComponent>(uid, out var surface))
            _placeableSurface.SetPlaceable(uid, true, surface);

        if (TryComp<AppearanceComponent>(uid, out var appearance))
            appearance.SetData(StorageVisuals.Open, component.Open);
    }
}
