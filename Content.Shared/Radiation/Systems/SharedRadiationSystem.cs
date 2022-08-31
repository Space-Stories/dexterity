using System.Linq;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Radiation.Systems;

public abstract class SharedRadiationSystem : EntitySystem
{

}

/// <summary>
///     Radiation ray emitted by radiation source towards radiation receiver.
///     Contains all information about encountered radiation blockers.
/// </summary>
[Serializable, NetSerializable]
public sealed class RadiationRay
{
    /// <summary>
    ///     Map on which source and receiver are placed.
    /// </summary>
    public MapId MapId;
    /// <summary>
    ///     World coordinates of radiation source.
    /// </summary>
    public Vector2 Source;
    /// <summary>
    ///     World coordinates of radiation receiver.
    /// </summary>
    public Vector2 Destination;
    /// <summary>
    ///     How much rads intensity reached radiation receiver.
    /// </summary>
    public float Rads;

    /// <summary>
    ///     Does rad ray reached destination or loose all intensity in blockers?
    /// </summary>
    public bool ReachedDestination => Rads > 0;

    /// <summary>
    ///     Does radiation traveled by grid tiles (gridcast) or world coordinates (raycast)?
    /// </summary>
    public bool IsGridcast => Grid != null;

    /// <summary>
    ///     Grid uid on which gridcast traveled. If it wasn't gridcast
    ///     will be set to null. Right now gridcast can travel only on one grid.
    /// </summary>
    public EntityUid? Grid;
    /// <summary>
    ///     All tiles visited by gridcast. If radiation has encountered blockers on this tile
    ///     will have float with updated radiation value. Empty if not gridcast.
    /// </summary>
    /// <remarks>Last tile may have negative value if ray lost all intensity.</remarks>
    public List<(Vector2i, float?)> VisitedTiles = new();

    /// <summary>
    ///     All blockers visited by raycast. Consist of pairs of impact point in world space
    ///     and float with updated radiation value. Empty if not raycast.
    /// </summary>
    /// <remarks>Last position may have negative value if ray lost all intensity.</remarks>
    public List<(Vector2, float)> Blockers = new();
}
