﻿using Content.Shared.Inventory;

namespace Content.Server.Inventory;

[RegisterComponent]
[ComponentReference(typeof(InventoryComponent))]
public sealed partial class ServerInventoryComponent : InventoryComponent { }
