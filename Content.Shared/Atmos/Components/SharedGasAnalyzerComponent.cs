using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Components
{
    [NetworkedComponent()]
    public abstract class SharedGasAnalyzerComponent : Component
    {

        [Serializable, NetSerializable]
        public enum GasAnalyzerUiKey
        {
            Key,
        }

        /// <summary>
        /// Atmospheric data is gathered in the system and sent to the user
        /// </summary>
        [Serializable, NetSerializable]
        public sealed class GasAnalyzerUserMessage : BoundUserInterfaceMessage
        {
            public string? Error;
            public GasMixEntry[] NodeGasMixes;

            public GasAnalyzerUserMessage(GasMixEntry[] nodeGasMixes, string? error = null)
            {
                NodeGasMixes = nodeGasMixes;
                Error = error;
            }
        }

        /// <summary>
        /// Contains information on a gas mix entry, turns into a tab in the UI
        /// </summary>
        [Serializable, NetSerializable]
        public struct GasMixEntry
        {
            /// <summary>
            /// Name of the tab in the UI
            /// </summary>
            public readonly string Name;
            public readonly float Pressure;
            public readonly float Temperature;
            public readonly GasEntry[]? Gases;

            public GasMixEntry(string name, float pressure, float temperature, GasEntry[]? gases = null)
            {
                Name = name;
                Pressure = pressure;
                Temperature = temperature;
                Gases = gases;
            }
        }

        /// <summary>
        /// Individual gas entry data for populating the UI
        /// </summary>
        [Serializable, NetSerializable]
        public struct GasEntry
        {
            public readonly string Name;
            public readonly float Amount;
            public readonly string Color;

            public GasEntry(string name, float amount, string color)
            {
                Name = name;
                Amount = amount;
                Color = color;
            }

            public override string ToString()
            {
                // e.g. "Plasma: 2000 mol"
                return Loc.GetString(
                    "gas-entry-info",
                     ("gasName", Name),
                     ("gasAmount", Amount));
            }
        }

        [Serializable, NetSerializable]
        public sealed class GasAnalyzerRefreshMessage : BoundUserInterfaceMessage
        {
            public GasAnalyzerRefreshMessage() {}
        }

        [Serializable, NetSerializable]
        public sealed class GasAnalyzerDisableMessage : BoundUserInterfaceMessage
        {
            public GasAnalyzerDisableMessage() {}
        }
    }

    [NetSerializable]
    [Serializable]
    public enum GasAnalyzerVisuals
    {
        VisualState,
    }

    [NetSerializable]
    [Serializable]
    public enum GasAnalyzerVisualState
    {
        Off,
        Working,
    }
}
