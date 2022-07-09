using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.UserInterface;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Audio;
using Content.Shared.Interaction.Events;
using Content.Shared.Toggleable;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server.Atmos.EntitySystems
{
    [UsedImplicitly]
    public sealed class GasTankSystem : EntitySystem
    {
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly ExplosionSystem _explosions = default!;
        [Dependency] private readonly InternalsSystem _internals = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly SharedContainerSystem _containers = default!;
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;

        private const float TimerDelay = 0.5f;
        private float _timer = 0f;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<GasTankComponent, ComponentShutdown>(OnGasShutdown);
            SubscribeLocalEvent<GasTankComponent, BeforeActivatableUIOpenEvent>(BeforeUiOpen);
            SubscribeLocalEvent<GasTankComponent, GetItemActionsEvent>(OnGetActions);
            SubscribeLocalEvent<GasTankComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<GasTankComponent, ToggleActionEvent>(OnActionToggle);
            SubscribeLocalEvent<GasTankComponent, GotEquippedEvent>(OnGasTankEquip);
            SubscribeLocalEvent<GasTankComponent, GotEquippedHandEvent>(OnGasTankEquipHand);
            SubscribeLocalEvent<GasTankComponent, DroppedEvent>(OnDropped);

            SubscribeLocalEvent<GasTankComponent, GasTankSetPressureMessage>(OnGasTankSetPressure);
            SubscribeLocalEvent<GasTankComponent, GasTankToggleInternalsMessage>(OnGasTankToggleInternals);
        }

        private void OnGasShutdown(EntityUid uid, GasTankComponent component, ComponentShutdown args)
        {
            DisconnectFromInternals(component);
        }

        private void OnGasTankToggleInternals(EntityUid uid, GasTankComponent component, GasTankToggleInternalsMessage args)
        {
            if (args.Session is not IPlayerSession playerSession ||
                playerSession.AttachedEntity is not {} player) return;

            ToggleInternals(player, component);
        }

        private void OnGasTankSetPressure(EntityUid uid, GasTankComponent component, GasTankSetPressureMessage args)
        {
            component.OutputPressure = args.Pressure;
        }

        public void UpdateUserInterface(GasTankComponent component, bool initialUpdate = false)
        {
            var internals = GetInternalsComponent(component);
            _ui.GetUiOrNull(component.Owner, SharedGasTankUiKey.Key)?.SetState(
                new GasTankBoundUserInterfaceState
                {
                    TankPressure = component.Air?.Pressure ?? 0,
                    OutputPressure = initialUpdate ? component.OutputPressure : null,
                    InternalsConnected = component.IsConnected,
                    CanConnectInternals = IsFunctional(component) && internals != null
                });
        }

        private void OnGasTankEquipHand(EntityUid uid, GasTankComponent component, GotEquippedHandEvent args)
        {
            _alerts.ShowAlert(args.User, AlertType.Internals, GetSeverity(component));
        }

        private void OnGasTankEquip(EntityUid uid, GasTankComponent component, GotEquippedEvent args)
        {
            _alerts.ShowAlert(args.Equipee, AlertType.Internals, GetSeverity(component));
        }

        private void BeforeUiOpen(EntityUid uid, GasTankComponent component, BeforeActivatableUIOpenEvent args)
        {
            // Only initial update includes output pressure information, to avoid overwriting client-input as the updates come in.
            UpdateUserInterface(component, true);
        }

        private void OnDropped(EntityUid uid, GasTankComponent component, DroppedEvent args)
        {
            DisconnectFromInternals(component, args.User);

            if (CanRemoveAlert(args.User))
                _alerts.ClearAlertCategory(args.User, AlertCategory.Internals);
        }

        private bool CanRemoveAlert(EntityUid user)
        {
            _inventory.TryGetContainerSlotEnumerator(user, out var enumerator);

            while (enumerator.MoveNext(out var container))
            {
                if (!HasComp<GasTankComponent>(container.ContainedEntity)) continue;
                return false;
            }

            return true;
        }

        private void OnGetActions(EntityUid uid, GasTankComponent component, GetItemActionsEvent args)
        {
            args.Actions.Add(component.ToggleAction);
        }

        private void OnExamined(EntityUid uid, GasTankComponent component, ExaminedEvent args)
        {
            if (args.IsInDetailsRange)
                args.PushMarkup(Loc.GetString("comp-gas-tank-examine", ("pressure", Math.Round(component.Air?.Pressure ?? 0))));
            if (component.IsConnected)
                args.PushMarkup(Loc.GetString("comp-gas-tank-connected"));
        }

        private void OnActionToggle(EntityUid uid, GasTankComponent component, ToggleActionEvent args)
        {
            if (args.Handled)
                return;

            ToggleInternals(args.Performer, component);
            args.Handled = true;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            _timer += frameTime;

            if (_timer < TimerDelay) return;
            _timer -= TimerDelay;

            foreach (var gasTank in EntityManager.EntityQuery<GasTankComponent>())
            {
                _atmosphereSystem.React(gasTank.Air, gasTank);
                CheckStatus(gasTank);
                if (_ui.IsUiOpen(gasTank.Owner, SharedGasTankUiKey.Key))
                {
                    UpdateUserInterface(gasTank);
                }
            }
        }

        private void ToggleInternals(EntityUid user, GasTankComponent component)
        {
            if (component.IsConnected)
            {
                DisconnectFromInternals(component);
            }
            else
            {
                ConnectToInternals(component);
            }

            _alerts.ShowAlert(user, AlertType.Internals, GetSeverity(component));
        }

        private short GetSeverity(GasTankComponent component)
        {
            if (!component.IsConnected) return 2;
            return component.Air.TotalMoles > 0f ? (short) 1 : (short) 0;
        }

        public GasMixture? RemoveAir(GasTankComponent component, float amount)
        {
            var gas = component.Air?.Remove(amount);
            CheckStatus(component);
            return gas;
        }

        public GasMixture RemoveAirVolume(GasTankComponent component, float volume)
        {
            if (component.Air == null)
                return new GasMixture(volume);

            var tankPressure = component.Air.Pressure;
            if (tankPressure < component.OutputPressure)
            {
                component.OutputPressure = tankPressure;
                UpdateUserInterface(component);
            }

            var molesNeeded = component.OutputPressure * volume / (Atmospherics.R * component.Air.Temperature);

            var air = RemoveAir(component, molesNeeded);

            if (air != null)
                air.Volume = volume;
            else
                return new GasMixture(volume);

            return air;
        }

        public void ConnectToInternals(GasTankComponent component)
        {
            if (component.IsConnected || !IsFunctional(component)) return;
            var internals = GetInternalsComponent(component);
            if (internals == null) return;
            component.IsConnected = _internals.TryConnectTank(internals, component.Owner);
            _actions.SetToggled(component.ToggleAction, component.IsConnected);

            // Couldn't toggle!
            if (!component.IsConnected) return;

            component._connectStream?.Stop();

            if (component._connectSound != null)
                component._connectStream = SoundSystem.Play(component._connectSound.GetSound(), Filter.Pvs(component.Owner, entityManager: EntityManager), component.Owner, component._connectSound.Params);

            UpdateUserInterface(component);
        }

        public void DisconnectFromInternals(GasTankComponent component, EntityUid? owner = null)
        {
            if (!component.IsConnected) return;
            component.IsConnected = false;
            _actions.SetToggled(component.ToggleAction, false);

            _internals.DisconnectTank(GetInternalsComponent(component, owner));
            component._disconnectStream?.Stop();

            if (component._disconnectSound != null)
                component._disconnectStream = SoundSystem.Play(component._disconnectSound.GetSound(), Filter.Pvs(component.Owner, entityManager: EntityManager), component.Owner, component._disconnectSound.Params);

            UpdateUserInterface(component);
        }

        private InternalsComponent? GetInternalsComponent(GasTankComponent component, EntityUid? owner = null)
        {
            if (Deleted(component.Owner)) return null;
            if (owner != null) return CompOrNull<InternalsComponent>(owner.Value);
            return _containers.TryGetContainingContainer(component.Owner, out var container)
                ? CompOrNull<InternalsComponent>(container.Owner)
                : null;
        }

        public void AssumeAir(GasTankComponent component, GasMixture giver)
        {
            _atmosphereSystem.Merge(component.Air, giver);
            CheckStatus(component);
        }

        public void CheckStatus(GasTankComponent component)
        {
            if (component.Air == null)
                return;

            var pressure = component.Air.Pressure;

            if (pressure > component.TankFragmentPressure)
            {
                // Give the gas a chance to build up more pressure.
                for (var i = 0; i < 3; i++)
                {
                    _atmosphereSystem.React(component.Air, component);
                }

                pressure = component.Air.Pressure;
                var range = (pressure - component.TankFragmentPressure) / component.TankFragmentScale;

                // Let's cap the explosion, yeah?
                // !1984
                if (range > GasTankComponent.MaxExplosionRange)
                {
                    range = GasTankComponent.MaxExplosionRange;
                }

                _explosions.TriggerExplosive(component.Owner, radius: range);

                return;
            }

            if (pressure > component.TankRupturePressure)
            {
                if (component._integrity <= 0)
                {
                    var environment = _atmosphereSystem.GetContainingMixture(component.Owner, false, true);
                    if(environment != null)
                        _atmosphereSystem.Merge(environment, component.Air);

                    SoundSystem.Play(component._ruptureSound.GetSound(), Filter.Pvs(component.Owner), Transform(component.Owner).Coordinates, AudioHelpers.WithVariation(0.125f));

                    QueueDel(component.Owner);
                    return;
                }

                component._integrity--;
                return;
            }

            if (pressure > component.TankLeakPressure)
            {
                if (component._integrity <= 0)
                {
                    var environment = _atmosphereSystem.GetContainingMixture(component.Owner, false, true);
                    if (environment == null)
                        return;

                    var leakedGas = component.Air.RemoveRatio(0.25f);
                    _atmosphereSystem.Merge(environment, leakedGas);
                }
                else
                {
                    component._integrity--;
                }

                return;
            }

            if (component._integrity < 3)
                component._integrity++;
        }

        private bool IsFunctional(GasTankComponent component)
        {
            return GetInternalsComponent(component) != null;
        }
    }
}
