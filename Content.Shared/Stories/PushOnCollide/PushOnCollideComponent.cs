namespace Content.Shared.Stories.Damage.Components;

[RegisterComponent]
public sealed partial class PushOnCollideComponent : Component
{
    [DataField("strength")]
    public float Strength { get; set; } = 10f;
}
