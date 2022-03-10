using Content.Shared.Disease;
using Content.Shared.Chemistry.Reagent;
using Content.Server.Disease;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using JetBrains.Annotations;

namespace Content.Server.Chemistry.ReagentEffects
{
    /// <summary>
    /// Default metabolism for medicine reagents.
    /// </summary>
    [UsedImplicitly]
    public sealed class ChemCureDisease : ReagentEffect
    {
        /// <summary>
        /// Chance it has each tick to cure the disease, between 0 and 1
        /// </summary>
        [DataField("cureChance")]
        public float CureChance = 0.1f;

        [DataField("targetSpecificDisease")]
        public bool TargetSpecificDisease = false;

        [DataField("specificDisease", customTypeSerializer: typeof(PrototypeIdSerializer<DiseasePrototype>))]
        [ViewVariables(VVAccess.ReadWrite)]
        public string? Disease = null;

        public override void Effect(ReagentEffectArgs args)
        {
            var ev = new CureDiseaseAttemptEvent(TargetSpecificDisease, Disease, CureChance);
            args.EntityManager.EventBus.RaiseLocalEvent(args.SolutionEntity, ev, false);
        }
    }
}
