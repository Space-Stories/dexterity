﻿using System.Threading.Tasks;
using Content.Shared.Construction;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Server.Construction.Completions
{
    public class DeleteEntity : IEdgeCompleted, IStepCompleted
    {
        public void ExposeData(ObjectSerializer serializer)
        {
        }

        public async Task StepCompleted(IEntity entity, IEntity user)
        {
            await Completed(entity, user);
        }

        public async Task Completed(IEntity entity, IEntity user)
        {
            if (entity.Deleted) return;

            entity.Delete();
        }
    }
}
