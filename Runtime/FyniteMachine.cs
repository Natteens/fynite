using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fynite
{
    /// <summary>
    /// A running machine. Driven by the Unity PlayerLoop; there is no manual update entry point.
    /// </summary>
    public sealed class FyniteMachine<TContext> : IDisposable, IFyniteTickable where TContext : class
    {
        private FyniteDefinition<TContext> definition;
        private FyniteTimeSource time;
        private TContext context;
        private Object owner;
        private Behaviour ownerBehaviour;
        private GameObject ownerGameObject;

        private int current;
        private FyniteMachineStatus status;
        private bool inCycle;
        private int loopSlot = FyniteLoop.Unregistered;

        internal FyniteMachine(Object owner, TContext context, FyniteDefinition<TContext> definition)
        {
            this.owner = owner;
            this.context = context;
            this.definition = definition;

            time = new FyniteTimeSource();

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

        public Type CurrentStateType
            => status == FyniteMachineStatus.Running ? definition.StateTypes[current] : null;

        public bool IsIn<TState>() where TState : FyniteState<TContext>
            => status == FyniteMachineStatus.Running && definition.StateTypes[current] == typeof(TState);

        /// <summary>
        /// Runs <c>Exit</c> on the active state once, unregisters from the PlayerLoop and rejects any
        /// later use. Safe to call more than once.
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
                    definition.States[current].InvokeExit();
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
            current = startIndex;

            var states = definition.States;
            for (var i = 0; i < states.Length; i++)
            {
                states[i].Bind(context, time);
            }

            status = FyniteMachineStatus.Running;

            try
            {
                states[current].InvokeEnter();
            }
            catch
            {
                status = FyniteMachineStatus.Faulted;
                Release();
                throw;
            }

            FyniteLoop.Register(this);
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
                var target = EvaluateTransitions();
                if (target >= 0)
                {
                    definition.States[current].InvokeExit();
                    if (status != FyniteMachineStatus.Running)
                    {
                        return;
                    }

                    current = target;
                    definition.States[current].InvokeEnter();
                    if (status != FyniteMachineStatus.Running)
                    {
                        return;
                    }
                }

                definition.States[current].InvokeUpdate();
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
                definition.States[current].InvokeFixedUpdate();
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

        private int EvaluateTransitions()
        {
            var target = Evaluate(definition.Global, 0, definition.Global.Length);
            if (target >= 0)
            {
                return target;
            }

            var start = definition.LocalStart[current];
            return Evaluate(definition.Local, start, start + definition.LocalCount[current]);
        }

        private int Evaluate(FyniteTransitionRecord<TContext>[] records, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                if (records[i].Predicate.Evaluate(context))
                {
                    return records[i].To;
                }
            }

            return -1;
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
                definition.States[current].InvokeExit();
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
            if (definition != null)
            {
                var states = definition.States;
                for (var i = 0; i < states.Length; i++)
                {
                    states[i].Unbind();
                }
            }

            definition = null;
            context = null;
            time = null;
            owner = null;
            ownerBehaviour = null;
            ownerGameObject = null;
        }
    }
}
