using Content.Shared.Parallax.Biomes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Salvage.Expeditions.Modifiers;

/// <summary>
/// Affects the biome to be used for salvage.
/// </summary>
[Prototype("salvageBiomeMod")]
public sealed class SalvageBiomeMod : IPrototype, ISalvageMod
{
    [IdDataField] public string ID { get; } = default!;

    /// <summary>
    /// Cost for difficulty modifiers.
    /// </summary>
    [DataField("cost")]
    public float Cost { get; } = 0f;

    /// <summary>
    /// Is weather allowed to apply to this biome.
    /// </summary>
    [DataField("weather")]
    public bool Weather = true;

    [DataField("biome", required: true, customTypeSerializer:typeof(PrototypeIdSerializer<BiomePrototype>))]
    public string? BiomePrototype;
}
