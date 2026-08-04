using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fynite
{
    /// <summary>
    /// A running machine. Driven by the Unity PlayerLoop; there is no manual update entry point.
    /// </summary>
    public sealed class FyniteMachine<TContext> : IDisposable, IFyniteTickable, IFyniteEventSink
        where TContext : class
    {
        private FyniteDefinition<TContext> definition;
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

        // The sources this machine is subscribed to, one entry per subscription, plus the ring buffer
        // of the ones that were published and are still waiting for a safe update.
        private FyniteEvent[] eventSources;
        private int[] eventQueue;
        private bool[] eventPending;
        private int eventHead;
        private int eventCount;

        internal FyniteMachine(Object owner, TContext context, FyniteDefinition<TContext> definition)
        {
            this.owner = owner;
            this.context = context;
            this.definition = definition;

            time = new FyniteTimeSource();

            var capacity = definition.Hierarchy.PathCapacity;
            activePath = new int[capacity];
            pathBuffer = new int[capacity];

            var sourceCount = definition.EventSources.Length;
            eventQueue = new int[sourceCount];
            eventPending = new bool[sourceCount];

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
        /// the PlayerLoop and rejects any later use. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            if (status == FyniteMachineStatus.Disposed)
            {
                return;
            }

            var wasRunning = status == FyniteMachineStatus.Running;
            status = FyniteMachineStatus.Disposed;

            try
            {
                if (wasRunning)
                {
                    ExitActivePath();
                }
            }
            finally
            {
                FyniteLoop.Unregister(this);
                Release();
            }
        }

        internal void Launch(int startIndex)
        {
            try
            {
                Subscribe();

                var states = definition.States;
                for (var i = 0; i < states.Length; i++)
                {
                    states[i].Bind(context, time);
                }

                for (var i = 0; i < states.Length; i++)
                {
                    states[i].BuildActivity(context);
                }

                status = FyniteMachineStatus.Running;
                activeCount = ResolvePath(startIndex, activePath);

                for (var i = 0; i < activeCount; i++)
                {
                    states[activePath[i]].InvokeEnter();
                }
            }
            catch
            {
                status = FyniteMachineStatus.Faulted;
                Release();
                throw;
            }

            FyniteLoop.Register(this);
        }

        private void Subscribe()
        {
            var sources = definition.EventSources;

            // Assigned before the loop so a failure halfway through is still undone by Release.
            eventSources = sources;

            for (var i = 0; i < sources.Length; i++)
            {
                sources[i].Subscribe(this, i);
            }
        }

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

                if (!switched && EvaluateTransitions(out var from, out var to) && !Switch(from, to))
                {
                    return;
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

        /// <summary>
        /// Called by every publication of a source this machine listens to, including from inside a
        /// callback and while the owner is disabled. It only marks the source as waiting: a source
        /// already waiting stays a single occurrence, so repeated publications never grow the queue.
        /// </summary>
        void IFyniteEventSink.Signal(int slot)
        {
            var pending = eventPending;
            if (pending == null || pending[slot])
            {
                return;
            }

            pending[slot] = true;

            var tail = eventHead + eventCount;
            if (tail >= eventQueue.Length)
            {
                tail -= eventQueue.Length;
            }

            eventQueue[tail] = slot;
            eventCount++;
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
            var budget = eventCount;

            while (budget > 0)
            {
                budget--;

                var slot = Dequeue();
                if (!MatchEvent(slot, out var from, out var to))
                {
                    // An event nobody is listening for in the active path is an occurrence that passed:
                    // it is spent here rather than kept waiting for some later state.
                    continue;
                }

                switched = true;
                return Switch(from, to);
            }

            return true;
        }

        private int Dequeue()
        {
            var slot = eventQueue[eventHead];

            eventHead++;
            if (eventHead == eventQueue.Length)
            {
                eventHead = 0;
            }

            eventCount--;
            eventPending[slot] = false;
            return slot;
        }

        private bool MatchEvent(int slot, out int from, out int to)
        {
            var global = definition.GlobalEvents;
            if (Match(global, 0, global.Length, slot, out from, out to))
            {
                return true;
            }

            for (var i = activeCount - 1; i >= 0; i--)
            {
                var state = activePath[i];
                var start = definition.LocalEventStart[state];

                if (Match(
                        definition.LocalEvents,
                        start,
                        start + definition.LocalEventCount[state],
                        slot,
                        out from,
                        out to))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Match(
            FyniteEventRecord[] records,
            int start,
            int end,
            int slot,
            out int from,
            out int to)
        {
            for (var i = start; i < end; i++)
            {
                if (records[i].Source != slot)
                {
                    continue;
                }

                from = records[i].From;
                to = records[i].To;
                return true;
            }

            from = FyniteTransitions<TContext>.AnyState;
            to = FyniteTransitions<TContext>.AnyState;
            return false;
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

        private bool EvaluateTransitions(out int from, out int to)
        {
            if (Evaluate(definition.Global, 0, definition.Global.Length, out from, out to))
            {
                return true;
            }

            for (var i = activeCount - 1; i >= 0; i--)
            {
                var state = activePath[i];
                var start = definition.LocalStart[state];

                if (Evaluate(definition.Local, start, start + definition.LocalCount[state], out from, out to))
                {
                    return true;
                }
            }

            return false;
        }

        private bool Evaluate(
            FyniteTransitionRecord<TContext>[] records,
            int start,
            int end,
            out int from,
            out int to)
        {
            for (var i = start; i < end; i++)
            {
                if (!records[i].Predicate.Evaluate(context))
                {
                    continue;
                }

                from = records[i].From;
                to = records[i].To;
                return true;
            }

            from = FyniteTransitions<TContext>.AnyState;
            to = FyniteTransitions<TContext>.AnyState;
            return false;
        }

        private void ExitActivePath()
        {
            var states = definition.States;
            while (activeCount > 0)
            {
                activeCount--;
                states[activePath[activeCount]].InvokeExit();
            }
        }

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
            status = FyniteMachineStatus.Disposed;

            try
            {
                ExitActivePath();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                FyniteLoop.Unregister(this);
                Release();
            }
        }

        private void Release()
        {
            if (eventSources != null)
            {
                for (var i = 0; i < eventSources.Length; i++)
                {
                    eventSources[i].Unsubscribe(this);
                }

                eventSources = null;
            }

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
            eventQueue = null;
            eventPending = null;
            eventHead = 0;
            eventCount = 0;
            definition = null;
            context = null;
            time = null;
            owner = null;
            ownerBehaviour = null;
            ownerGameObject = null;
        }
    }
}
