using System.Collections.Generic;
using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.Atmos;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Server.NodeContainer.Nodes
{
    /// <summary>
    ///     Connects with other <see cref="PipeNode"/>s whose <see cref="PipeDirection"/>
    ///     correctly correspond.
    /// </summary>
    [DataDefinition]
    public class PipeNode : Node, IGasMixtureHolder, IRotatableNode
    {
        private PipeDirection _connectedDirections;

        /// <summary>
        ///     The directions in which this pipe can connect to other pipes around it.
        /// </summary>
        [ViewVariables]
        [DataField("pipeDirection")]
        private PipeDirection _originalPipeDirection;

        /// <summary>
        ///     The *current* pipe directions (accounting for rotation)
        ///     Used to check if this pipe can connect to another pipe in a given direction.
        /// </summary>
        public PipeDirection CurrentPipeDirection { get; private set; }

        private HashSet<PipeNode>? _alwaysReachable;

        public void AddAlwaysReachable(PipeNode pipeNode)
        {
            if (pipeNode.NodeGroupID != NodeGroupID) return;
            _alwaysReachable ??= new();
            _alwaysReachable.Add(pipeNode);

            if (NodeGroup != null)
                EntitySystem.Get<NodeGroupSystem>().QueueRemakeGroup((BaseNodeGroup) NodeGroup);
        }

        public void RemoveAlwaysReachable(PipeNode pipeNode)
        {
            if (_alwaysReachable == null) return;

            _alwaysReachable.Remove(pipeNode);

            if (NodeGroup != null)
                EntitySystem.Get<NodeGroupSystem>().QueueRemakeGroup((BaseNodeGroup) NodeGroup);
        }

        /// <summary>
        ///     The directions in which this node is connected to other nodes.
        ///     Used by <see cref="PipeVisualState"/>.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public PipeDirection ConnectedDirections
        {
            get => _connectedDirections;
            private set
            {
                _connectedDirections = value;
                UpdateAppearance();
            }
        }

        /// <summary>
        ///     Whether this node can connect to others or not.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool ConnectionsEnabled
        {
            get => _connectionsEnabled;
            set
            {
                _connectionsEnabled = value;

                if (NodeGroup != null)
                    EntitySystem.Get<NodeGroupSystem>().QueueRemakeGroup((BaseNodeGroup) NodeGroup);
            }
        }

        [DataField("connectionsEnabled")]
        private bool _connectionsEnabled = true;

        public override bool Connectable(IEntityManager entMan, TransformComponent? xform = null)
        {
            return _connectionsEnabled && base.Connectable(entMan, xform);
        }

        [DataField("rotationsEnabled")]
        public bool RotationsEnabled { get; set; } = true;

        /// <summary>
        ///     The <see cref="IPipeNet"/> this pipe is a part of.
        /// </summary>
        [ViewVariables]
        private IPipeNet? PipeNet => (IPipeNet?) NodeGroup;

        /// <summary>
        ///     The gases in this pipe.
        /// </summary>
        [ViewVariables]
        public GasMixture Air
        {
            get => PipeNet?.Air ?? GasMixture.SpaceGas;
            set
            {
                DebugTools.Assert(PipeNet != null);
                PipeNet!.Air = value;
            }
        }

        [ViewVariables]
        [DataField("volume")]
        public float Volume { get; set; } = DefaultVolume;

        private const float DefaultVolume = 200f;

        public override void OnContainerStartup()
        {
            base.OnContainerStartup();
            OnConnectedDirectionsNeedsUpdating();
        }

        public override void OnContainerShutdown()
        {
            base.OnContainerShutdown();
            UpdateAdjacentConnectedDirections();
        }

        public void JoinPipeNet(IPipeNet pipeNet)
        {
            OnConnectedDirectionsNeedsUpdating();
        }

        /// <summary>
        ///     Rotates the <see cref="PipeDirection"/> when the entity is rotated, and re-calculates the <see cref="IPipeNet"/>.
        /// </summary>
        void IRotatableNode.RotateEvent(ref RotateEvent ev)
        {
            OnConnectedDirectionsNeedsUpdating();
            UpdateAppearance();
        }

        public override IEnumerable<Node> GetReachableNodes(TransformComponent xform,
            EntityQuery<NodeContainerComponent> nodeQuery,
            EntityQuery<TransformComponent> xformQuery,
            IMapGrid? grid,
            IEntityManager entMan)
        {
            if (_alwaysReachable != null)
            {
                var remQ = new RemQueue<PipeNode>();
                foreach (var pipe in _alwaysReachable)
                {
                    if (pipe.Deleting)
                    {
                        remQ.Add(pipe);
                    }
                    yield return pipe;
                }

                foreach (var pipe in remQ)
                {
                    _alwaysReachable.Remove(pipe);
                }
            }

            if (!xform.Anchored || grid == null)
                yield break;

            var pos = grid.TileIndicesFor(xform.Coordinates);

            for (var i = 0; i < PipeDirectionHelpers.PipeDirections; i++)
            {
                var pipeDir = (PipeDirection) (1 << i);

                if (!CurrentPipeDirection.HasDirection(pipeDir))
                    continue;

                foreach (var pipe in LinkableNodesInDirection(pos, pipeDir, grid, nodeQuery))
                {
                    yield return pipe;
                }
            }
        }

        /// <summary>
        ///     Gets the pipes that can connect to us from entities on the tile or adjacent in a direction.
        /// </summary>
        private IEnumerable<PipeNode> LinkableNodesInDirection(Vector2i pos, PipeDirection pipeDir, IMapGrid grid,
            EntityQuery<NodeContainerComponent> nodeQuery)
        {
            foreach (var pipe in PipesInDirection(pos, pipeDir, grid, nodeQuery))
            {
                if (pipe.NodeGroupID == NodeGroupID
                    && pipe.CurrentPipeDirection.HasDirection(pipeDir.GetOpposite()))
                {
                    yield return pipe;
                }
            }
        }

        /// <summary>
        ///     Gets the pipes from entities on the tile adjacent in a direction.
        /// </summary>
        protected IEnumerable<PipeNode> PipesInDirection(Vector2i pos, PipeDirection pipeDir, IMapGrid grid,
            EntityQuery<NodeContainerComponent> nodeQuery)
        {
            var offsetPos = pos.Offset(pipeDir.ToDirection());

            foreach (var entity in grid.GetAnchoredEntities(offsetPos))
            {
                if (!nodeQuery.TryGetComponent(entity, out var container))
                    continue;

                foreach (var node in container.Nodes.Values)
                {
                    if (node is PipeNode pipe)
                        yield return pipe;
                }
            }
        }

        /// <summary>
        ///     Updates the <see cref="ConnectedDirections"/> of this and all sorrounding pipes.
        ///     Also updates CurrentPipeDirection.
        /// </summary>
        private void OnConnectedDirectionsNeedsUpdating()
        {
            if (RotationsEnabled)
            {
                CurrentPipeDirection = _originalPipeDirection.RotatePipeDirection(IoCManager.Resolve<IEntityManager>().GetComponent<TransformComponent>(Owner).LocalRotation);
            }
            else
            {
                CurrentPipeDirection = _originalPipeDirection;
            }
            UpdateConnectedDirections();
            UpdateAdjacentConnectedDirections();
            UpdateAppearance();
        }

        /// <summary>
        ///     Checks what directions there are connectable pipes in, to update <see cref="ConnectedDirections"/>.
        /// </summary>
        private void UpdateConnectedDirections()
        {
            ConnectedDirections = PipeDirection.None;

            var entMan = IoCManager.Resolve<IEntityManager>();
            var xform = entMan.GetComponent<TransformComponent>(Owner);
            if (!IoCManager.Resolve<IMapManager>().TryGetGrid(xform.GridID, out var grid))
                return;
            var pos = grid.WorldToTile(xform.WorldPosition);
            var query = entMan.GetEntityQuery<NodeContainerComponent>();

            for (var i = 0; i < PipeDirectionHelpers.AllPipeDirections; i++)
            {
                var pipeDir = (PipeDirection) (1 << i);

                if (!CurrentPipeDirection.HasDirection(pipeDir))
                    continue;

                foreach (var pipe in LinkableNodesInDirection(pos, pipeDir, grid, query))
                {
                    if (pipe.Connectable(entMan) && pipe.NodeGroupID == NodeGroupID)
                    {
                        ConnectedDirections |= pipeDir;
                        break;
                    }
                }
            }
        }

        /// <summary>
        ///     Calls <see cref="UpdateConnectedDirections"/> on all adjacent pipes,
        ///     to update their <see cref="ConnectedDirections"/> when this pipe is changed.
        /// </summary>
        private void UpdateAdjacentConnectedDirections()
        {
            var entMan = IoCManager.Resolve<IEntityManager>();
            var xform = entMan.GetComponent<TransformComponent>(Owner);
            if (!IoCManager.Resolve<IMapManager>().TryGetGrid(xform.GridID, out var grid))
                return;
            var pos = grid.WorldToTile(xform.WorldPosition);
            var query = entMan.GetEntityQuery<NodeContainerComponent>();

            for (var i = 0; i < PipeDirectionHelpers.PipeDirections; i++)
            {
                var pipeDir = (PipeDirection) (1 << i);

                foreach (var pipe in LinkableNodesInDirection(pos, pipeDir, grid, query))
                {
                    pipe.UpdateConnectedDirections();
                    pipe.UpdateAppearance();
                }
            }
        }

        /// <summary>
        ///     Updates the <see cref="AppearanceComponent"/>.
        ///     Gets the combined <see cref="ConnectedDirections"/> of every pipe on this entity, so the visualizer on this entity can draw the pipe connections.
        /// </summary>
        private void UpdateAppearance()
        {
            if (!IoCManager.Resolve<IEntityManager>().TryGetComponent(Owner, out AppearanceComponent? appearance)
                || !IoCManager.Resolve<IEntityManager>().TryGetComponent(Owner, out NodeContainerComponent? container))
                return;

            var netConnectedDirections = PipeDirection.None;

            foreach (var node in container.Nodes.Values)
            {
                if (node is PipeNode pipe)
                {
                    netConnectedDirections |= pipe.ConnectedDirections;
                }
            }

            appearance.SetData(PipeVisuals.VisualState, netConnectedDirections);
        }
    }
}
