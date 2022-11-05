﻿using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Server.Xenoarchaeology.Equipment.Components;

/// <summary>
/// Activecomp used for tracking artifact analyzers that are currently
/// in the process of scanning an artifact.
/// </summary>
[RegisterComponent]
public sealed class ActiveArtifactAnalyzerComponent : Component
{
    /// <summary>
    /// When did the scanning start?
    /// </summary>
    [DataField("startTime", customTypeSerializer: typeof(TimespanSerializer))]
    public TimeSpan StartTime;

    /// <summary>
    /// What is being scanned?
    /// </summary>
    [ViewVariables]
    public EntityUid Artifact;

    public readonly SoundSpecifier ScanningSound = new SoundPathSpecifier("/Audio/Machines/scan_loop.ogg");
    public readonly SoundSpecifier ScanFinishedSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");
    public IPlayingAudioStream? LoopStream;
}
