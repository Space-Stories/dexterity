using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Decals;
using Content.Server.Popups;
using Content.Shared.Audio;
using Content.Shared.Crayon;
using Content.Shared.Database;
using Content.Shared.Decals;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Crayon;

public sealed class CrayonSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly AdminLogSystem _logs = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrayonComponent, ComponentInit>(OnCrayonInit);
        SubscribeLocalEvent<CrayonComponent, CrayonSelectMessage>(OnCrayonBoundUI);
        SubscribeLocalEvent<CrayonComponent, UseInHandEvent>(OnCrayonUse);
        SubscribeLocalEvent<CrayonComponent, AfterInteractEvent>(OnCrayonAfterInteract);
        SubscribeLocalEvent<CrayonComponent, DroppedEvent>(OnCrayonDropped);
        SubscribeLocalEvent<CrayonComponent, ComponentGetState>(OnCrayonGetState);
    }

    private static void OnCrayonGetState(EntityUid uid, CrayonComponent component, ref ComponentGetState args)
    {
        args.State = new CrayonComponentState(component._color, component.SelectedState, component.Charges, component.Capacity);
    }

    private void OnCrayonAfterInteract(EntityUid uid, CrayonComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (component.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("crayon-interact-not-enough-left-text"), uid, Filter.Entities(args.User));
            args.Handled = true;
            return;
        }

        if (!args.ClickLocation.IsValid(EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("crayon-interact-invalid-location"), uid, Filter.Entities(args.User));
            args.Handled = true;
            return;
        }

        if(!_decals.TryAddDecal(component.SelectedState, args.ClickLocation.Offset(new Vector2(-0.5f,-0.5f)), out _, Color.FromName(component._color), cleanable: true))
            return;

        if (component.UseSound != null)
            SoundSystem.Play(Filter.Pvs(uid), component.UseSound.GetSound(), uid, AudioHelpers.WithVariation(0.125f));

        // Decrease "Ammo"
        component.Charges--;
        Dirty(component);
        _logs.Add(LogType.CrayonDraw, LogImpact.Low, $"{EntityManager.ToPrettyString(args.User):user} drew a {component._color:color} {component.SelectedState}");
        args.Handled = true;
    }

    private void OnCrayonUse(EntityUid uid, CrayonComponent component, UseInHandEvent args)
    {
        // Open crayon window if neccessary.
        if (args.Handled)
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor)) return;

        component.UserInterface?.Toggle(actor.PlayerSession);

        if (component.UserInterface?.SessionHasOpen(actor.PlayerSession) == true)
        {
            // Tell the user interface the selected stuff
            component.UserInterface.SetState(new CrayonBoundUserInterfaceState(component.SelectedState, component.Color));
        }

        args.Handled = true;
    }

    private void OnCrayonBoundUI(EntityUid uid, CrayonComponent component, CrayonSelectMessage args)
    {
        // Check if the selected state is valid
        if (!_prototypeManager.TryIndex<DecalPrototype>(args.State, out var prototype) || !prototype.Tags.Contains("crayon")) return;

        component.SelectedState = args.State;
        Dirty(component);
    }

    private void OnCrayonInit(EntityUid uid, CrayonComponent component, ComponentInit args)
    {
        component.Charges = component.Capacity;

        // Get the first one from the catalog and set it as default
        var decal = _prototypeManager.EnumeratePrototypes<DecalPrototype>().FirstOrDefault(x => x.Tags.Contains("crayon"));
        component.SelectedState = decal?.ID ?? string.Empty;
        Dirty(component);
    }

    private void OnCrayonDropped(EntityUid uid, CrayonComponent component, DroppedEvent args)
    {
        if (TryComp<ActorComponent>(args.User, out var actor))
            component.UserInterface?.Close(actor.PlayerSession);
    }
}
