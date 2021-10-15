﻿using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part
{
    /// <summary>
    ///     Defines the symmetry of a <see cref="SharedBodyPartComponent"/>.
    /// </summary>
    [Serializable, NetSerializable]
    public enum BodyPartSymmetry
    {
        None = 0,
        Left,
        Right
    }
}
