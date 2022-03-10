using Content.Shared.Disease;
using Content.Shared.Damage;
using JetBrains.Annotations;

namespace Content.Server.Disease.Effects
{
    [UsedImplicitly]
    public sealed class DiseaseHealthChange : DiseaseEffect
    {
        [DataField("damage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier Damage = default!;
        public override void Effect(DiseaseEffectArgs args)
        {
            EntitySystem.Get<DamageableSystem>().TryChangeDamage(args.DiseasedEntity, Damage);
        }

    }
}
