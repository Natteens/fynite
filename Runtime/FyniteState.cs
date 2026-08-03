using System;

namespace Fynite
{
    /// <summary>
    /// Behaviour of a single state. Concrete states need a parameterless constructor and are
    /// instantiated once per machine.
    /// </summary>
    public abstract class FyniteState<TContext> where TContext : class
    {
        private TContext context;
        private FyniteTimeSource time;

        protected TContext Context => context;

        protected float DeltaTime => time == null ? 0f : time.Delta;

        protected float FixedDeltaTime => time == null ? 0f : time.FixedDelta;

        protected virtual void Enter()
        {
        }

        protected virtual void Update()
        {
        }

        protected virtual void FixedUpdate()
        {
        }

        protected virtual void Exit()
        {
        }

        internal void Bind(TContext boundContext, FyniteTimeSource timeSource)
        {
            if (context != null)
            {
                throw new InvalidOperationException(
                    $"Fynite: state '{GetType().Name}' is already bound to a machine.");
            }

            context = boundContext;
            time = timeSource;
        }

        internal void Unbind()
        {
            context = null;
            time = null;
        }

        internal void InvokeEnter() => Enter();

        internal void InvokeUpdate() => Update();

        internal void InvokeFixedUpdate() => FixedUpdate();

        internal void InvokeExit() => Exit();
    }
}
