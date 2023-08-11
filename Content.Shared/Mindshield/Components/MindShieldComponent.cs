using Robust.Shared.GameStates;

namespace Content.Shared.Mindshield.Components;
/// <summary>
/// If a player has a Mindshield they will get this component to prevent conversion.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed class MindShieldComponent : Component
{
}
