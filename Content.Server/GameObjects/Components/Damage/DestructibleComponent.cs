﻿#nullable enable
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Stack;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Damage;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Utility;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Damage
{
    /// <summary>
    ///     When attached to an <see cref="IEntity"/>, allows it to take damage and deletes it after taking enough damage.
    /// </summary>
    [RegisterComponent]
    public class DestructibleComponent : Component, IDestroyAct
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        private ActSystem _actSystem = default!;

        public override string Name => "Destructible";

        /// <summary>
        ///     The amount of damage at which the behavior for this component
        ///     will trigger.
        /// </summary>
        [ViewVariables]
        private int Threshold { get; set; }

        /// <summary>
        /// Entities spawned on destruction plus the min and max amount spawned.
        /// </summary>
        public Dictionary<string, MinMax>? SpawnOnDestroy { get; private set; }

        /// <summary>
        ///     Sound played upon destruction.
        /// </summary>
        [ViewVariables]
        private string DestroySound { get; set; } = string.Empty;

        /// <summary>
        /// Used instead of <see cref="DestroySound"/> if specified.
        /// </summary>
        [ViewVariables]
        private string DestroySoundCollection { get; set; } = string.Empty;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(this, d => d.Threshold, "threshold", 0);
            serializer.DataField(this, d => d.SpawnOnDestroy, "spawnOnDestroy", null);
            serializer.DataField(this, ruinable => ruinable.DestroySound, "destroySound", string.Empty);
            serializer.DataField(this, ruinable => ruinable.DestroySoundCollection, "destroySoundCollection", string.Empty);
        }

        public override void Initialize()
        {
            base.Initialize();

            _actSystem = EntitySystem.Get<ActSystem>();
        }

        public override void HandleMessage(ComponentMessage message, IComponent? component)
        {
            base.HandleMessage(message, component);

            switch (message)
            {
                case DamageChangedMessage msg:
                {
                    if (msg.Damageable.Owner != Owner)
                    {
                        break;
                    }

                    if (msg.Damageable.TotalDamage >= Threshold)
                    {
                        Destroy();
                    }

                    break;
                }
            }
        }

        /// <summary>
        ///     Destroys the Owner <see cref="IEntity"/>, playing a sound
        ///     and optionally spawning entities.
        /// </summary>
        private void Destroy()
        {
            if (Owner.Deleted)
            {
                return;
            }

            _actSystem.HandleDestruction(Owner, true);
        }

        private void PlaySound()
        {
            var pos = Owner.Transform.Coordinates;
            var sound = string.Empty;

            if (DestroySoundCollection != string.Empty)
            {
                sound = AudioHelpers.GetRandomFileFromSoundCollection(DestroySoundCollection);
            }
            else if (DestroySound != string.Empty)
            {
                sound = DestroySound;
            }

            if (sound != string.Empty)
            {
                EntitySystem.Get<AudioSystem>().PlayAtCoords(sound, pos, AudioHelpers.WithVariation(0.125f));
            }
        }

        private void DoSpawnOnDestroy(DestructionEventArgs eventArgs)
        {
            if (SpawnOnDestroy == null || !eventArgs.IsSpawnWreck)
            {
                return;
            }

            foreach (var (key, value) in SpawnOnDestroy)
            {
                var count = value.Min >= value.Max
                    ? value.Min
                    : _random.Next(value.Min, value.Max + 1);

                if (count == 0) continue;

                if (EntityPrototypeHelpers.HasComponent<StackComponent>(key))
                {
                    var spawned = Owner.EntityManager.SpawnEntity(key, Owner.Transform.Coordinates);
                    var stack = spawned.GetComponent<StackComponent>();
                    stack.Count = count;
                    spawned.RandomOffset(0.5f);
                }
                else
                {
                    for (var i = 0; i < count; i++)
                    {
                        var spawned = Owner.EntityManager.SpawnEntity(key, Owner.Transform.Coordinates);
                        spawned.RandomOffset(0.5f);
                    }
                }
            }
        }

        void IDestroyAct.OnDestroy(DestructionEventArgs eventArgs)
        {
            PlaySound();
            DoSpawnOnDestroy(eventArgs);
        }

        public struct MinMax
        {
            public int Min;
            public int Max;
        }
    }
}
