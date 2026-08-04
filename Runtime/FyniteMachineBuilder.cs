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
        private readonly FyniteHierarchyBuilder hierarchy = new FyniteHierarchyBuilder();

        private int startIndex = -1;
        private bool built;

        internal FyniteMachineBuilder(Object owner, TContext context)
        {
            this.owner = owner;
            this.context = context;
        }

        /// <summary>
        /// Sets the top level state the machine enters during <c>Build()</c>. Required, allowed once,
        /// and rejected at <c>Build()</c> if that state was declared as a child of another one.
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

        /// <summary>
        /// Declares <typeparamref name="TChild"/> as a direct child of <typeparamref name="TParent"/>.
        /// Both states are registered. The first child declared for a parent becomes its initial child,
        /// entered right after the parent; later declarations do not replace it.
        /// </summary>
        public FyniteMachineBuilder<TContext> Child<TParent, TChild>()
            where TParent : FyniteState<TContext>, new()
            where TChild : FyniteState<TContext>, new()
        {
            ThrowIfBuilt();

            hierarchy.Relate(
                transitions.Register<TParent>(),
                typeof(TParent),
                transitions.Register<TChild>(),
                typeof(TChild));

            return this;
        }

        /// <summary>
        /// Overrides which child of <typeparamref name="TParent"/> is entered with it. Only needed to
        /// depart from the first-declared convention; <typeparamref name="TChild"/> must be a direct
        /// child of <typeparamref name="TParent"/> by the time <c>Build()</c> runs.
        /// </summary>
        public FyniteMachineBuilder<TContext> InitialChild<TParent, TChild>()
            where TParent : FyniteState<TContext>, new()
            where TChild : FyniteState<TContext>, new()
        {
            ThrowIfBuilt();

            hierarchy.SetInitialChild(
                transitions.Register<TParent>(),
                transitions.Register<TChild>());

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
        /// Validates the hierarchy, enters the initial state together with its initial children and
        /// registers the machine in the Unity PlayerLoop.
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

            var definition = transitions.Compile(hierarchy, context);
            var startParent = definition.Hierarchy.Parent[startIndex];

            if (startParent >= 0)
            {
                var startName = definition.StateTypes[startIndex].Name;
                throw new InvalidOperationException(
                    $"Fynite: Start<{startName}>() is invalid because {startName} is a child of " +
                    $"{definition.StateTypes[startParent].Name}.");
            }

            var machine = new FyniteMachine<TContext>(owner, context, definition);
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
