using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Content.Shared.Chemistry.Metabolizable;
using Content.Shared.Movement.Components;
using Content.Shared.Chemistry.Components;

namespace Content.Server.Chemistry.Metabolism
{
    /// <summary>
    /// Default metabolism for stimulants and tranqs. Attempts to find a MovementSpeedModifier on the target,
    /// adding one if not there and to change the movespeed
    /// </summary>
    [DataDefinition]
    public class MovespeedModifierMetabolism : IMetabolizable
    {
        /// <summary>
        /// How much of the reagent should be metabolized each sec.
        /// </summary>
        [DataField("rate")]
        public ReagentUnit MetabolismRate { get; set; } = ReagentUnit.New(1);

        /// <summary>
        /// How much the entities' walk speed is multiplied by.
        /// </summary>
        [DataField("walkSpeedModifier")]
        public float WalkSpeedModifier { get; set; } = 1;

        /// <summary>
        /// How much the entities' run speed is multiplied by.
        /// </summary>
        [DataField("sprintSpeedModifier")]
        public float SprintSpeedModifier { get; set; } = 1;

        /// <summary>
        /// how long the modifier persist after the final unit of reagent is metabolised,
        /// should really be longer than however long it takes for a metabolism tick(1 second).
        /// </summary>
        [DataField("statusLifetime")]
        public int StatusLifetime = 1500;

        /// <summary>
        /// Remove reagent at set rate, changes the movespeed modifiers and adds a MovespeedModifierMetabolismComponent if not already there.
        /// </summary>
        /// <param name="solutionEntity"></param>
        /// <param name="reagentId"></param>
        /// <param name="tickTime"></param>
        /// <returns></returns>
        ReagentUnit IMetabolizable.Metabolize(IEntity solutionEntity, string reagentId, float tickTime)
        {
            if (solutionEntity.TryGetComponent(out MovementSpeedModifierComponent? movement))
            {
                solutionEntity.EnsureComponent(out MovespeedModifierMetabolismComponent status);

                status.WalkSpeedModifier = WalkSpeedModifier;
                status.SprintSpeedModifier = SprintSpeedModifier;
                status.EffectTime = StatusLifetime * MetabolismRate.Int();
                status.ResetTimer();

                //If any of the modifers aren't synced to the movement modifier component, then refresh them, otherwise don't
                //Also I don't know if this is a good way to do a NAND gate in c#
                if (!(status.WalkSpeedModifier.Equals(WalkSpeedModifier) && status.SprintSpeedModifier.Equals(SprintSpeedModifier)))
                    movement?.RefreshMovementSpeedModifiers();
                
                status.Dirty();    
            }
            return MetabolismRate;
        }
    }
}
