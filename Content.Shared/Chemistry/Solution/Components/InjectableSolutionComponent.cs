﻿using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Chemistry.Solution.Components
{
    [RegisterComponent]
    public class InjectableSolutionComponent : Component
    {
        public override string Name => "InjectableSolution";

        [ViewVariables]
        [DataField("solution")]
        public string Solution { get; set; } = default!;
    }
}
