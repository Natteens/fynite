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
        private FyniteActivityPlan<TContext> activity;

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

        /// <summary>
        /// Declares what this state does over time, as a chain of steps. Called once, during
        /// <c>Build()</c>. The chain starts when the state is entered, is cancelled when it is left,
        /// and starts over on the next entry. States that have nothing to sequence leave this alone.
        /// </summary>
        protected virtual void ConfigureActivity(FyniteActivityBuilder<TContext> activity)
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

        /// <summary>
        /// Compiles the chain declared by <see cref="ConfigureActivity"/>, once. Leaves the state
        /// without an activity when nothing was declared.
        /// </summary>
        internal void BuildActivity(TContext boundContext)
        {
            var builder = new FyniteActivityBuilder<TContext>(GetType());
            ConfigureActivity(builder);
            activity = builder.Compile(boundContext);
        }

        internal void Unbind()
        {
            activity?.Cancel();
            activity = null;
            context = null;
            time = null;
        }

        internal void InvokeEnter()
        {
            Enter();
            activity?.Reset();
        }

        internal void InvokeUpdate()
        {
            Update();
            activity?.Tick(context, DeltaTime);
        }

        internal void InvokeFixedUpdate() => FixedUpdate();

        internal void InvokeExit()
        {
            activity?.Cancel();
            Exit();
        }
    }
}
