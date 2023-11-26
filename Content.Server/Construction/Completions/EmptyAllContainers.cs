using Content.Server.Hands.Systems;
using Content.Shared.Construction;
using Content.Shared.Hands.Components;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Construction.Completions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class EmptyAllContainers : IGraphAction
    {
        /// <summary>
        ///     Whether or not the user should attempt to pick up the removed entities.
        /// </summary>
        [DataField]
        public bool Pickup = false;

        /// <summary>
        ///    Whether or not to empty the container at the user's location.
        /// </summary>
        [DataField]
        public bool EmptyAtUser = false;

        public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
        {
            if (!entityManager.TryGetComponent(uid, out ContainerManagerComponent? containerManager))
                return;

            var containerSys = entityManager.EntitySysManager.GetEntitySystem<ContainerSystem>();
            var handSys = entityManager.EntitySysManager.GetEntitySystem<HandsSystem>();

            HandsComponent? hands = null;
            var pickup = Pickup && entityManager.TryGetComponent(userUid, out hands);

            EntityCoordinates? dropCoordinates = null;
            if (EmptyAtUser && entityManager.TryGetComponent(userUid, out TransformComponent? userXform))
                dropCoordinates = userXform.Coordinates;

            foreach (var container in containerManager.GetAllContainers())
            {
                foreach (var ent in containerSys.EmptyContainer(container, true, dropCoordinates, reparent: !pickup))
                {
                    if (pickup)
                        handSys.PickupOrDrop(userUid, ent, handsComp: hands);
                }
            }
        }
    }
}
