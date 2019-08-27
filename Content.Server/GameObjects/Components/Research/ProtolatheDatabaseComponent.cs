using System;
using System.Collections.Generic;
using Content.Shared.GameObjects;
using Content.Shared.GameObjects.Components.Research;
using Content.Shared.Research;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.GameObjects.Components.Research
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedLatheDatabaseComponent))]
    public class ProtolatheDatabaseComponent : SharedProtolatheDatabaseComponent
    {
        public override string Name => "ProtolatheDatabase";

        public override ComponentState GetComponentState()
        {
            return new ProtolatheDatabaseState(GetRecipeIdList());
        }

        public void Sync()
        {
            if (!Owner.TryGetComponent(out TechnologyDatabaseComponent database)) return;

            var prototypeManager = IoCManager.Resolve<IPrototypeManager>();

            foreach (var technology in database.Technologies)
            {
                foreach (var id in technology.UnlockedRecipes)
                {
                    var recipe = (LatheRecipePrototype)prototypeManager.Index(typeof(LatheRecipePrototype), id);
                    UnlockRecipe(recipe);
                }
            }

            Dirty();
        }

        public bool UnlockRecipe(LatheRecipePrototype recipe)
        {
            if (!ProtolatheRecipes.Contains(recipe)) return false;

            AddRecipe(recipe);

            return true;
        }
    }
}
