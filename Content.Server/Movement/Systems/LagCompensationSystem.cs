using Content.Server.Movement.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Movement.Systems;

/// <summary>
/// Stores a buffer of previous positions of the relevant entity.
/// Can be used to check the entity's position at a recent point in time.
/// </summary>
public sealed class LagCompensationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public static readonly TimeSpan BufferTime = TimeSpan.FromMilliseconds(500);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LagCompensationComponent, MoveEvent>(OnLagMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var earliestTime = curTime - BufferTime;

        // Cull any old ones from active updates
        // Probably fine to include ignored.
        foreach (var (_, comp) in EntityQuery<ActiveLagCompensationComponent, LagCompensationComponent>(true))
        {
            var invalid = true;

            while (comp.Positions.TryPeek(out var pos))
            {
                if (pos.Item1 < earliestTime)
                {
                    comp.Positions.Dequeue();
                    continue;
                }

                break;
            }

            if (comp.Positions.Count == 0)
            {
                RemComp<ActiveLagCompensationComponent>(comp.Owner);
            }
        }
    }

    private void OnLagMove(EntityUid uid, LagCompensationComponent component, ref MoveEvent args)
    {
        EnsureComp<ActiveLagCompensationComponent>(uid);
        component.Positions.Enqueue((_timing.CurTime, args.NewPosition, args.NewRotation));
    }

    public (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(EntityUid uid, IPlayerSession pSession,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return (EntityCoordinates.Invalid, Angle.Zero);

        var angle = Angle.Zero;
        var coordinates = EntityCoordinates.Invalid;
        var ping = pSession.Ping;
        var sentTime = _timing.CurTime - TimeSpan.FromMilliseconds(ping);

        if (!TryComp<LagCompensationComponent>(uid, out var lag) || lag.Positions.Count == 0)
            return (xform.Coordinates, xform.LocalRotation);

        foreach (var pos in lag.Positions)
        {
            coordinates = pos.Item2;
            angle = pos.Item3;

            if (pos.Item1 >= sentTime)
                break;
        }

        if (coordinates == default)
        {
            coordinates = xform.Coordinates;
            angle = xform.LocalRotation;
        }

        return (coordinates, angle);
    }

    public Angle GetAngle(EntityUid uid, IPlayerSession pSession, TransformComponent? xform = null)
    {
        var (_, angle) = GetCoordinatesAngle(uid, pSession, xform);
        return angle;
    }

    public EntityCoordinates GetCoordinates(EntityUid uid, IPlayerSession pSession, TransformComponent? xform = null)
    {
        var (coordinates, _) = GetCoordinatesAngle(uid, pSession, xform);
        return coordinates;
    }
}
