using Content.Server.Storage.EntitySystems;
using Content.Shared.Storage.Components;

namespace Content.Server.Storage.Components
{
    [RegisterComponent]
    [Access(typeof(StorageSystem))]
    public sealed class StorageFillComponent : SharedStorageFillComponent {};
}
