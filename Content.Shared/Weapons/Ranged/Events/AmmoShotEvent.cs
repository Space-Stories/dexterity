namespace Content.Shared.Weapons.Ranged;

/// <summary>
/// Raised when projectiles have been fired from a gun.
/// </summary>
public sealed class AmmoShotEvent : EntityEventArgs
{
    public List<EntityUid> FiredProjectiles = default!;
}
