﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Damage;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs;
using Content.Shared.Stunnable;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.DoAfter;

public abstract class SharedDoAfterSystem : EntitySystem
{
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        // We cache the list as to not allocate every update tick...
        private readonly Queue<DoAfter> _pending = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DoAfterComponent, DamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<DoAfterComponent, MobStateChangedEvent>(OnStateChanged);
            SubscribeLocalEvent<DoAfterComponent, ComponentGetState>(OnDoAfterGetState);
        }

        public void Add(DoAfterComponent component, DoAfter doAfter)
        {
            doAfter.ID = component.RunningIndex;
            doAfter.Delay = doAfter.EventArgs.Delay;
            component.DoAfters.Add(component.RunningIndex, doAfter);
            EnsureComp<ActiveDoAfterComponent>(component.Owner);
            component.RunningIndex++;
            Dirty(component);
        }

        private void OnDoAfterGetState(EntityUid uid, DoAfterComponent component, ref ComponentGetState args)
        {
            args.State = new DoAfterComponentState(component.DoAfters);
        }

        public void Cancelled(DoAfterComponent component, DoAfter doAfter)
        {
            if (!component.DoAfters.TryGetValue(doAfter.ID, out var index))
                return;

            component.DoAfters.Remove(doAfter.ID);

            if (component.DoAfters.Count == 0)
                RemComp<ActiveDoAfterComponent>(component.Owner);

            RaiseNetworkEvent(new CancelledDoAfterMessage(component.Owner, index.ID));
        }

        /// <summary>
        ///     Call when the particular DoAfter is finished.
        ///     Client should be tracking this independently.
        /// </summary>
        public void Finished(DoAfterComponent component, DoAfter doAfter)
        {
            if (!component.DoAfters.ContainsKey(doAfter.ID))
                return;

            component.DoAfters.Remove(doAfter.ID);

            if (component.DoAfters.Count == 0)
                RemComp<ActiveDoAfterComponent>(component.Owner);
        }

        private void OnStateChanged(EntityUid uid, DoAfterComponent component, MobStateChangedEvent args)
        {
            if (args.NewMobState != MobState.Dead || args.NewMobState != MobState.Critical)
                return;

            foreach (var (_, doAfter) in component.DoAfters)
            {
                Cancel(doAfter);
            }
        }

        /// <summary>
        /// Cancels DoAfter if it breaks on damage and it meets the threshold
        /// </summary>
        /// <param name="_">
        /// The EntityUID of the user
        /// </param>
        /// <param name="component"></param>
        /// <param name="args"></param>
        public void OnDamage(EntityUid _, DoAfterComponent component, DamageChangedEvent args)
        {
            if (!args.InterruptsDoAfters || !args.DamageIncreased || args.DamageDelta == null)
                return;

            foreach (var (_, doAfter) in component.DoAfters)
            {
                if (doAfter.EventArgs.BreakOnDamage && args.DamageDelta?.Total.Float() > doAfter.EventArgs.DamageThreshold)
                    Cancel(doAfter);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var (_, comp) in EntityManager.EntityQuery<ActiveDoAfterComponent, DoAfterComponent>())
            {
                foreach (var (_, doAfter) in comp.DoAfters.ToArray())
                {
                    Run(doAfter);

                    switch (doAfter.Status)
                    {
                        case DoAfterStatus.Running:
                            break;
                        case DoAfterStatus.Cancelled:
                            _pending.Enqueue(doAfter);
                            break;
                        case DoAfterStatus.Finished:
                            _pending.Enqueue(doAfter);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                while (_pending.TryDequeue(out var doAfter))
                {
                    if (doAfter.Status == DoAfterStatus.Cancelled && doAfter.Done != null)
                    {
                        Cancelled(comp, doAfter);
                        doAfter.Done(true);
                    }

                    if (doAfter.Status == DoAfterStatus.Finished && doAfter.Done != null)
                    {
                        Finished(comp, doAfter);
                        doAfter.Done(false);
                    }
                }
            }
        }

        /// <summary>
        ///     Tasks that are delayed until the specified time has passed
        ///     These can be potentially cancelled by the user moving or when other things happen.
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <returns></returns>
        public async Task<DoAfterStatus> WaitDoAfter(DoAfterEventArgs eventArgs)
        {
            var doAfter = CreateDoAfter(eventArgs);

            await doAfter.AsTask;

            return doAfter.Status;
        }



        /// <summary>
        ///     Creates a DoAfter without waiting for it to finish. You can use events with this.
        ///     These can be potentially cancelled by the user moving or when other things happen.
        ///     Use this when you need to send extra data with the DoAfter
        /// </summary>
        /// <param name="eventArgs">The DoAfterEventArgs</param>
        /// <param name="data">The extra data sent over </param>
        public void DoAfter<T>(DoAfterEventArgs eventArgs, T data)
        {
            var doAfter = CreateDoAfter(eventArgs);

            doAfter.Done = cancelled =>
            {
                Send(data, cancelled, eventArgs);
            };
        }

        /// <summary>
        ///     Creates a DoAfter without waiting for it to finish. You can use events with this.
        ///     These can be potentially cancelled by the user moving or when other things happen.
        ///     Use this if you don't have any extra data to send with the DoAfter
        /// </summary>
        /// <param name="eventArgs">The DoAfterEventArgs</param>
        public void DoAfter(DoAfterEventArgs eventArgs)
        {
            var doAfter = CreateDoAfter(eventArgs);

            doAfter.Done = cancelled =>
            {
                Send(cancelled, eventArgs);
            };
        }

        private DoAfter CreateDoAfter(DoAfterEventArgs eventArgs)
        {
            // Setup
            eventArgs.CancelToken = new CancellationToken();
            var doAfter = new DoAfter(eventArgs, EntityManager);
            // Caller's gonna be responsible for this I guess
            var doAfterComponent = Comp<DoAfterComponent>(eventArgs.User);
            doAfter.ID = doAfterComponent.RunningIndex;
            doAfter.StartTime = _gameTiming.CurTime;
            Add(doAfterComponent, doAfter);
            return doAfter;
        }

        private void Run(DoAfter doAfter)
        {
            switch (doAfter.Status)
            {
                case DoAfterStatus.Running:
                    break;
                case DoAfterStatus.Cancelled:
                case DoAfterStatus.Finished:
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            doAfter.Elapsed = _gameTiming.CurTime - doAfter.StartTime;

            if (IsFinished(doAfter))
            {
                if (!TryPostCheck(doAfter))
                    doAfter.Tcs.SetResult(DoAfterStatus.Cancelled);
                else
                    doAfter.Tcs.SetResult(DoAfterStatus.Finished);
                return;
            }

            if (IsCancelled(doAfter))
                doAfter.Tcs.SetResult(DoAfterStatus.Cancelled);
        }

        private bool TryPostCheck(DoAfter doAfter)
        {
            return doAfter.EventArgs.PostCheck?.Invoke() != false;
        }

        private bool IsFinished(DoAfter doAfter)
        {
            var delay = TimeSpan.FromSeconds(doAfter.EventArgs.Delay);

            if (doAfter.Elapsed <= delay)
                return false;

            return true;
        }

        private bool IsCancelled(DoAfter doAfter)
        {
            var eventArgs = doAfter.EventArgs;
            var xForm = GetEntityQuery<TransformComponent>();

            if (!Exists(eventArgs.User) || eventArgs.Target is {} target && !Exists(target))
                return true;

            if (eventArgs.CancelToken.IsCancellationRequested)
                return true;

            //TODO: Handle Inertia in space
            if (eventArgs.BreakOnUserMove && !xForm.GetComponent(eventArgs.User).Coordinates.InRange(EntityManager, doAfter.UserGrid, eventArgs.MovementThreshold))
                return true;

            if (eventArgs.Target != null && eventArgs.BreakOnTargetMove && !xForm.GetComponent(eventArgs.Target!.Value).Coordinates.InRange(EntityManager, doAfter.TargetGrid, eventArgs.MovementThreshold))
                return true;

            if (eventArgs.ExtraCheck != null && !eventArgs.ExtraCheck.Invoke())
                return true;

            if (eventArgs.BreakOnStun && HasComp<StunnedComponent>(eventArgs.User))
                return true;

            if (eventArgs.NeedHand)
            {
                if (!TryComp<SharedHandsComponent>(eventArgs.User, out var handsComp))
                {
                    //TODO: Figure out active hand and item values

                    // If we had a hand but no longer have it that's still a paddlin'
                    if (doAfter.ActiveHand != null)
                        return true;
                }
                else
                {
                    var currentActiveHand = handsComp.ActiveHand?.Name;
                    if (doAfter.ActiveHand != currentActiveHand)
                        return true;

                    var currentItem = handsComp.ActiveHandEntity;
                    if (doAfter.ActiveItem != currentItem)
                        return true;
                }
            }

            if (eventArgs.DistanceThreshold != null)
            {
                var userXform = xForm.GetComponent(eventArgs.User);

                if (eventArgs.Target != null && !eventArgs.User.Equals(eventArgs.Target))
                {
                    //recalculate Target location in case Target has also moved
                    var targetCoords = xForm.GetComponent(eventArgs.Target.Value).Coordinates;
                    if (!userXform.Coordinates.InRange(EntityManager, targetCoords, eventArgs.DistanceThreshold.Value))
                        return true;
                }

                if (eventArgs.Used != null)
                {
                    var usedCoords = xForm.GetComponent(eventArgs.Used.Value).Coordinates;
                    if (!userXform.Coordinates.InRange(EntityManager, usedCoords, eventArgs.DistanceThreshold.Value))
                        return true;
                }
            }

            return false;
        }

        public void Cancel(DoAfter doAfter)
        {
            if (doAfter.Status == DoAfterStatus.Running)
                doAfter.Tcs.SetResult(DoAfterStatus.Cancelled);
        }

        /// <summary>
        /// Send the DoAfter event, used where you don't need any extra data to send.
        /// </summary>
        /// <param name="cancelled"></param>
        /// <param name="args"></param>
        public void Send(bool cancelled, DoAfterEventArgs args)
        {
            var ev = new DoAfterEvent(cancelled, args);

            if (EntityManager.EntityExists(args.User))
                RaiseLocalEvent(args.User, ev, args.Broadcast);

            if (args.Target is {} target && EntityManager.EntityExists(target))
                RaiseLocalEvent(target, ev, args.Broadcast);

            if (args.Used is {} used && EntityManager.EntityExists(used))
                RaiseLocalEvent(used, ev, args.Broadcast);
        }

        /// <summary>
        /// Send the DoAfter event, used where you need extra data to send
        /// </summary>
        /// <param name="data"></param>
        /// <param name="cancelled"></param>
        /// <param name="args"></param>
        /// <typeparam name="T"></typeparam>
        public void Send<T>(T data, bool cancelled, DoAfterEventArgs args)
        {
            var ev = new DoAfterEvent<T>(data, cancelled, args);

            if (EntityManager.EntityExists(args.User))
                RaiseLocalEvent(args.User, ev, args.Broadcast);

            if (args.Target is {} target && EntityManager.EntityExists(target))
                RaiseLocalEvent(target, ev, args.Broadcast);

            if (args.Used is {} used && EntityManager.EntityExists(used))
                RaiseLocalEvent(used, ev, args.Broadcast);
        }
}
