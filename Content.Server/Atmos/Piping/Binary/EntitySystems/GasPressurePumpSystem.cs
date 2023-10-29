using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Binary.Components;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.EntitySystems;
using Content.Server.Nodes.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.Audio;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server.Atmos.Piping.Binary.EntitySystems
{
    [UsedImplicitly]
    public sealed class GasPressurePumpSystem : EntitySystem
    {
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly SharedAmbientSoundSystem _ambientSoundSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly NodeGraphSystem _nodeSystem = default!;
        [Dependency] private readonly AtmosPipeNetSystem _pipeNodeSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GasPressurePumpComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<GasPressurePumpComponent, AtmosDeviceUpdateEvent>(OnPumpUpdated);
            SubscribeLocalEvent<GasPressurePumpComponent, AtmosDeviceDisabledEvent>(OnPumpLeaveAtmosphere);
            SubscribeLocalEvent<GasPressurePumpComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<GasPressurePumpComponent, ActivateInWorldEvent>(OnPumpActivate);
            // Bound UI subscriptions
            SubscribeLocalEvent<GasPressurePumpComponent, GasPressurePumpChangeOutputPressureMessage>(OnOutputPressureChangeMessage);
            SubscribeLocalEvent<GasPressurePumpComponent, GasPressurePumpToggleStatusMessage>(OnToggleStatusMessage);
        }

        private void OnInit(EntityUid uid, GasPressurePumpComponent pump, ComponentInit args)
        {
            UpdateAppearance(uid, pump);
        }

        private void OnExamined(EntityUid uid, GasPressurePumpComponent pump, ExaminedEvent args)
        {
            if (!EntityManager.GetComponent<TransformComponent>(uid).Anchored || !args.IsInDetailsRange) // Not anchored? Out of range? No status.
                return;

            if (Loc.TryGetString("gas-pressure-pump-system-examined", out var str,
                    ("statusColor", "lightblue"), // TODO: change with pressure?
                    ("pressure", pump.TargetPressure)
                ))
            {
                args.PushMarkup(str);
            }
        }

        private void OnPumpUpdated(EntityUid uid, GasPressurePumpComponent pump, AtmosDeviceUpdateEvent args)
        {
            if (!pump.Enabled
            || !_nodeSystem.TryGetNode<AtmosPipeNodeComponent>(uid, pump.InletName, out var inletId, out var inletNode, out var inlet)
            || !_pipeNodeSystem.TryGetGas(inletId, out var inletGas, inlet, inletNode)
            || !_nodeSystem.TryGetNode<AtmosPipeNodeComponent>(uid, pump.OutletName, out var outletId, out var outletNode, out var outlet)
            || !_pipeNodeSystem.TryGetGas(outletId, out var outletGas, outlet, outletNode))
            {
                _ambientSoundSystem.SetAmbience(uid, false);
                return;
            }

            var outputStartingPressure = outletGas.Pressure;

            if (outputStartingPressure >= pump.TargetPressure)
            {
                _ambientSoundSystem.SetAmbience(uid, false);
                return; // No need to pump gas if target has been reached.
            }

            if (inletGas.TotalMoles > 0 && inletGas.Temperature > 0)
            {
                // We calculate the necessary moles to transfer using our good ol' friend PV=nRT.
                var pressureDelta = pump.TargetPressure - outputStartingPressure;
                var transferMoles = (pressureDelta * outletGas.Volume) / (inletGas.Temperature * Atmospherics.R);

                var removed = inletGas.Remove(transferMoles);
                _atmosphereSystem.Merge(outletGas, removed);
                _ambientSoundSystem.SetAmbience(uid, removed.TotalMoles > 0f);
            }
        }

        private void OnPumpLeaveAtmosphere(EntityUid uid, GasPressurePumpComponent pump, AtmosDeviceDisabledEvent args)
        {
            pump.Enabled = false;
            UpdateAppearance(uid, pump);

            DirtyUI(uid, pump);
            _userInterfaceSystem.TryCloseAll(uid, GasPressurePumpUiKey.Key);
        }

        private void OnPumpActivate(EntityUid uid, GasPressurePumpComponent pump, ActivateInWorldEvent args)
        {
            if (!EntityManager.TryGetComponent(args.User, out ActorComponent? actor))
                return;

            if (Transform(uid).Anchored)
            {
                _userInterfaceSystem.TryOpen(uid, GasPressurePumpUiKey.Key, actor.PlayerSession);
                DirtyUI(uid, pump);
            }
            else
            {
                _popupSystem.PopupCursor(Loc.GetString("comp-gas-pump-ui-needs-anchor"), args.User);
            }

            args.Handled = true;
        }

        private void OnToggleStatusMessage(EntityUid uid, GasPressurePumpComponent pump, GasPressurePumpToggleStatusMessage args)
        {
            pump.Enabled = args.Enabled;
            _adminLogger.Add(LogType.AtmosPowerChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Session.AttachedEntity!.Value):player} set the power on {ToPrettyString(uid):device} to {args.Enabled}");
            DirtyUI(uid, pump);
            UpdateAppearance(uid, pump);
        }

        private void OnOutputPressureChangeMessage(EntityUid uid, GasPressurePumpComponent pump, GasPressurePumpChangeOutputPressureMessage args)
        {
            pump.TargetPressure = Math.Clamp(args.Pressure, 0f, Atmospherics.MaxOutputPressure);
            _adminLogger.Add(LogType.AtmosPressureChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Session.AttachedEntity!.Value):player} set the pressure on {ToPrettyString(uid):device} to {args.Pressure}kPa");
            DirtyUI(uid, pump);

        }

        private void DirtyUI(EntityUid uid, GasPressurePumpComponent? pump)
        {
            if (!Resolve(uid, ref pump))
                return;

            _userInterfaceSystem.TrySetUiState(uid, GasPressurePumpUiKey.Key,
                new GasPressurePumpBoundUserInterfaceState(EntityManager.GetComponent<MetaDataComponent>(uid).EntityName, pump.TargetPressure, pump.Enabled));
        }

        private void UpdateAppearance(EntityUid uid, GasPressurePumpComponent? pump = null, AppearanceComponent? appearance = null)
        {
            if (!Resolve(uid, ref pump, ref appearance, false))
                return;

            _appearance.SetData(uid, PumpVisuals.Enabled, pump.Enabled, appearance);
        }
    }
}
