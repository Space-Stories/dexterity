﻿using System.Threading;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Server.Magic;

/// <summary>
/// Spellbooks for having an entity learn spells as long as they've read the book and it's in their hand.
/// </summary>
[RegisterComponent]
public sealed class SpellbookComponent : Component
{
    /// <summary>
    /// List of spells that this book has. This is a combination of the WorldSpells, EntitySpells, and InstantSpells.
    /// </summary>
    [ViewVariables]
    public readonly List<ActionType> Spells = new();

    /// <summary>
    /// The three fields below is just used for initialization.
    /// </summary>
    [DataField("worldSpells", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<int, WorldTargetActionPrototype>))]
    public readonly Dictionary<string, int> WorldSpells = new();

    [DataField("entitySpells", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<int, EntityTargetActionPrototype>))]
    public readonly Dictionary<string, int> EntitySpells = new();

    [DataField("instantSpells", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<int, InstantActionPrototype>))]
    public readonly Dictionary<string, int> InstantSpells = new();

    [ViewVariables]
    [DataField("learnTime")]
    public float LearnTime = .75f;

    /// <summary>
    /// If set to true then you don't have to keep the book after learning it
    /// </summary>
    [ViewVariables]
    [DataField("isPersistent")]
    public bool Persistent = false;

    /// <summary>
    /// If set to true the book will crumble after a single use
    /// </summary>
    [ViewVariables]
    [DataField("singleUse")]
    public bool SingleUse = false;

    public CancellationTokenSource? CancelToken;
}
