﻿#nullable enable
using Content.Server.Observer;
using Content.Shared.GameObjects.Components.Body;
using Content.Shared.GameObjects.Components.Body.Part;
using Content.Shared.GameObjects.Components.Damage;
using Content.Shared.GameObjects.Components.Movement;
using Robust.Server.Interfaces.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Players;

namespace Content.Server.GameObjects.Components.Body
{
    /// <summary>
    ///     Component representing a collection of <see cref="IBodyPart"></see>
    ///     attached to each other.
    /// </summary>
    [RegisterComponent]
    [ComponentReference(typeof(SharedBodyComponent))]
    [ComponentReference(typeof(DamageableComponent))]
    [ComponentReference(typeof(IDamageableComponent))]
    [ComponentReference(typeof(IBody))]
    public class BodyComponent : SharedBodyComponent, IRelayMoveInput
    {
        protected override void Startup()
        {
            base.Startup();

            // This is ran in Startup as entities spawned in Initialize
            // are not synced to the client since they are assumed to be
            // identical on it
            foreach (var (slot, partId) in PartIds)
            {
                // Using MapPosition instead of Coordinates here prevents
                // a crash within the character preview menu in the lobby
                var part = Owner.EntityManager.SpawnEntity(partId, Owner.Transform.MapPosition);
                var partComponent = part.GetComponent<IBodyPart>();

                TryAddPart(slot, partComponent, true);
            }
        }

        void IRelayMoveInput.MoveInputPressed(ICommonSession session)
        {
            if (CurrentDamageState == DamageState.Dead)
            {
                new Ghost().Execute(null, (IPlayerSession) session, null);
            }
        }
    }
}
