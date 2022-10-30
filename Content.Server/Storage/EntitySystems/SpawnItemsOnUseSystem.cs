using Content.Server.Storage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Storage;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Storage.EntitySystems
{
    public sealed class SpawnItemsOnUseSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpawnItemsOnUseComponent, UseInHandEvent>(OnUseInHand);
        }

        private void OnUseInHand(EntityUid uid, SpawnItemsOnUseComponent component, UseInHandEvent args)
        {
            if (args.Handled)
                return;

            var coords = Transform(args.User).Coordinates;
            var spawnEntities = EntitySpawnCollection.GetSpawns(component.Items, _random);
            EntityUid? entityToPlaceInHands = null;

            foreach (var proto in spawnEntities)
            {
                entityToPlaceInHands = Spawn(proto, coords);
            }

            if (component.Sound != null)
                _audioSystem.Play(component.Sound, Filter.Pvs(uid), uid);

            component.Uses--;
            if (component.Uses == 0)
            {
                args.Handled = true;
                EntityManager.DeleteEntity(uid);
            }

            if (entityToPlaceInHands != null)
            {
                _handsSystem.PickupOrDrop(args.User, entityToPlaceInHands.Value);
            }
        }
    }
}
