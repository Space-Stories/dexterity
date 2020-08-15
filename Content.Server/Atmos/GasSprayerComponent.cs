﻿using System;
using Content.Server.GameObjects.Components.Chemistry;
using Content.Server.Interfaces;
using Content.Shared.Atmos;
using Content.Shared.Chemistry;
using Content.Shared.GameObjects.Components.Pointing;
using Content.Shared.Interfaces.GameObjects.Components;
using Microsoft.CodeAnalysis;
using Robust.Server.GameObjects;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;


namespace Content.Server.Atmos
{
    [RegisterComponent]
    public class GasSprayerComponent : Component, IAfterInteract
    {
#pragma warning disable 649
        [Dependency] private readonly IServerNotifyManager _notifyManager = default!;
        [Dependency] private readonly IServerEntityManager _serverEntityManager = default!;
#pragma warning restore 649

        public override string Name => "GasSprayer";

        private string _spraySound;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref _spraySound, "spraySound", string.Empty);
        }


        public void AfterInteract(AfterInteractEventArgs eventArgs)
        {
            if (Owner.TryGetComponent(out SolutionComponent tank) &&
                tank.Solution.GetReagentQuantity("chem.H2O").Float().Equals(0f))
            {
                //TODO: Parameterize to use object prototype's name
                _notifyManager.PopupMessage(Owner, eventArgs.User,
                    Loc.GetString("The Extinguisher is out of water!", Owner));
            }
            else
            {
                tank.TryRemoveReagent("chem.H2O", ReagentUnit.New(50));

                var playerPos = eventArgs.User.Transform.GridPosition;
                var direction = (eventArgs.ClickLocation.Position - playerPos.Position).Normalized;
                playerPos.Offset(direction);

                var spray = _serverEntityManager.SpawnEntity("ExtinguisherSpray", playerPos);

                spray.GetComponent<AppearanceComponent>()
                    .SetData(RoguePointingArrowVisuals.Rotation, direction.ToAngle().Degrees);
                if (spray.TryGetComponent<GasVaporComponent>(out GasVaporComponent air))
                {
                    air.Air = new GasMixture(200){Temperature = Atmospherics.T20C};
                    air.Air.SetMoles(Gas.WaterVapor,20);
                }


                //Todo: Parameterize into prototype
                spray.GetComponent<GasVaporComponent>().StartMove(direction, 5);
                EntitySystem.Get<AudioSystem>().PlayFromEntity(_spraySound, Owner);
            }
        }
    }
}
