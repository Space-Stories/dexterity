﻿using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Anomaly;

/// <summary>
/// The types of anomalous particles used
/// for interfacing with anomalies.
/// </summary>
/// <remarks>
/// The only thought behind these names is that
/// they're a continuation of radioactive particles.
/// Yes i know detla+ waves exist, but they're not
/// common enough for me to care.
/// </remarks>
[Serializable, NetSerializable]
public enum AnomalousParticleType : byte
{
    Delta,
    Epsilon,
    Zeta
}

[Serializable, NetSerializable]
public enum AnomalyVesselVisuals : byte
{
    HasAnomaly
}
[Serializable, NetSerializable]
public enum AnomalyVesselVisualLayers : byte
{
    Base
}

[Serializable, NetSerializable]
public enum AnomalyScannerUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class AnomalyScannerUserInterfaceState : BoundUserInterfaceState
{
    public FormattedMessage Message;

    public AnomalyScannerUserInterfaceState(FormattedMessage message)
    {
        Message = message;
    }
}
