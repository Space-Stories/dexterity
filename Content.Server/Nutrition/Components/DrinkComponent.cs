using System.Linq;
using System.Threading.Tasks;
using Content.Server.Body.Behavior;
using Content.Server.Fluids.Components;
using Content.Shared.Audio;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Solution.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Helpers;
using Content.Shared.Notification.Managers;
using Content.Shared.Nutrition.Components;
using Content.Shared.Sound;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Server.Nutrition.Components
{
    [RegisterComponent]
    public class DrinkComponent : Component, IUse, IAfterInteract, IExamine, ILand
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        public override string Name => "Drink";

        int IAfterInteract.Priority => 10;

        [ViewVariables]
        private bool _opened;

        [ViewVariables]
        [DataField("useSound")]
        private SoundSpecifier _useSound = new SoundPathSpecifier("/Audio/Items/drink.ogg");

        [ViewVariables]
        [DataField("isOpen")]
        private bool _defaultToOpened;

        [ViewVariables(VVAccess.ReadWrite)]
        public ReagentUnit TransferAmount { get; [UsedImplicitly] private set; } = ReagentUnit.New(5);

        [ViewVariables(VVAccess.ReadWrite)]
        public bool Opened
        {
            get => _opened;
            set
            {
                if (_opened == value)
                {
                    return;
                }

                _opened = value;
                OpenedChanged();
            }
        }

        [ViewVariables] public bool Empty => B();

        private bool B()
        {
            var drainAvailable = EntitySystem.Get<SolutionContainerSystem>()
                .DrainAvailable(Owner);
            return drainAvailable <= 0;
        }

        [DataField("openSounds")]
        private SoundSpecifier _openSounds = new SoundCollectionSpecifier("canOpenSounds");
        [DataField("pressurized")]
        private bool _pressurized = default;
        [DataField("burstSound")]
        private SoundSpecifier _burstSound = new SoundPathSpecifier("/Audio/Effects/flash_bang.ogg");

        protected override void Initialize()
        {
            base.Initialize();

            Opened = _defaultToOpened;
            UpdateAppearance();
        }

        private void OpenedChanged()
        {
            var solutionSys = EntitySystem.Get<SolutionContainerSystem>();
            if (!solutionSys.TryGetSolution(Owner, "drink", out _))
            {
                return;
            }

            if (Opened)
            {
                solutionSys.AddRefillable(Owner, "drink");
                solutionSys.AddDrainable(Owner, "drink");
            }
            else
            {
                solutionSys.RemoveRefillable(Owner);
                solutionSys.RemoveDrainable(Owner);
            }
        }

        // TODO move to DrinkSystem
        public void UpdateAppearance()
        {
            if (!Owner.TryGetComponent(out AppearanceComponent? appearance) ||
                !EntitySystem.Get<SolutionContainerSystem>().HasSolution(Owner))
            {
                return;
            }

            var drainAvailable = EntitySystem.Get<SolutionContainerSystem>().DrainAvailable(Owner);
            appearance.SetData(SharedFoodComponent.FoodVisuals.Visual, drainAvailable.Float());
        }

        bool IUse.UseEntity(UseEntityEventArgs args)
        {
            if (!Opened)
            {
                //Do the opening stuff like playing the sounds.
                SoundSystem.Play(Filter.Pvs(args.User), _openSounds.GetSound(), args.User, AudioParams.Default);

                Opened = true;
                return false;
            }

            if (!EntitySystem.Get<SolutionContainerSystem>().HasSolution(Owner) ||
                EntitySystem.Get<SolutionContainerSystem>().DrainAvailable(Owner) <= 0)
            {
                args.User.PopupMessage(Loc.GetString("drink-component-on-use-is-empty", ("owner", Owner)));
                return true;
            }

            return TryUseDrink(args.User, args.User);
        }

        //Force feeding a drink to someone.
        async Task<bool> IAfterInteract.AfterInteract(AfterInteractEventArgs eventArgs)
        {
            if (eventArgs.Target == null)
            {
                return false;
            }

            return TryUseDrink(eventArgs.User, eventArgs.Target, true);
        }

        public void Examine(FormattedMessage message, bool inDetailsRange)
        {
            if (!Opened || !inDetailsRange)
            {
                return;
            }

            var color = Empty ? "gray" : "yellow";
            var openedText =
                Loc.GetString(Empty ? "drink-component-on-examine-is-empty" : "drink-component-on-examine-is-opened");
            message.AddMarkup(Loc.GetString("drink-component-on-examine-details-text", ("colorName", color), ("text", openedText)));
        }

        private bool TryUseDrink(IEntity user, IEntity target, bool forced = false)
        {
            if (!Opened)
            {
                target.PopupMessage(Loc.GetString("drink-component-try-use-drink-not-open", ("owner", Owner)));
                return false;
            }

            if (!EntitySystem.Get<SolutionContainerSystem>().TryGetDrainableSolution(Owner, out var interactions) ||
                interactions.DrainAvailable <= 0)
            {
                if (!forced)
                {
                    target.PopupMessage(Loc.GetString("drink-component-try-use-drink-is-empty", ("entity", Owner)));
                }

                return false;
            }

            if (!target.TryGetComponent(out SharedBodyComponent? body) ||
                !body.TryGetMechanismBehaviors<StomachBehavior>(out var stomachs))
            {
                target.PopupMessage(Loc.GetString("drink-component-try-use-drink-cannot-drink", ("owner", Owner)));
                return false;
            }


            if (user != target &&
                !user.InRangeUnobstructed(target, popup: true))
            {
                return false;
            }

            var solutionContainerSystem = EntitySystem.Get<SolutionContainerSystem>();
            var transferAmount = ReagentUnit.Min(TransferAmount, interactions.DrainAvailable);
            var drain = solutionContainerSystem.Drain(interactions, transferAmount);
            var firstStomach = stomachs.FirstOrDefault(stomach => stomach.CanTransferSolution(drain));

            // All stomach are full or can't handle whatever solution we have.
            if (firstStomach == null)
            {
                target.PopupMessage(Loc.GetString("drink-component-try-use-drink-had-enough", ("owner", Owner)));

                if (!interactions.Owner.HasComponent<RefillableSolutionComponent>())
                {
                    drain.SpillAt(target, "PuddleSmear");
                    return false;
                }

                solutionContainerSystem.Refill(interactions, drain);
                return false;
            }

            SoundSystem.Play(Filter.Pvs(target), _useSound.GetSound(), target, AudioParams.Default.WithVolume(-2f));

            target.PopupMessage(Loc.GetString("drink-component-try-use-drink-success-slurp"));
            UpdateAppearance();

            // TODO: Account for partial transfer.

            drain.DoEntityReaction(target, ReactionMethod.Ingestion);

            firstStomach.TryTransferSolution(drain);

            return true;
        }

        void ILand.Land(LandEventArgs eventArgs)
        {
            if (_pressurized &&
                !Opened &&
                _random.Prob(0.25f) &&
                EntitySystem.Get<SolutionContainerSystem>().TryGetDrainableSolution(Owner, out var interactions))
            {
                Opened = true;

                var solution = EntitySystem.Get<SolutionContainerSystem>()
                    .Drain(interactions, interactions.DrainAvailable);
                solution.SpillAt(Owner, "PuddleSmear");

                SoundSystem.Play(Filter.Pvs(Owner), _burstSound.GetSound(), Owner, AudioParams.Default.WithVolume(-4));
            }
        }
    }
}
