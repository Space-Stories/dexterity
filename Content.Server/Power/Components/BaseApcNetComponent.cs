#nullable enable
using Content.Server.Power.NodeGroups;

namespace Content.Server.Power.Components
{
    public abstract class BaseApcNetComponent : BaseNetConnectorComponent<IApcNet>
    {
        protected override IApcNet NullNet => ApcNet.NullNet;
    }
}
