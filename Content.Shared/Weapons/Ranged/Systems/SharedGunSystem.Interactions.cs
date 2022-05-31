using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.CombatMode;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    private void OnExamine(EntityUid uid, GunComponent component, ExaminedEvent args)
    {
        var selectColor = !_combatMode.IsInCombatMode(args.Examiner) ? SafetyExamineColor : ModeExamineColor;
        args.PushMarkup(Loc.GetString("gun-selected-mode-examine", ("color", ModeExamineColor), ("mode", component.SelectedMode)));
        args.PushMarkup(Loc.GetString("gun-fire-rate-examine", ("color", FireRateExamineColor), ("fireRate", component.FireRate)));
    }

    private void OnAltVerb(EntityUid uid, GunComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || component.SelectedMode == component.AvailableModes)
            return;

        var nextMode = GetNextMode(component);

        AlternativeVerb verb = new()
        {
            Act = () => SelectFire(component, nextMode, args.User),
            Text = $"Change to {nextMode}",
            IconTexture = "/Textures/Interface/VerbIcons/fold.svg.192dpi.png",
        };

        args.Verbs.Add(verb);
    }

    private SelectiveFire GetNextMode(GunComponent component)
    {
        var modes = new List<SelectiveFire>();

        foreach (var mode in Enum.GetValues<SelectiveFire>())
        {
            if ((mode & component.AvailableModes) == 0x0) continue;
            modes.Add(mode);
        }

        var index = modes.IndexOf(component.SelectedMode);
        return modes[(index + 1) % modes.Count];
    }

    private void SelectFire(GunComponent component, SelectiveFire fire, EntityUid? user = null)
    {
        if (component.SelectedMode == fire) return;

        DebugTools.Assert((component.AvailableModes  & fire) != 0x0);
        component.SelectedMode = fire;
        var curTime = Timing.CurTime;
        var cooldown = TimeSpan.FromSeconds(InteractNextFire);

        if (component.NextFire < curTime)
            component.NextFire = curTime + cooldown;
        else
            component.NextFire += cooldown;

        PlaySound(component.Owner, component.SoundModeToggle?.GetSound(Random, ProtoManager), user);
        Popup($"Selected {fire}", component.Owner, user);
        // When actions done add here.

        Dirty(component);
    }

    /// <summary>
    /// Cycles the gun's <see cref="SelectiveFire"/> to the next available one.
    /// </summary>
    public void CycleFire(GunComponent component, EntityUid? user = null)
    {
        // Noop
        if (component.SelectedMode == component.AvailableModes) return;

        DebugTools.Assert((component.AvailableModes & component.SelectedMode) == component.SelectedMode);
        var nextMode = GetNextMode(component);
        SelectFire(component, nextMode, user);
    }

    private sealed class CycleModeEvent : InstantActionEvent
    {
        public SelectiveFire Mode;

        public CycleModeEvent(SelectiveFire mode)
        {
            Mode = mode;
        }
    }

    private void OnCycleMode(EntityUid uid, GunComponent component, CycleModeEvent args)
    {
        SelectFire(component, args.Mode, args.Performer);
    }
}
