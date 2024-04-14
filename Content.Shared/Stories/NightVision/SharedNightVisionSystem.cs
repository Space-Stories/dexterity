namespace Content.Shared.Stories.NightVision;

public abstract class SharedNightVisionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<NightVisionComponent, MapInitEvent>(OnNightVisionMapInit);
        SubscribeLocalEvent<NightVisionComponent, ComponentRemove>(OnNightVisionRemove);

        SubscribeLocalEvent<NightVisionComponent, AfterAutoHandleStateEvent>(OnNightVisionAfterHandle);
    }

    private void OnNightVisionAfterHandle(Entity<NightVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        NightVisionChanged(ent);
    }

    private void OnNightVisionMapInit(Entity<NightVisionComponent> ent, ref MapInitEvent args)
    {
        NightVisionChanged(ent);
    }

    private void OnNightVisionRemove(Entity<NightVisionComponent> ent, ref ComponentRemove args)
    {
        NightVisionRemoved(ent);
    }

    protected virtual void NightVisionChanged(Entity<NightVisionComponent> ent)
    {
    }

    protected virtual void NightVisionRemoved(Entity<NightVisionComponent> ent)
    {
    }
}
