using Content.Shared.Procedural.PostGeneration;
using Content.Shared.Tag;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared.Procedural.DungeonGenerators;

/// <summary>
/// Places rooms in pre-selected pack layouts. Chooses rooms from the specified whitelist.
/// </summary>
public sealed class PrefabDunGen : IDunGen
{
    /// <summary>
    /// Rooms need to match any of these tags
    /// </summary>
    [DataField("roomWhitelist", customTypeSerializer:typeof(PrototypeIdListSerializer<TagPrototype>))]
    public List<string> RoomWhitelist = new();

    /// <summary>
    /// Room pack presets we can use for this prefab.
    /// </summary>
    [DataField("presets", required: true, customTypeSerializer:typeof(PrototypeIdSerializer<DungeonPresetPrototype>))]
    public List<string> Presets = new();
}
