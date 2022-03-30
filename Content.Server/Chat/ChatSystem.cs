﻿using System.Linq;
using System.Text;
using System.Xml;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Disease;
using Content.Server.Disease.Components;
using Content.Server.Ghost.Components;
using Content.Server.Headset;
using Content.Server.Players;
using Content.Server.Radio.EntitySystems;
using Content.Shared.ActionBlocker;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Disease.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Players;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Chat;

/// <summary>
///     ChatSystem is responsible for in-simulation chat handling, such as whispering, speaking, emoting, etc.
///     ChatSystem depends on ChatManager to actually send the messages.
/// </summary>
public sealed class ChatSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IChatSanitizationManager _sanitizer = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AdminLogSystem _logs = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ListeningSystem _listener = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private const int VoiceRange = 7; // how far voice goes in world units
    private const int WhisperRange = 2; // how far whisper goes in world units

    private bool _loocEnabled = true;
    private readonly bool _adminLoocEnabled = true;

    public override void Initialize()
    {
        _configurationManager.OnValueChanged(CCVars.LoocEnabled, OnLoocEnabledChanged, true);
    }

    private void OnLoocEnabledChanged(bool val)
    {
        if (_loocEnabled == val) return;

        _loocEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-looc-chat-enabled-message" : "chat-manager-looc-chat-disabled-message"));
    }

    public void TrySendInGameICMessage(EntityUid source, string message, InGameICChatType desiredType, bool hideChat,
        IConsoleShell? shell = null, IPlayerSession? player = null)
    {
        if (HasComp<GhostComponent>(source))
        {
            // Ghosts can only send dead chat messages, so we'll forward it to InGame OOC.
            TrySendInGameOOCMessage(source, message, InGameOOCChatType.Dead, hideChat, shell, player);
            return;
        }

        if (!CanSessionInGameChat(shell, player))
            return;

        message = SanitizeInGameICMessage(source, message, out var emoteStr);

        // Is this -actually- an emote, and is a player sending it?
        if (player != null && emoteStr != message && emoteStr != null)
        {
            SendEntityEmote(source, emoteStr, hideChat);
            return;
        }

        // Sus
        if (player?.AttachedEntity is { Valid: true } entity && source != entity)
        {
            return;
        }

        // Otherwise, send whatever type.
        switch (desiredType)
        {
            case InGameICChatType.Speak:
                SendEntitySpeak(source, message, hideChat);
                break;
            case InGameICChatType.Whisper:
                SendEntityWhisper(source, message, hideChat);
                break;
            case InGameICChatType.Emote:
                SendEntityEmote(source, message, hideChat);
                break;
        }
    }

    public void TrySendInGameOOCMessage(EntityUid source, string message, InGameOOCChatType type, bool hideChat,
        IConsoleShell? shell = null, IPlayerSession? player = null)
    {
        if (!CanSessionInGameChat(shell, player))
            return;

        // It doesn't make any sense for a non-player to send in-game OOC messages, whereas non-players may be sending
        // in-game IC messages.
        if (player?.AttachedEntity is not { Valid: true } entity || source != entity)
            return;

        message = SanitizeInGameOOCMessage(message);

        switch (type)
        {
            case InGameOOCChatType.Dead:
                SendDeadChat(source, player, message, hideChat);
                break;
            case InGameOOCChatType.Looc:
                SendLOOC(source, player, message, hideChat);
                break;
        }
    }

    #region Private API

    private void SendEntitySpeak(EntityUid source, string message, bool hideChat = false)
    {
        if (!_actionBlocker.CanSpeak(source)) return;

        // TODO wtf make this an event.
        if (HasComp<DiseasedComponent>(source) &&
            TryComp<DiseaseCarrierComponent>(source, out var carrier))
            EntitySystem.Get<DiseaseSystem>().SneezeCough(source, _random.Pick(carrier.Diseases), string.Empty);

        var messageWrap = Loc.GetString("chat-manager-entity-say-wrap-message",
            ("entityName", Name(source)));

        SendInVoiceRange(ChatChannel.Local, message, messageWrap, source, hideChat);

        _logs.Add(LogType.Chat, LogImpact.Low, $"Say from {ToPrettyString(source):user}: {message}");
    }

    private void SendEntityWhisper(EntityUid source, string message, bool hideChat = false)
    {
        if (!_actionBlocker.CanSpeak(source)) return;

        _listener.PingListeners(source, message);
        var obfuscatedMessage = ObfuscateMessageReadability(message, 0.2f);

        var transformSource = Transform(source);
        var sourceCoords = transformSource.Coordinates;
        var messageWrap = Loc.GetString("chat-manager-entity-whisper-wrap-message",
            ("entityName", Name(source)));

        var xforms = GetEntityQuery<TransformComponent>();
        var ghosts = GetEntityQuery<GhostComponent>();

        var sessions = new List<ICommonSession>();
        ClientDistanceToList(source, VoiceRange, sessions);

        // Whisper needs these special calculations, since it can obfuscate the message.
        foreach (var session in sessions)
        {
            if (session.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            var transformEntity = xforms.GetComponent(playerEntity);

            if (sourceCoords.InRange(EntityManager, transformEntity.Coordinates, WhisperRange) ||
                ghosts.HasComponent(playerEntity))
            {
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, message, messageWrap, source, hideChat, session.ConnectedClient);
            }
            else
            {
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, obfuscatedMessage, messageWrap, source, hideChat,
                    session.ConnectedClient);
            }
        }

        _logs.Add(LogType.Chat, LogImpact.Low, $"Whisper from {ToPrettyString(source):user}: {message}");
    }

    private void SendEntityEmote(EntityUid source, string action, bool hideChat)
    {
        if (!_actionBlocker.CanEmote(source)) return;

        var messageWrap = Loc.GetString("chat-manager-entity-me-wrap-message",
            ("entityName", Name(source)));

        SendInVoiceRange(ChatChannel.Emotes, action, messageWrap, source, hideChat);
        _logs.Add(LogType.Chat, LogImpact.Low, $"Emote from {ToPrettyString(source):user}: {action}");
    }

    private void SendLOOC(EntityUid source, IPlayerSession player, string message, bool hideChat)
    {
        if (_adminManager.IsAdmin(player))
        {
            if (!_adminLoocEnabled) return;
        }
        else if (!_loocEnabled) return;
        var messageWrap = Loc.GetString("chat-manager-entity-looc-wrap-message",
            ("entityName", Name(source)));

        SendInVoiceRange(ChatChannel.LOOC, message, messageWrap, source, hideChat);
        _logs.Add(LogType.Chat, LogImpact.Low, $"LOOC from {player:Player}: {message}");
    }

    private void SendDeadChat(EntityUid source, IPlayerSession player, string message, bool hideChat)
    {
        var clients = GetDeadChatClients();
        var playerName = Name(source);
        string messageWrap;
        if (_adminManager.IsAdmin(player))
        {
            messageWrap = Loc.GetString("chat-manager-send-admin-dead-chat-wrap-message",
                ("adminChannelName", Loc.GetString("chat-manager-admin-channel-name")),
                ("userName", player.ConnectedClient.UserName));
            _logs.Add(LogType.Chat, LogImpact.Low, $"Dead chat from {player:Player}: {message}");
        }
        else
        {
            messageWrap = Loc.GetString("chat-manager-send-dead-chat-wrap-message",
                ("deadChannelName", Loc.GetString("chat-manager-dead-channel-name")),
                ("playerName", (playerName)));
            _logs.Add(LogType.Chat, LogImpact.Low, $"Admin dead chat from {player:Player}: {message}");
        }

        _chatManager.ChatMessageToMany(ChatChannel.Dead, message, messageWrap, source, hideChat, clients.ToList());
        _logs.Add(LogType.Chat, LogImpact.Low, $"Dead chat from {player:Player}: {message}");
    }
    #endregion

    #region Utility

    private void SendInVoiceRange(ChatChannel channel, string message, string messageWrap, EntityUid source, bool hideChat)
    {
        var sessions = new List<ICommonSession>();
        ClientDistanceToList(source, VoiceRange, sessions);

        foreach (var session in sessions)
        {
            _chatManager.ChatMessageToOne(channel, message, messageWrap, source, hideChat, session.ConnectedClient);
        }
    }

    private bool CanSessionInGameChat(IConsoleShell? shell = null, IPlayerSession? player = null)
    {
        var mindComponent = player?.ContentData()?.Mind;

        if (mindComponent == null)
        {
            shell?.WriteError("You don't have a mind!");
            return false;
        }

        if (player?.AttachedEntity is not { Valid: true } _)
        {
            shell?.WriteError("You don't have an entity!");
            return false;
        }

        return true;
    }

    private string SanitizeInGameICMessage(EntityUid source, string message, out string? emoteStr)
    {
        var newMessage = message.Trim();
        newMessage = SanitizeMessageCapital(source, newMessage);
        newMessage = FormattedMessage.EscapeText(newMessage);

        _sanitizer.TrySanitizeOutSmilies(newMessage, source, out newMessage, out emoteStr);

        return newMessage;
    }

    private string SanitizeInGameOOCMessage(string message)
    {
        var newMessage = message.Trim();
        newMessage = FormattedMessage.EscapeText(newMessage);

        return newMessage;
    }

    private IEnumerable<INetChannel> GetDeadChatClients()
    {
        return Filter.Empty()
            .AddWhereAttachedEntity(uid => HasComp<GhostComponent>(uid))
            .Recipients
            .Union(_adminManager.ActiveAdmins)
            .Select(p => p.ConnectedClient);
    }

    private string SanitizeMessageCapital(EntityUid source, string message)
    {
        if (message.StartsWith(';'))
        {
            // Remove semicolon
            message = message.Substring(1).TrimStart();

            // Capitalize first letter
            message = message[0].ToString().ToUpper() + message.Remove(0, 1);

            if (_inventory.TryGetSlotEntity(source, "ears", out var entityUid) &&
                TryComp(entityUid, out HeadsetComponent? headset))
            {
                headset.RadioRequested = true;
            }
            else
            {
                source.PopupMessage(Loc.GetString("chat-manager-no-headset-on-message"));
            }
        }
        else
        {
            // Capitalize first letter
            message = message[0].ToString().ToUpper() + message.Remove(0, 1);
        }

        return message;
    }

    private void ClientDistanceToList(EntityUid source, int voiceRange, List<ICommonSession> playerSessions)
    {
        var ghosts = GetEntityQuery<GhostComponent>();
        var xforms = GetEntityQuery<TransformComponent>();

        var transformSource = xforms.GetComponent(source);
        var sourceMapId = transformSource.MapID;
        var sourceCoords = transformSource.Coordinates;

        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not {Valid: true} playerEntity)
                continue;

            var transformEntity = xforms.GetComponent(playerEntity);

            if (transformEntity.MapID != sourceMapId ||
                !ghosts.HasComponent(playerEntity) &&
                !sourceCoords.InRange(EntityManager, transformEntity.Coordinates, voiceRange))
                continue;

            playerSessions.Add(player);
        }
    }

    private string ObfuscateMessageReadability(string message, float chance)
    {
        var modifiedMessage = new StringBuilder(message);

        for (var i = 0; i < message.Length; i++)
        {
            if (char.IsWhiteSpace((modifiedMessage[i])))
            {
                continue;
            }

            if (_random.Prob(chance))
            {
                modifiedMessage[i] = modifiedMessage[i];
            }
            else
            {
                modifiedMessage[i] = '~';
            }
        }

        return modifiedMessage.ToString();
    }

    #endregion
}

/// <summary>
///     InGame IC chat is for chat that is specifically ingame (not lobby) but is also in character, i.e. speaking.
/// </summary>
public enum InGameICChatType
{
    Speak,
    Emote,
    Whisper
}

/// <summary>
///     InGame OOC chat is for chat that is specifically ingame (not lobby) but is OOC, like deadchat or LOOC.
/// </summary>
public enum InGameOOCChatType
{
    Looc,
    Dead
}
