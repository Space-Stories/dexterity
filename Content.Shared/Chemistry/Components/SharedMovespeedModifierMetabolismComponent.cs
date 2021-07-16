using Content.Shared.Movement.Components;
using Content.Shared.NetIDs;
using Robust.Shared.GameObjects;
using Robust.Shared.Players;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;
using System;
using System.Threading;

namespace Content.Shared.Chemistry.Components
{
    //TODO: refactor movement modifier component because this is a pretty poor solution
    public class SharedMovespeedModifierMetabolismComponent : Component
    {
        [ViewVariables]
        public override string Name => "MovespeedModifierMetabolismComponent";

        [ViewVariables]
        public float WalkSpeedModifier { get; set; }

        [ViewVariables]
        public float SprintSpeedModifier { get; set; }

        [ViewVariables]
        public int EffectTime { get; set; }

        private CancellationTokenSource? _cancellation;

        public void ResetModifiers()
        {
            WalkSpeedModifier = 1;
            SprintSpeedModifier = 1;
            var movement = Owner.GetComponent<MovementSpeedModifierComponent>();
            movement.RefreshMovementSpeedModifiers();
            _cancellation?.Cancel();
            Dirty();
        }
        public void ResetTimer()
        {
            _cancellation?.Cancel();
            _cancellation = new CancellationTokenSource();
            Owner.SpawnTimer(EffectTime, ResetModifiers, _cancellation.Token);
        }

        public override ComponentState GetComponentState(ICommonSession player)
        {
            return new MovespeedModifierMetabolismComponentState(WalkSpeedModifier, SprintSpeedModifier);
        }

        [Serializable, NetSerializable]
        public class MovespeedModifierMetabolismComponentState : ComponentState
        {
            public float WalkSpeedModifier { get; }
            public float SprintSpeedModifier { get; }
            public MovespeedModifierMetabolismComponentState(float walkSpeedModifier, float sprintSpeedModifier) : base(ContentNetIDs.METABOLISM_SPEEDCHANGE)
            {
                WalkSpeedModifier = walkSpeedModifier;
                SprintSpeedModifier = sprintSpeedModifier;
            }
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (curState is not MovespeedModifierMetabolismComponentState state)
            {
                return;
            }

            WalkSpeedModifier = state.WalkSpeedModifier;
            SprintSpeedModifier = state.SprintSpeedModifier;

            _cancellation = new CancellationTokenSource();

            Owner.SpawnTimer(EffectTime, ResetModifiers, _cancellation.Token);

            Owner.TryGetComponent(out MovementSpeedModifierComponent? movement);
            movement?.RefreshMovementSpeedModifiers();

        }
    }
}

