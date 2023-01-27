using Content.Shared.Tools;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;

namespace Content.Server.MachineLinking.Components
{
    [RegisterComponent]
    public sealed partial class SignalLinkerComponent : Component
    {
        [ViewVariables]
        public EntityUid? SavedTransmitter;

        [ViewVariables]
        public EntityUid? SavedReceiver;

        /// <summary>
        ///     Optional tool quality required for linker to work.
        ///     If linker entity doesn't have this quality it will ignore any interaction.
        /// </summary>
        [DataField("requiredQuality", customTypeSerializer: typeof(PrototypeIdSerializer<ToolQualityPrototype>))]
        [ViewVariables(VVAccess.ReadWrite)]
        public string? RequiredQuality;
    }
}
