using System.Threading.Tasks;
using Content.Shared.Construction.Prototypes;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Construction
{
    [TestFixture]
    public sealed class ConstructionPrototypeTest : ContentIntegrationTest
    {
        [Test]
        public async Task TestStartValid()
        {
            var server = StartServerDummyTicker();

            await server.WaitIdleAsync();

            var protoMan = server.ResolveDependency<IPrototypeManager>();

            foreach (var proto in protoMan.EnumeratePrototypes<ConstructionPrototype>())
            {
                var start = proto.StartNode;
                var graph = protoMan.Index<ConstructionGraphPrototype>(proto.Graph);

                Assert.That(graph.Nodes.ContainsKey(start), $"Found no startNode {start} on graph {graph.ID} for construction prototype {proto.ID}!");
            }
        }

        [Test]
        public async Task TestTargetsValid()
        {
            var server = StartServerDummyTicker();

            await server.WaitIdleAsync();

            var protoMan = server.ResolveDependency<IPrototypeManager>();

            foreach (var proto in protoMan.EnumeratePrototypes<ConstructionPrototype>())
            {
                var target = proto.TargetNode;
                var graph = protoMan.Index<ConstructionGraphPrototype>(proto.Graph);

                Assert.That(graph.Nodes.ContainsKey(target), $"Found no targetNode {target} on graph {graph.ID} for construction prototype {proto.ID}!");
            }
        }
    }
}
