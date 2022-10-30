using Content.Shared.Singularity;
using Content.Shared.Singularity.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Singularity.Components
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedSingularityComponent))]
    public sealed class ServerSingularityComponent : SharedSingularityComponent
    {
        [Dependency] private readonly IEntityManager _entMan = default!;

        private SharedSingularitySystem _singularitySystem = default!;

        [ViewVariables(VVAccess.ReadWrite)]
        public int Energy
        {
            get => _energy;
            set
            {
                if (value == _energy) return;

                _energy = value;
                if (_energy <= 0)
                {
                    _entMan.DeleteEntity(Owner);
                    return;
                }

                var level = _energy switch
                {
                    >= 1500 => 6,
                    >= 1000 => 5,
                    >= 600 => 4,
                    >= 300 => 3,
                    >= 200 => 2,
                    < 200 => 1
                };
                _singularitySystem.ChangeSingularityLevel(this, level);
            }
        }
        private int _energy = 180;

        [ViewVariables]
        public int EnergyDrain =>
            Level switch
            {
                6 => 20,
                5 => 15,
                4 => 10,
                3 => 5,
                2 => 2,
                1 => 1,
                _ => 0
            };

        [DataField("moveAccumulator")]
        public float MoveAccumulator;

        // This is an interesting little workaround.
        // See, two singularities queuing deletion of each other at the same time will annihilate.
        // This is undesirable behaviour, so this flag allows the imperatively first one processed to take priority.
        [ViewVariables(VVAccess.ReadWrite)]
        public bool BeingDeletedByAnotherSingularity { get; set; }

        [DataField("singularityFormingSound")] private SoundSpecifier _singularityFormingSound = new SoundPathSpecifier("/Audio/Effects/singularity_form.ogg");
        [DataField("singularityCollapsingSound")] private SoundSpecifier _singularityCollapsingSound = new SoundPathSpecifier("/Audio/Effects/singularity_collapse.ogg");

        public override ComponentState GetComponentState()
        {
            return new SingularityComponentState(Level);
        }

        protected override void Initialize()
        {
            base.Initialize();

            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            _singularitySystem = sysMan.GetEntitySystem<SharedSingularitySystem>();

            var audioSystem = sysMan.GetEntitySystem<SharedAudioSystem>();
            var audioParams = AudioParams.Default;
            audioParams.Loop = true;
            audioParams.MaxDistance = 20f;
            audioParams.Volume = 5;
            audioSystem.Play(_singularityFormingSound, Filter.Pvs(Owner), Owner);

            _singularitySystem.ChangeSingularityLevel(this, 1);
        }

        protected override void Shutdown()
        {
            base.Shutdown();
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SharedAudioSystem>()
                .Play(_singularityCollapsingSound, Filter.Pvs(Owner), _entMan.GetComponent<TransformComponent>(Owner).Coordinates);
        }
    }
}
