﻿using System;
using System.Linq;
using Content.Server.GameObjects.Components.Body.Circulatory;
using Content.Server.GameObjects.Components.Body.Respiratory;
using Content.Server.Utility;
using Content.Shared.Chemistry;
using Content.Shared.GameObjects.Components.Chemistry;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server.GameObjects.Components.Chemistry
{
    /// <summary>
    /// Handles messages from an <see cref="AreaEffectComponent"/> to implement smoke behavior to its owner.
    /// </summary>
    [RegisterComponent]
    public class SmokeComponent : Component
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public override string Name => "Smoke";

        public override void HandleMessage(ComponentMessage message, IComponent component)
        {
            base.HandleMessage(message, component);
            switch (message)
            {
                case AreaEffectSpreadMessage spreadMessage:
                    HandleSpreadMessage(spreadMessage);
                    break;
                case AreaEffectReactMessage reactMessage:
                    HandleReactMessage(reactMessage);
                    break;
                case AreaEffectKillMessage:
                    HandleKillMessage();
                    break;
            }
        }

        private void HandleSpreadMessage(AreaEffectSpreadMessage spreadMessage)
        {
            foreach (var smokeEntity in spreadMessage.Spawned)
            {
                if (smokeEntity.TryGetComponent(out SmokeComponent smokeComp) &&
                    Owner.TryGetComponent(out SolutionContainerComponent contents))
                {
                    var solution = contents.Solution.Clone();
                    smokeComp.TryAddSolution(solution);
                }
            }
        }

        private void HandleReactMessage(AreaEffectReactMessage reactMessage)
        {
            var averageExposures = reactMessage.AverageExposures;

            if (!Owner.TryGetComponent(out SolutionContainerComponent contents))
                return;

            var mapGrid = _mapManager.GetGrid(Owner.Transform.GridID);
            var tile = mapGrid.GetTileRef(Owner.Transform.Coordinates.ToVector2i(Owner.EntityManager, _mapManager));

            var solutionFraction = 1 / Math.Floor(averageExposures);

            foreach (var reagentQuantity in contents.ReagentList.ToArray())
            {
                if (reagentQuantity.Quantity == ReagentUnit.Zero) continue;
                var reagent = _prototypeManager.Index<ReagentPrototype>(reagentQuantity.ReagentId);

                // React with the tile the smoke is on
                reagent.ReactionTile(tile, reagentQuantity.Quantity * solutionFraction);

                // Touch every entity on the tile
                foreach (var entity in tile.GetEntitiesInTileFast())
                {
                    reagent.ReactionEntity(entity, ReactionMethod.Touch, reagentQuantity.Quantity * solutionFraction);
                }
            }

            // Enter the bloodstream of every entity without internals
            foreach (var entity in tile.GetEntitiesInTileFast())
            {
                if (!entity.TryGetComponent(out BloodstreamComponent bloodstream))
                    continue;

                if (entity.TryGetComponent(out InternalsComponent internals) &&
                    internals.AreInternalsWorking())
                    continue;

                var cloneSolution = contents.Solution.Clone();
                var transferAmount = ReagentUnit.Min(cloneSolution.TotalVolume * solutionFraction, bloodstream.EmptyVolume);
                var transferSolution = cloneSolution.SplitSolution(transferAmount);

                foreach (var reagentQuantity in transferSolution.Contents.ToArray())
                {
                    if (reagentQuantity.Quantity == ReagentUnit.Zero) continue;
                    var reagent = _prototypeManager.Index<ReagentPrototype>(reagentQuantity.ReagentId);
                    transferSolution.RemoveReagent(reagentQuantity.ReagentId,reagent.ReactionEntity(entity, ReactionMethod.Ingestion, reagentQuantity.Quantity));
                }

                bloodstream.TryTransferSolution(transferSolution);
            }
        }

        private void HandleKillMessage()
        {
            if (Owner.Deleted)
                return;
            Owner.Delete();
        }

        public void TryAddSolution(Solution solution)
        {
            if (solution.TotalVolume == 0)
                return;

            if (!Owner.TryGetComponent(out SolutionContainerComponent contents))
                return;

            var addSolution = solution.SplitSolution(ReagentUnit.Min(solution.TotalVolume, contents.EmptyVolume));

            var result = contents.TryAddSolution(addSolution);

            if (!result)
                return;

            if (Owner.TryGetComponent(out AppearanceComponent appearance))
            {
                appearance.SetData(SmokeVisuals.Color, contents.Color);
            }
        }
    }
}
