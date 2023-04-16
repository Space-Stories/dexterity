using Content.Shared.Salvage.Expeditions.Modifiers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Salvage.Expeditions;

[Prototype("salvageFaction")]
public sealed class SalvageFactionPrototype : IPrototype, ISalvageMod
{
    [IdDataField] public string ID { get; } = default!;

    /// <summary>
    /// Cost for difficulty modifiers.
    /// </summary>
    [DataField("cost")]
    public float Cost { get; } = 0f;

    [ViewVariables(VVAccess.ReadWrite), DataField("groups", required: true)]
    public List<SalvageMobGroup> MobGroups = default!;

    /// <summary>
    /// Miscellaneous data for factions.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("configs")]
    public Dictionary<string, string> Configs = new();
}
