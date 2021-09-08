﻿using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Chemistry.Components.SolutionManager
{
    /// <summary>
    ///     Denotes the solution that can removed  be with syringes.
    /// </summary>
    [RegisterComponent]
    public class DrawableSolutionComponent : Component
    {
        public override string Name => "DrawableSolution";

        /// <summary>
        /// Solution name that can be removed with syringes.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("solution")]
        public string Solution { get; set; } = "default";
    }
}
