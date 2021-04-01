﻿using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Client.GameObjects.Components.HUD.Inventory
{
    [Prototype("hudTheme")]
    public class HudThemePrototype : IPrototype
    {
        [DataField("name", required: true)]
        public string Name { get; } = string.Empty;

        [field: DataField("id", required: true)]
        public string ID { get; } = string.Empty;

        [field: DataField("path", required: true)]
        public string Path { get; } = string.Empty;
    }
}
