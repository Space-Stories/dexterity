using Content.Shared.Actions;
using Content.Shared.Roles;
using Content.Shared.Terminator.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Terminator.Components;

/// <summary>
/// Main exterminator component, handles the target, if any, and adding the curse action.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedTerminatorSystem))]
public sealed partial class TerminatorComponent : Component
{
    /// <summary>
    /// Used to force the terminate objective's target.
    /// If null it will be a random person.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// Action to use for arnie's curse.
    /// </summary>
    [DataField]
    public EntProtoId CurseAction = "ActionArniesCurse";

    /// <summary>
    /// Action created for using arnie's curse.
    /// May not exist since TimedDespawn could delete it.
    /// If it is null the action has been used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? CurseActionEntity;
}

/// <summary>
/// Raised on an exterminator to give it arnie's curse and make it valid.
/// </summary>
public sealed partial class ExterminatorCurseEvent : InstantActionEvent
{
    /// <summary>
    /// Starting gear to give when curse is used.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<StartingGearPrototype> Gear = string.Empty;

    /// <summary>
    /// Replacement accent to give when curse is used.
    /// </summary>
    /// <remarks>
    /// Due to speech NiceCode being server only this cant be ProtoId in shared :(
    /// </remarks>
    [DataField(required: true)]
    public string Accent = string.Empty;
}
