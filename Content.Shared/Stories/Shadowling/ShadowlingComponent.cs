using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Prototypes;

namespace Content.Shared.Stories.Shadowling;
[NetworkedComponent, RegisterComponent]
public sealed partial class ShadowlingComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), DataField("actions")]
    public Dictionary<EntProtoId, int> Actions = new()
    {
        {"ActionShadowlingHatch", 0},
        {"ActionShadowlingShadowWalk", 0},
        {"ActionShadowlingGlare", 0},
        {"ActionShadowlingVeil", 0},
        {"ActionShadowlingIcyVeins", 0},
        {"ActionShadowlingCollectiveMind", 0},
        {"ActionShadowlingRapidReHatch", 0},
        {"ActionShadowlingEnthrall", 0},

        {"ActionShadowlingBlindnessSmoke", 5},
        {"ActionShadowlingSonicScreech", 3},
        {"ActionShadowlingBlackRecuperation", 9},

        {"ActionShadowlingAscendance", 15},
        {"ActionShadowlingLightningStorm", 15},
        {"ActionShadowlingAnnihilate", 15},
    };

    [ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid?> GrantedActions = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid> Thralls = new();
}
