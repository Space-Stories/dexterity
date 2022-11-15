using Content.Server.Chat.Systems;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.VoiceMask;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.Radio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;
using Robust.Shared.Utility;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;

namespace Content.Server.Headset
{
    [RegisterComponent]
    [ComponentReference(typeof(IRadio))]
    [ComponentReference(typeof(IListen))]
#pragma warning disable 618
    public sealed class HeadsetComponent : Component, IListen, IRadio
#pragma warning restore 618
    {
        [Dependency] private readonly IEntityManager _entMan = default!;
        [Dependency] private readonly IServerNetManager _netManager = default!;

        private ChatSystem _chatSystem = default!;
        private RadioSystem _radioSystem = default!;

        //[DataField("channels", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<RadioChannelPrototype>))]
        [ViewVariables]
        public HashSet<string> Channels = new()
        {
            "Common"
        };

        [DataField("chipsPrototypes", required: true)]
        public List<string> ChipsPrototypes = new List<string>();
        [ViewVariables]
        public List<EntityUid> ChipsInstalled = new List<EntityUid>();

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("isChipExtractable")]
        public bool IsChipsExtractable = true;
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("chipSlotsAmount")]
        public int ChipSlotsAmount = 2;

        [DataField("chipExtarctionSound")]
        public SoundSpecifier ChipExtarctionSound = new SoundPathSpecifier("/Audio/Items/pistol_magout.ogg");
        [DataField("chipInsertionSound")]
        public SoundSpecifier ChipInsertionSound = new SoundPathSpecifier("/Audio/Items/pistol_magin.ogg");

        [ViewVariables]
        public Container ChipContainer = default!;
        public const string ChipContainerName = "chip_slots";

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("listenRange")]
        public int ListenRange { get; private set; }

        public bool RadioRequested { get; set; }

        protected override void Initialize()
        {
            base.Initialize();

            _chatSystem = EntitySystem.Get<ChatSystem>();
            _radioSystem = EntitySystem.Get<RadioSystem>();
        }

        public bool CanListen(string message, EntityUid source, RadioChannelPrototype? prototype)
        {
            return prototype != null && Channels.Contains(prototype.ID) && RadioRequested;
        }

        public void Receive(string message, RadioChannelPrototype channel, EntityUid source)
        {
            if (!Channels.Contains(channel.ID) || !Owner.TryGetContainer(out var container)) return;

            if (!_entMan.TryGetComponent(container.Owner, out ActorComponent? actor)) return;

            var playerChannel = actor.PlayerSession.ConnectedClient;

            var name = _entMan.GetComponent<MetaDataComponent>(source).EntityName;

            if (_entMan.TryGetComponent(source, out VoiceMaskComponent? mask) && mask.Enabled)
            {
                name = mask.VoiceName;
            }

            message = _chatSystem.TransformSpeech(source, message);
            if (message.Length == 0)
                return;

            message = FormattedMessage.EscapeText(message);
            name = FormattedMessage.EscapeText(name);

            var msg = new MsgChatMessage
            {
                Channel = ChatChannel.Radio,
                Message = message,
                WrappedMessage = Loc.GetString("chat-radio-message-wrap", ("color", channel.Color), ("channel", $"\\[{channel.LocalizedName}\\]"), ("name", name), ("message", message))
            };

            _netManager.ServerSendMessage(msg, playerChannel);
        }

        public void Listen(string message, EntityUid speaker, RadioChannelPrototype? channel)
        {
            if (channel == null)
            {
                return;
            }

            Broadcast(message, speaker, channel);
        }

        public void Broadcast(string message, EntityUid speaker, RadioChannelPrototype channel)
        {
            if (!Channels.Contains(channel.ID)) return;

            _radioSystem.SpreadMessage(this, speaker, message, channel);
            RadioRequested = false;
        }
    }
}
