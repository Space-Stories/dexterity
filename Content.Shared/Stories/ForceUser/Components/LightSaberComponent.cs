using Robust.Shared.GameStates;

namespace Content.Shared.Stories.Force.LightSaber;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class LightSaberComponent : Component
{
    [DataField("lightSaberOwner"), AutoNetworkedField]
    public EntityUid? LightSaberOwner;

    [DataField("deactivateProb")]
    public float DeactivateProb = 0.5f;
}
