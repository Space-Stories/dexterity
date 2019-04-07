﻿using SS14.Shared.GameObjects;
using SS14.Shared.Serialization;
using System;

namespace Content.Shared.GameObjects.Components.Items
{
    [Serializable, NetSerializable]
    public class ClothingComponentState : ItemComponentState
    {
        public string ClothingEquippedPrefix { get; set; }

        public ClothingComponentState(string clothingEquippedPrefix, string equippedPrefix) : base(ContentNetIDs.CLOTHING)
        {
            ClothingEquippedPrefix = clothingEquippedPrefix;
            EquippedPrefix = equippedPrefix;
        }
    }
}
