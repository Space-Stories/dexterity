﻿using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Body.Prototypes;

[Prototype("body")]
public sealed class BodyPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = default!;

    [DataField("name")]
    public string Name { get; private set; } = "";

    [DataField("root")] public string Root { get; private set; } = string.Empty;

    [DataField("slots")] public Dictionary<string, BodyPrototypeSlot> Slots { get; private set; } = new();

    private BodyPrototype() { }

    public BodyPrototype(string id, string name, string root, Dictionary<string, BodyPrototypeSlot> slots)
    {
        ID = id;
        Name = name;
        Root = root;
        Slots = slots;
    }
}

[DataRecord]
public sealed record BodyPrototypeSlot
{
    public EntProtoId? Part;
    public readonly HashSet<string> Connections = new();
    public readonly Dictionary<string, string> Organs = new();
}
