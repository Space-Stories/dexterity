﻿using System.Linq;
using System.Threading;
using Content.Server.DoAfter;
using Content.Server.Lock;
using Content.Server.Mind.Components;
using Content.Server.Resist;
using Content.Server.Station.Components;
using Content.Server.Storage.Components;
using Content.Server.Tools.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Coordinates;
using Robust.Shared.Random;

namespace Content.Server.Storage.EntitySystems;

public sealed class BluespaceLockerSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly WeldableSystem _weldableSystem = default!;
    [Dependency] private readonly LockSystem _lockSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceLockerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BluespaceLockerComponent, StorageBeforeOpenEvent>(PreOpen);
        SubscribeLocalEvent<BluespaceLockerComponent, StorageAfterCloseEvent>(PostClose);
        SubscribeLocalEvent<BluespaceLockerComponent, BluespaceLockerTeleportDelayComplete>(OnBluespaceLockerTeleportDelayComplete);
    }

    private void OnStartup(EntityUid uid, BluespaceLockerComponent component, ComponentStartup args)
    {
        GetTargetStorage(component);

        if (component.BluespaceEffectOnInit)
            BluespaceEffect(uid, component);
    }

    private void BluespaceEffect(EntityUid uid, BluespaceLockerComponent component)
    {
        _entityManager.SpawnEntity(component.BluespaceEffectPrototype, uid.ToCoordinates());
        // TODO make the spawned entity follow the locker
    }

    private void PreOpen(EntityUid uid, BluespaceLockerComponent component, StorageBeforeOpenEvent args)
    {
        EntityStorageComponent? entityStorageComponent = null;

        if (!Resolve(uid, ref entityStorageComponent))
            return;

        component.CancelToken?.Cancel();

        // Select target
        var targetContainerStorageComponent = GetTargetStorage(component);
        if (targetContainerStorageComponent == null)
            return;
        BluespaceLockerComponent? targetContainerBluespaceComponent = null;

        // Close target if it is open
        if (targetContainerStorageComponent.Open)
            _entityStorage.CloseStorage(targetContainerStorageComponent.Owner, targetContainerStorageComponent);

        // Apply bluespace effects if target is not a bluespace locker, otherwise let it handle it
        if (!Resolve(targetContainerStorageComponent.Owner, ref targetContainerBluespaceComponent, false))
        {
            // Move contained items
            if (component.TransportEntities || component.TransportSentient)
                foreach (var entity in targetContainerStorageComponent.Contents.ContainedEntities.ToArray())
                {
                    if (EntityManager.HasComponent<MindComponent>(entity))
                    {
                        if (component.TransportSentient)
                            entityStorageComponent.Contents.Insert(entity, EntityManager);
                    }
                    else if (component.TransportEntities)
                        entityStorageComponent.Contents.Insert(entity, EntityManager);
                }

            // Move contained air
            if (component.TransportGas)
            {
                entityStorageComponent.Air.CopyFromMutable(targetContainerStorageComponent.Air);
                targetContainerStorageComponent.Air.Clear();
            }

            // Bluespace effects
            if (component.BluespaceEffectOnTeleportSource)
                BluespaceEffect(targetContainerStorageComponent.Owner, component);
            if (component.BluespaceEffectOnTeleportTarget)
                BluespaceEffect(uid, component);
        }
    }

    private bool ValidLink(BluespaceLockerComponent component, EntityUid link)
    {
        return link.Valid && TryComp<EntityStorageComponent>(link, out var linkStorage) && linkStorage.LifeStage != ComponentLifeStage.Deleted;
    }

    private bool ValidAutolink(BluespaceLockerComponent component, EntityUid link)
    {
        if (!ValidLink(component, link))
            return false;

        if (component.PickLinksFromSameMap &&
            link.ToCoordinates().GetMapId(_entityManager) == component.Owner.ToCoordinates().GetMapId(_entityManager))
            return false;

        if (component.PickLinksFromStationGrids &&
            !_entityManager.HasComponent<StationMemberComponent>(link.ToCoordinates().GetGridUid(_entityManager)))
            return false;

        if (component.PickLinksFromResistLockers &&
            !_entityManager.HasComponent<ResistLockerComponent>(link))
            return false;

        if (component.PickLinksFromSameAccess &&
            (_entityManager.TryGetComponent<AccessComponent>(component.Owner, out var sourceAccess) != _entityManager.TryGetComponent<AccessComponent>(link, out var targetAccess) ||
            (sourceAccess != null && !sourceAccess.Tags.SetEquals(targetAccess!.Tags))))
            return false;

        return true;
    }

    private EntityStorageComponent? GetTargetStorage(BluespaceLockerComponent component)
    {
        while (true)
        {
            // Ensure MinBluespaceLinks
            if (component.BluespaceLinks.Count < component.MinBluespaceLinks)
            {
                // Get an shuffle the list of all EntityStorages
                var storages = _entityManager.EntityQuery<EntityStorageComponent>().ToArray();
                _robustRandom.Shuffle(storages);

                // Add valid candidates till MinBluespaceLinks is met
                foreach (var storage in storages)
                {
                    var potentialLink = storage.Owner;

                    if (!ValidAutolink(component, potentialLink))
                        continue;

                    component.BluespaceLinks.Add(potentialLink);
                    if (component.AutoLinksBidirectional)
                    {
                        _entityManager.EnsureComponent<BluespaceLockerComponent>(storage.Owner, out var targetBluespaceComponent);
                        targetBluespaceComponent.BluespaceLinks.Add(component.Owner);
                    }
                    if (component.BluespaceLinks.Count >= component.MinBluespaceLinks)
                        break;
                }
            }

            // If there are no possible link targets and no links, return null
            if (component.BluespaceLinks.Count == 0)
                return null;

            // Attempt to select, validate, and return a link
            var links = component.BluespaceLinks.ToArray();
            var link = links[_robustRandom.Next(0, component.BluespaceLinks.Count)];
            if (ValidLink(component, link))
                return Comp<EntityStorageComponent>(link);
            component.BluespaceLinks.Remove(link);
        }
    }

    private void PostClose(EntityUid uid, BluespaceLockerComponent component, StorageAfterCloseEvent args)
    {
        PostClose(uid, component);
    }

    private void OnBluespaceLockerTeleportDelayComplete(EntityUid uid, BluespaceLockerComponent component, BluespaceLockerTeleportDelayComplete args)
    {
        PostClose(uid, component, false);
    }

    private void PostClose(EntityUid uid, BluespaceLockerComponent component, bool doDelay = true)
    {
        EntityStorageComponent? entityStorageComponent = null;

        if (!Resolve(uid, ref entityStorageComponent))
            return;

        component.CancelToken?.Cancel();

        // Do delay
        if (doDelay && component.Delay > 0)
        {
            component.CancelToken = new CancellationTokenSource();

            _doAfterSystem.DoAfter(
                new DoAfterEventArgs(uid, component.Delay, component.CancelToken.Token)
                {
                    UserFinishedEvent = new BluespaceLockerTeleportDelayComplete()
                });
            return;
        }

        // Select target
        var targetContainerStorageComponent = GetTargetStorage(component);
        if (targetContainerStorageComponent == null)
            return;

        // Move contained items
        if (component.TransportEntities || component.TransportSentient)
            foreach (var entity in entityStorageComponent.Contents.ContainedEntities.ToArray())
            {
                if (EntityManager.HasComponent<MindComponent>(entity))
                {
                    if (component.TransportSentient)
                        targetContainerStorageComponent.Contents.Insert(entity, EntityManager);
                }
                else if (component.TransportEntities)
                    targetContainerStorageComponent.Contents.Insert(entity, EntityManager);
            }

        // Move contained air
        if (component.TransportGas)
        {
            targetContainerStorageComponent.Air.CopyFromMutable(entityStorageComponent.Air);
            entityStorageComponent.Air.Clear();
        }

        // Open and empty target
        if (targetContainerStorageComponent.Open)
        {
            _entityStorage.EmptyContents(targetContainerStorageComponent.Owner, targetContainerStorageComponent);
            _entityStorage.ReleaseGas(targetContainerStorageComponent.Owner, targetContainerStorageComponent);
        }
        else
        {
            if (targetContainerStorageComponent.IsWeldedShut)
            {
                // It gets bluespaced open...
                _weldableSystem.ForceWeldedState(targetContainerStorageComponent.Owner, false);
                if (targetContainerStorageComponent.IsWeldedShut)
                    targetContainerStorageComponent.IsWeldedShut = false;
            }
            LockComponent? lockComponent = null;
            if (Resolve(targetContainerStorageComponent.Owner, ref lockComponent, false) && lockComponent.Locked)
                _lockSystem.Unlock(lockComponent.Owner, lockComponent.Owner, lockComponent);

            _entityStorage.OpenStorage(targetContainerStorageComponent.Owner, targetContainerStorageComponent);
        }

        // Bluespace effects
        if (component.BluespaceEffectOnTeleportSource)
            BluespaceEffect(uid, component);
        if (component.BluespaceEffectOnTeleportTarget)
            BluespaceEffect(targetContainerStorageComponent.Owner, component);
    }

    private sealed class BluespaceLockerTeleportDelayComplete : EntityEventArgs
    {
    }
}
