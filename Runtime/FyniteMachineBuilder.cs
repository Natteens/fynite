using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace Fynite
{
    /// <summary>
    /// Configures a machine. Every method throws once <c>Build()</c> has run.
    /// </summary>
    public sealed class FyniteMachineBuilder<TContext> where TContext : class
    {
        private readonly Object owner;
        private readonly TContext context;
        private readonly FyniteTransitions<TContext> transitions = new FyniteTransitions<TContext>();
        private readonly List<IFyniteTransitions<TContext>> modules =
            new List<IFyniteTransitions<TContext>>();

        private int startIndex = -1;
        private bool built;

        internal FyniteMachineBuilder(Object owner, TContext context)
        {
            this.owner = owner;
            this.context = context;
        }

        /// <summary>
        /// Sets the state the machine enters during <c>Build()</c>. Required, and allowed once.
        /// </summary>
        public FyniteMachineBuilder<TContext> Start<TState>()
            where TState : FyniteState<TContext>, new()
        {
            ThrowIfBuilt();

            if (startIndex >= 0)
            {
                throw new InvalidOperationException(
                    $"Fynite: Start<{typeof(TState).Name}>() was called but the initial state is " +
                    $"already '{transitions.DescribeState(startIndex)}'.");
            }

            startIndex = transitions.Register<TState>();
            return this;
        }

        public FyniteMachineBuilder<TContext> Use<TTransitions>()
            where TTransitions : IFyniteTransitions<TContext>, new()
        {
            ThrowIfBuilt();

            modules.Add(new TTransitions());
            return this;
        }

        /// <summary>
        /// Enters the initial state and registers the machine in the Unity PlayerLoop.
        /// </summary>
        public FyniteMachine<TContext> Build()
        {
            ThrowIfBuilt();

            if (startIndex < 0)
            {
                throw new InvalidOperationException(
                    "Fynite: Start<TState>() is required before Build().");
            }

            built = true;

            for (var i = 0; i < modules.Count; i++)
            {
                modules[i].Configure(transitions);
            }

            var machine = new FyniteMachine<TContext>(owner, context, transitions.Compile());
            machine.Launch(startIndex);
            return machine;
        }

        private void ThrowIfBuilt()
        {
            if (built)
            {
                throw new InvalidOperationException(
                    "Fynite: this builder was already built. Create a new one with Machine.Attach().");
            }
        }
    }
}
