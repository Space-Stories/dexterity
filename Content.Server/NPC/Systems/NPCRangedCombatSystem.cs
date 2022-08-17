using Content.Server.NPC.Components;
using Content.Server.Weapon.Ranged.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;

namespace Content.Server.NPC.Systems;

public sealed class NPCRangedCombatSystem : EntitySystem
{
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly RotateToFaceSystem _face = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var xformQuery = GetEntityQuery<TransformComponent>();
        var combatQuery = GetEntityQuery<SharedCombatModeComponent>();

        foreach (var (comp, xform) in EntityQuery<NPCRangedCombatComponent, TransformComponent>())
        {
            if (!xformQuery.TryGetComponent(comp.Target, out var targetXform))
            {
                continue;
            }

            if (targetXform.MapID != xform.MapID)
            {
                comp.Status = CombatStatus.TargetUnreachable;
                continue;
            }

            if (combatQuery.TryGetComponent(comp.Owner, out var combatMode))
            {
                combatMode.IsInCombatMode = true;
            }

            var gun = _gun.GetGun(comp.Owner);

            if (gun == null)
            {
                comp.Status = CombatStatus.NoWeapon;
                continue;
            }

            // TODO: LOS
            // TODO: Ammo checks
            // TODO: Burst fire
            // TODO: Cycling
            // Max rotation speed
            _face.TryFaceCoordinates(comp.Owner, _transform.GetWorldPosition(targetXform, xformQuery));

            _gun.AttemptShoot(comp.Owner, gun, targetXform.Coordinates);
        }
    }
}
