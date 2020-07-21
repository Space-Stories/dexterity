﻿using Content.Server.Mobs;
using Content.Shared.GameObjects.Components.Mobs;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.ViewVariables;
using Timer = Robust.Shared.Timers.Timer;
using Content.Shared.GameObjects.Components.Movement;

namespace Content.Server.GameObjects.Components.Mobs
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedStunnableComponent))]
    public class StunnableComponent : SharedStunnableComponent
    {
#pragma warning disable 649
        [Dependency] private IGameTiming _gameTiming;
#pragma warning restore 649

        [ViewVariables] public override bool Stunned => StunnedTimer > 0f;
        [ViewVariables] public override bool KnockedDown => KnockdownTimer > 0f;
        [ViewVariables] public override bool SlowedDown => SlowdownTimer > 0f;
        [ViewVariables] public float StunCap => _stunCap;
        [ViewVariables] public float KnockdownCap => _knockdownCap;
        [ViewVariables] public float SlowdownCap => _slowdownCap;

        protected override void OnStun()
        {
            StandingStateHelper.DropAllItemsInHands(Owner, false);
        }

        protected override void OnKnockdown()
        {
            StandingStateHelper.Down(Owner);
        }

        public void CancelAll()
        {
            KnockdownTimer = 0f;
            StunnedTimer = 0f;
            Dirty();
        }

        public void ResetStuns()
        {
            StunnedTimer = 0f;
            SlowdownTimer = 0f;

            if (KnockedDown)
            {
                StandingStateHelper.Standing(Owner);
            }

            KnockdownTimer = 0f;
        }

        public void Update(float delta)
        {
            if (Stunned)
            {
                StunnedTimer -= delta;

                if (StunnedTimer <= 0)
                {
                    StunnedTimer = 0f;
                    Dirty();
                }
            }

            if (KnockedDown)
            {
                KnockdownTimer -= delta;

                if (KnockdownTimer <= 0f)
                {
                    StandingStateHelper.Standing(Owner);

                    KnockdownTimer = 0f;
                    Dirty();
                }
            }

            if (SlowedDown)
            {
                SlowdownTimer -= delta;

                if (SlowdownTimer <= 0f)
                {
                    SlowdownTimer = 0f;

                    if (Owner.TryGetComponent(out MovementSpeedModifierComponent movement))
                    {
                        movement.RefreshMovementSpeedModifiers();
                    }

                    Dirty();
                }
            }

            if (!StunStart.HasValue || !StunEnd.HasValue ||
                !Owner.TryGetComponent(out ServerStatusEffectsComponent status))
            {
                return;
            }

            var start = StunStart.Value;
            var end = StunEnd.Value;

            var length = (end - start).TotalSeconds;
            var progress = (_gameTiming.CurTime - start).TotalSeconds;

            if (progress >= length)
            {
                Timer.Spawn(250, () => status.RemoveStatusEffect(StatusEffect.Stun), StatusRemoveCancellation.Token);
                LastStun = null;
            }
        }

        public override ComponentState GetComponentState()
        {
            return new StunnableComponentState(Stunned, KnockedDown, SlowedDown, WalkModifierOverride,
                RunModifierOverride);
        }
    }
}
