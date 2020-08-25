﻿using Content.Server.Atmos;
using Content.Server.GameObjects.Components.NodeContainer;
using Content.Server.GameObjects.Components.NodeContainer.Nodes;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.ViewVariables;
using System.Linq;

namespace Content.Server.GameObjects.Components.Atmos.Piping
{
    /// <summary>
    ///     Transfers gas from a <see cref="PipeNode"/> to the tile it is on.
    /// </summary>
    public abstract class BaseVentComponent : PipeNetDeviceComponent
    {
        [ViewVariables]
        private PipeNode _ventInlet;

        private AtmosphereSystem _atmosSystem;

        public override void Initialize()
        {
            base.Initialize();
            _atmosSystem = EntitySystem.Get<AtmosphereSystem>();
            _ventInlet = Owner.GetComponent<NodeContainerComponent>().Nodes.OfType<PipeNode>().FirstOrDefault();
        }

        public override void Update()
        {
            if (_ventInlet == null)
                return;
            var tileAtmos = AtmosHelpers.GetTileAtmosphere(Owner.Transform.GridPosition);
            if (tileAtmos == null)
                return;
            VentGas(_ventInlet.Air, tileAtmos.Air);
            _atmosSystem.GetGridAtmosphere(Owner.Transform.GridID).Invalidate(tileAtmos.GridIndices);
        }

        protected abstract void VentGas(GasMixture inletGas, GasMixture outletGas);
    }
}
