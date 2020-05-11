using System.Collections.Generic;
using Content.Server.AI.Operators;
using Content.Server.AI.Operators.Inventory;
using Content.Server.AI.Utility.Considerations;
using Content.Server.AI.Utility.Considerations.Clothing;
using Content.Server.AI.Utility.Considerations.Inventory;
using Content.Server.AI.Utility.Curves;
using Content.Server.AI.WorldState;
using Content.Server.AI.WorldState.States;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.Utility.Actions.Clothing.Head
{
    public sealed class EquipHead : UtilityAction
    {
        private IEntity _entity;

        public EquipHead(IEntity owner, IEntity entity, float weight) : base(owner)
        {
            _entity = entity;
            Bonus = weight;
        }

        public override void SetupOperators(Blackboard context)
        {
            ActionOperators = new Queue<IOperator>(new IOperator[]
            {
                new EquipEntityOperator(Owner, _entity),
                new UseItemInHandsOperator(Owner, _entity),
            });
        }

        protected override void UpdateBlackboard(Blackboard context)
        {
            base.UpdateBlackboard(context);
            context.GetState<TargetEntityState>().SetValue(_entity);
        }

        protected override Consideration[] Considerations { get; } = {
            new ClothingInSlotCon(EquipmentSlotDefines.Slots.HEAD,
                new InverseBoolCurve()),
            new CanPutTargetInHandsCon(
                new BoolCurve()),
        };
    }
}
