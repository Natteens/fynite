using System;
using System.Runtime.ExceptionServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fynite
{
    /// <summary>
    /// A running machine. Driven by the Unity PlayerLoop; there is no manual update entry point.
    /// </summary>
    public sealed class FyniteMachine<TContext> : IDisposable, IFyniteTickable where TContext : class
    {
        /// <summary>
        /// Why a machine is stopping. Every reason runs the same steps; it only decides who owns the
        /// loop registry at that moment, and who gets told when an <c>Exit</c> fails.
        /// </summary>
        private enum StopReason
        {
            /// <summary>Explicit <c>Dispose</c>. The failing Exit is the caller's to see.</summary>
            Disposed,

            /// <summary>The owner went away. Nobody is left to catch anything, so failures are logged.</summary>
            OwnerDestroyed,

            /// <summary>The loop is resetting. It has already emptied its registry.</summary>
            LoopReset,

            /// <summary>The build never finished. The build's own failure is the one that matters.</summary>
            LaunchFailed
        }

        private FyniteDefinition<TContext> definition;
        private FyniteEventInbox inbox;
        private FyniteTimeSource time;
        private TContext context;
        private Object owner;
        private Behaviour ownerBehaviour;
        private GameObject ownerGameObject;

        private int[] activePath;
        private int[] pathBuffer;
        private int activeCount;
        private FyniteMachineStatus status;
        private bool inCycle;
        private int loopSlot = FyniteLoop.Unregistered;

        internal FyniteMachine(Object owner, TContext context, FyniteDefinition<TContext> definition)
        {
            this.owner = owner;
            this.context = context;
            this.definition = definition;

            time = new FyniteTimeSource();
            inbox = new FyniteEventInbox(definition.EventSources);

            var capacity = definition.Hierarchy.PathCapacity;
            activePath = new int[capacity];
            pathBuffer = new int[capacity];

            ownerBehaviour = owner as Behaviour;
            if (ownerBehaviour != null)
            {
                ownerGameObject = ownerBehaviour.gameObject;
            }
            else if (owner is Component component)
            {
                ownerGameObject = component.gameObject;
            }
            else
            {
                ownerGameObject = owner as GameObject;
            }
        }

        public bool IsRunning => status == FyniteMachineStatus.Running;

        /// <summary>
        /// The deepest active state: the leaf of the active path, or the single active state of a flat
        /// machine.
        /// </summary>
        public Type CurrentStateType
            => status == FyniteMachineStatus.Running
                ? definition.StateTypes[activePath[activeCount - 1]]
                : null;

        /// <summary>
        /// True for every state on the active path, so a superstate still answers true while one of its
        /// children is the current state.
        /// </summary>
        public bool IsIn<TState>() where TState : FyniteState<TContext>
        {
            if (status != FyniteMachineStatus.Running)
            {
                return false;
            }

            var type = typeof(TState);
            for (var i = 0; i < activeCount; i++)
            {
                if (definition.StateTypes[activePath[i]] == type)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Runs <c>Exit</c> once on every active state, from the leaf up to the top, unregisters from
        /// the PlayerLoop and rejects any later use. Safe to call more than once. An <c>Exit</c> that
        /// throws does not stop the states above it from being left, and the exception still reaches
        /// the caller.
        /// </summary>
        public void Dispose()
        {
            if (status == FyniteMachineStatus.Disposed)
            {
                return;
            }

            var failure = Stop(StopReason.Disposed);
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        /// <summary>
        /// Starts the machine, or leaves nothing behind. Every step is undone if a later one fails, and
        /// the machine is only handed to the loop once it is fully entered — and only if the loop is
        /// still the one the build began in.
        /// </summary>
        internal void Launch(int startIndex, int buildGeneration)
        {
            try
            {
                // A reset during Configure or Compile already invalidated this build; stop before any
                // Enter runs.
                RequireCurrentLoop(buildGeneration);

                inbox.Open();
                BindStates();

                // Binding runs ConfigureActivity, which is user code and could have reset the loop.
                RequireCurrentLoop(buildGeneration);

                status = FyniteMachineStatus.Running;
                EnterInitialPath(startIndex);

                // Enter is user code and may itself have reset the loop.
                if (!FyniteLoop.TryRegister(this, buildGeneration))
                {
                    throw StaleBuild();
                }
            }
            catch
            {
                AbortLaunch();
                throw;
            }
        }

        private void BindStates()
        {
            var states = definition.States;

            for (var i = 0; i < states.Length; i++)
            {
                states[i].Bind(context, time);
            }

            for (var i = 0; i < states.Length; i++)
            {
                states[i].BuildActivity(context);
            }
        }

        private void EnterInitialPath(int startIndex)
        {
            var states = definition.States;
            var count = ResolvePath(startIndex, activePath);

            // The path grows one state at a time so that a failing Enter leaves behind exactly the
            // states that did enter — every one of which is then given its Exit.
            for (var i = 0; i < count; i++)
            {
                activeCount = i + 1;
                states[activePath[i]].InvokeEnter();
            }

            activeCount = count;
        }

        private void AbortLaunch()
        {
            var failure = Stop(StopReason.LaunchFailed);
            if (failure != null)
            {
                // Secondary to whatever made the build fail, which is the exception being propagated.
                Debug.LogException(failure);
            }
        }

        private static void RequireCurrentLoop(int buildGeneration)
        {
            if (!FyniteLoop.IsCurrent(buildGeneration))
            {
                throw StaleBuild();
            }
        }

        private static InvalidOperationException StaleBuild()
            => new InvalidOperationException(
                "Fynite: the PlayerLoop was reset while this machine was being built, so the machine " +
                "was not started.");

        internal void Tick(float deltaTime)
        {
            if (!CanTick())
            {
                return;
            }

            inCycle = true;
            time.Delta = deltaTime;

            try
            {
                if (!ProcessEvents(out var switched))
                {
                    return;
                }

                if (!switched)
                {
                    var match = MatchPredicate();
                    if (match.Found && !Switch(match.From, match.To))
                    {
                        return;
                    }
                }

                for (var i = 0; i < activeCount; i++)
                {
                    definition.States[activePath[i]].InvokeUpdate();
                    if (status != FyniteMachineStatus.Running)
                    {
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                Fault(exception);
            }
            finally
            {
                inCycle = false;
            }
        }

        internal void TickFixed(float fixedDeltaTime)
        {
            if (!CanTick())
            {
                return;
            }

            inCycle = true;
            time.FixedDelta = fixedDeltaTime;

            try
            {
                for (var i = 0; i < activeCount; i++)
                {
                    definition.States[activePath[i]].InvokeFixedUpdate();
                    if (status != FyniteMachineStatus.Running)
                    {
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                Fault(exception);
            }
            finally
            {
                inCycle = false;
            }
        }

        int IFyniteTickable.LoopSlot
        {
            get => loopSlot;
            set => loopSlot = value;
        }

        void IFyniteTickable.LoopUpdate(float deltaTime) => Tick(deltaTime);

        void IFyniteTickable.LoopFixedUpdate(float fixedDeltaTime) => TickFixed(fixedDeltaTime);

        void IFyniteTickable.LoopShutdown()
        {
            if (status == FyniteMachineStatus.Disposed)
            {
                return;
            }

            var failure = Stop(StopReason.LoopReset);
            if (failure != null)
            {
                // Reported, not thrown: the reset still has other machines to end.
                Debug.LogException(failure);
            }
        }

        /// <summary>
        /// Consumes the events that were waiting when the cycle began and runs the transition the
        /// first matching one asks for. Returns false when the machine stopped running while
        /// switching, and reports through <paramref name="switched"/> whether predicates still have to
        /// be evaluated this cycle.
        /// </summary>
        private bool ProcessEvents(out bool switched)
        {
            switched = false;

            // Only what was already waiting is consumed, so anything published from a callback of this
            // very cycle waits for the next one instead of being handled recursively.
            var budget = inbox.PendingCount;

            while (budget > 0)
            {
                budget--;

                var match = MatchEvent(inbox.Dequeue());
                if (!match.Found)
                {
                    // An event nobody is listening for in the active path is an occurrence that passed:
                    // it is spent here rather than kept waiting for some later state.
                    continue;
                }

                switched = true;
                return Switch(match.From, match.To);
            }

            return true;
        }

        private FyniteMatch MatchEvent(int source)
        {
            var events = definition.Events;

            var match = events.MatchGlobal(source);
            if (match.Found)
            {
                return match;
            }

            for (var i = activeCount - 1; i >= 0; i--)
            {
                match = events.MatchLocal(source, activePath[i]);
                if (match.Found)
                {
                    return match;
                }
            }

            return FyniteMatch.None;
        }

        private FyniteMatch MatchPredicate()
        {
            var predicates = definition.Predicates;

            var match = predicates.MatchGlobal(context);
            if (match.Found)
            {
                return match;
            }

            for (var i = activeCount - 1; i >= 0; i--)
            {
                match = predicates.MatchLocal(context, activePath[i]);
                if (match.Found)
                {
                    return match;
                }
            }

            return FyniteMatch.None;
        }

        private bool CanTick()
        {
            if (status != FyniteMachineStatus.Running || inCycle)
            {
                return false;
            }

            if (owner == null)
            {
                DisposeDetachedOwner();
                return false;
            }

            if (ownerBehaviour != null)
            {
                return ownerBehaviour.isActiveAndEnabled;
            }

            return ownerGameObject == null || ownerGameObject.activeInHierarchy;
        }

        private int ResolvePath(int target, int[] buffer)
        {
            var hierarchy = definition.Hierarchy;
            var count = hierarchy.Depth[target] + 1;

            var node = target;
            for (var i = count - 1; i >= 0; i--)
            {
                buffer[i] = node;
                node = hierarchy.Parent[node];
            }

            var child = hierarchy.InitialChild[target];
            while (child >= 0)
            {
                buffer[count++] = child;
                child = hierarchy.InitialChild[child];
            }

            return count;
        }

        private bool Switch(int from, int to)
        {
            var states = definition.States;
            var count = ResolvePath(to, pathBuffer);

            var limit = count < activeCount ? count : activeCount;
            var common = 0;
            while (common < limit && activePath[common] == pathBuffer[common])
            {
                common++;
            }

            // An explicit self transition, and any transition back into the current leaf, re-enters the
            // target instead of stopping at it.
            if (from == to || to == activePath[activeCount - 1])
            {
                var depth = definition.Hierarchy.Depth[to];
                if (common > depth)
                {
                    common = depth;
                }
            }

            while (activeCount > common)
            {
                activeCount--;
                states[activePath[activeCount]].InvokeExit();
                if (status != FyniteMachineStatus.Running)
                {
                    return false;
                }
            }

            for (var i = common; i < count; i++)
            {
                activePath[i] = pathBuffer[i];
                activeCount = i + 1;
                states[activePath[i]].InvokeEnter();
                if (status != FyniteMachineStatus.Running)
                {
                    return false;
                }
            }

            activeCount = count;
            return true;
        }

        /// <summary>
        /// The one way a machine stops. Leaves the active path leaf first — which cancels each activity
        /// before its <c>Exit</c> — then drops every subscription and lets go of everything it held.
        /// Returns the first <c>Exit</c> failure, or null.
        /// </summary>
        private Exception Stop(StopReason reason)
        {
            status = reason == StopReason.LaunchFailed
                ? FyniteMachineStatus.Faulted
                : FyniteMachineStatus.Disposed;

            Exception failure;

            try
            {
                failure = ExitEnteredStates();
            }
            finally
            {
                // A loop reset has already emptied its registry; reaching into it from here would
                // fight the sweep that is running.
                if (reason != StopReason.LoopReset)
                {
                    FyniteLoop.Unregister(this);
                }

                Release();
            }

            return failure;
        }

        /// <summary>
        /// Leaves every state that was entered, leaf first. A failing <c>Exit</c> never costs the
        /// states above it their own: the first failure is returned for the caller to report or
        /// rethrow, and any later one is logged so it is not lost behind it.
        /// </summary>
        private Exception ExitEnteredStates()
        {
            if (definition == null)
            {
                return null;
            }

            var states = definition.States;
            Exception first = null;

            while (activeCount > 0)
            {
                activeCount--;

                try
                {
                    states[activePath[activeCount]].InvokeExit();
                }
                catch (Exception exception)
                {
                    if (first == null)
                    {
                        first = exception;
                    }
                    else
                    {
                        Debug.LogException(exception);
                    }
                }
            }

            return first;
        }

        /// <summary>
        /// Stops without leaving the active path. A machine that threw is in an unknown state, so
        /// running more of its code is not safe; it is reported and released instead.
        /// </summary>
        private void Fault(Exception exception)
        {
            if (status == FyniteMachineStatus.Running)
            {
                status = FyniteMachineStatus.Faulted;
                FyniteLoop.Unregister(this);
            }

            Debug.LogException(exception, owner);
            Release();
        }

        private void DisposeDetachedOwner()
        {
            var failure = Stop(StopReason.OwnerDestroyed);
            if (failure != null)
            {
                Debug.LogException(failure);
            }
        }

        private void Release()
        {
            inbox?.Close();

            if (definition != null)
            {
                var states = definition.States;
                for (var i = 0; i < states.Length; i++)
                {
                    states[i].Unbind();
                }
            }

            activeCount = 0;
            activePath = null;
            pathBuffer = null;
            inbox = null;
            definition = null;
            context = null;
            time = null;
            owner = null;
            ownerBehaviour = null;
            ownerGameObject = null;
        }
    }
}
