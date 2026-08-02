using System;
using Fynite.Diagnostics;

namespace Fynite
{
    /// <summary>
    /// The one place where a compiled graph regains its static context type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything downstream of the compiler is non-generic: the asset, the runner and the runtime graph
    /// data all work without knowing <c>TContext</c>. But <see cref="FyniteBuilder{TContext}"/> and
    /// <see cref="FyniteDefinition{TContext}"/> are generic, so something has to close that generic —
    /// and it cannot be <c>MakeGenericType</c> plus <c>Activator.CreateInstance</c>, because the runtime
    /// is not allowed to construct types reflectively.
    /// </para>
    /// <para>
    /// The compiler creates a <see cref="FyniteContextBinder{TContext}"/> in the editor, where
    /// reflection is fine, and stores it in a <c>[SerializeReference]</c> field. Unity's serializer
    /// records the closed generic type and rebuilds that instance whenever the asset loads — after a
    /// domain reload, after restarting the editor, and in a player build. From then on the binder is an
    /// ordinary virtual call, and every generic operation happens inside a method that already knows
    /// <typeparamref name="TContext" /> statically.
    /// </para>
    /// </remarks>
    [Serializable]
    public abstract class FyniteContextBinder
    {
        /// <summary>Context type the compiled graph executes against.</summary>
        public abstract Type ContextType { get; }

        /// <summary>
        /// Rebuilds the immutable definition from compiled data.
        /// </summary>
        /// <param name="graph">Dense structure produced by the compiler.</param>
        /// <param name="blocks">Prototype per authored block occurrence, indexed by the graph's slots.</param>
        /// <param name="payloads">Payload carrier per signal, index-aligned with <c>graph.signals</c>. Entries may be null.</param>
        /// <exception cref="FyniteDefinitionException">The compiled data does not describe a valid machine.</exception>
        public abstract IFyniteDefinition Build(FyniteRuntimeGraph graph, FyniteBlock[] blocks, FynitePayloadType[] payloads);

        /// <summary>Creates a machine for a definition this binder produced.</summary>
        /// <exception cref="ArgumentException"><paramref name="context"/> is not a <see cref="ContextType"/>.</exception>
        public abstract IFyniteMachine CreateMachine(IFyniteDefinition definition, object context, IFyniteDiagnostics diagnostics);
    }

    /// <summary>The binder for one concrete context type.</summary>
    /// <typeparam name="TContext">Context type the compiled graph executes against.</typeparam>
    [Serializable]
    public sealed class FyniteContextBinder<TContext> : FyniteContextBinder where TContext : class
    {
        /// <inheritdoc />
        public override Type ContextType => typeof(TContext);

        /// <inheritdoc />
        public override IFyniteDefinition Build(FyniteRuntimeGraph graph, FyniteBlock[] blocks, FynitePayloadType[] payloads)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            blocks = blocks ?? Array.Empty<FyniteBlock>();
            var states = graph.states ?? Array.Empty<FyniteRuntimeState>();
            var signals = graph.signals ?? Array.Empty<FyniteRuntimeSignal>();
            var reactions = graph.reactions ?? Array.Empty<FyniteRuntimeReaction>();

            if (states.Length == 0)
            {
                throw new FyniteDefinitionException("The compiled graph declares no states.");
            }

            if ((uint)graph.rootIndex >= (uint)states.Length)
            {
                throw new FyniteDefinitionException(
                    "The compiled graph has no valid root index (" + graph.rootIndex + " of " + states.Length + " states).");
            }

            var builder = new FyniteBuilder<TContext>();

            // The compiler emits states parent-first, so a single forward pass can wire the tree.
            var stateHandles = new StateHandle[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                int parent = states[i].parent;
                if (parent < 0)
                {
                    stateHandles[i] = builder.AddState(states[i].name);
                    continue;
                }

                if (parent >= i)
                {
                    throw new FyniteDefinitionException(
                        "Compiled state '" + states[i].name + "' declares a parent at index " + parent +
                        " which is not earlier in the array; compiled states must be ordered parent-first.");
                }

                stateHandles[i] = builder.AddState(states[i].name, stateHandles[parent]);
            }

            builder.SetRoot(stateHandles[graph.rootIndex]);

            for (int i = 0; i < states.Length; i++)
            {
                int initial = states[i].initialChild;
                if (initial < 0)
                {
                    continue;
                }

                if ((uint)initial >= (uint)states.Length)
                {
                    throw new FyniteDefinitionException(
                        "Compiled state '" + states[i].name + "' declares initial child index " + initial +
                        " which is out of range.");
                }

                builder.SetInitial(stateHandles[i], stateHandles[initial]);
            }

            var signalHandles = new SignalHandle[signals.Length];
            for (int i = 0; i < signals.Length; i++)
            {
                var payload = payloads != null && i < payloads.Length ? payloads[i] : null;
                signalHandles[i] = builder.AddSignal(signals[i].name, payload?.Type);
            }

            for (int i = 0; i < states.Length; i++)
            {
                var slots = builder.State(stateHandles[i]);
                AddActions(slots, states[i].enter, blocks, FynitePhase.Enter, states[i].name);
                AddActions(slots, states[i].tick, blocks, FynitePhase.Tick, states[i].name);
                AddActions(slots, states[i].fixedTick, blocks, FynitePhase.FixedTick, states[i].name);
                AddActions(slots, states[i].exit, blocks, FynitePhase.Exit, states[i].name);
            }

            // Array order is the registration order, which is the last tie-break during resolution.
            for (int i = 0; i < reactions.Length; i++)
            {
                var compiled = reactions[i];
                RequireIndex(compiled.source, states.Length, "reaction source state");
                RequireIndex(compiled.signal, signals.Length, "reaction signal");

                var reaction = builder.State(stateHandles[compiled.source]).On(signalHandles[compiled.signal]);

                if (compiled.target >= 0)
                {
                    RequireIndex(compiled.target, states.Length, "reaction target state");
                    reaction = reaction.TransitionTo(stateHandles[compiled.target]);
                }

                reaction = reaction.Priority(compiled.priority);

                var guards = compiled.guards;
                if (guards != null)
                {
                    for (int g = 0; g < guards.Length; g++)
                    {
                        var prototype = RequireBlock(blocks, guards[g], "guard", states[compiled.source].name);
                        if (!(prototype is IFyniteGuard<TContext>))
                        {
                            throw new FyniteDefinitionException(
                                "Guard block '" + prototype.GetType().FullName + "' on state '" +
                                states[compiled.source].name + "' does not implement IFyniteGuard<" +
                                typeof(TContext).FullName + ">.");
                        }

                        var captured = prototype;
                        reaction = reaction.When(() => (IFyniteGuard<TContext>)captured.CreateBlockInstance());
                    }
                }

                var effects = compiled.effects;
                if (effects != null)
                {
                    for (int e = 0; e < effects.Length; e++)
                    {
                        var prototype = RequireBlock(blocks, effects[e], "effect", states[compiled.source].name);
                        RequireAction(prototype, states[compiled.source].name, FynitePhase.Effect);

                        var captured = prototype;
                        reaction = reaction.Do(() => (IFyniteAction<TContext>)captured.CreateBlockInstance());
                    }
                }
            }

            return builder.Build();
        }

        /// <inheritdoc />
        public override IFyniteMachine CreateMachine(IFyniteDefinition definition, object context, IFyniteDiagnostics diagnostics)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!(definition is FyniteDefinition<TContext> typedDefinition))
            {
                throw new ArgumentException(
                    "Definition was built for '" + definition.ContextType.FullName + "' but this binder is for '" +
                    typeof(TContext).FullName + "'.",
                    nameof(definition));
            }

            if (!(context is TContext typedContext))
            {
                throw new ArgumentException(
                    "Context of type '" + context.GetType().FullName + "' is not a '" + typeof(TContext).FullName + "'.",
                    nameof(context));
            }

            return typedDefinition.CreateMachine(typedContext, diagnostics);
        }

        private static void AddActions(
            FyniteStateBuilder<TContext> slots,
            int[] indices,
            FyniteBlock[] blocks,
            FynitePhase phase,
            string stateName)
        {
            if (indices == null)
            {
                return;
            }

            for (int i = 0; i < indices.Length; i++)
            {
                var prototype = RequireBlock(blocks, indices[i], phase.ToString(), stateName);
                RequireAction(prototype, stateName, phase);

                // The prototype carries the authored configuration; every machine clones its own copy so
                // a stateful block never leaks across entities sharing this definition.
                var captured = prototype;
                Func<IFyniteAction<TContext>> factory = () => (IFyniteAction<TContext>)captured.CreateBlockInstance();

                switch (phase)
                {
                    case FynitePhase.Enter:
                        slots.OnEnter(factory);
                        break;
                    case FynitePhase.Tick:
                        slots.OnTick(factory);
                        break;
                    case FynitePhase.FixedTick:
                        slots.OnFixedTick(factory);
                        break;
                    case FynitePhase.Exit:
                        slots.OnExit(factory);
                        break;
                    default:
                        throw new FyniteDefinitionException("Phase " + phase + " cannot hold state blocks.");
                }
            }
        }

        private static FyniteBlock RequireBlock(FyniteBlock[] blocks, int index, string slot, string stateName)
        {
            if ((uint)index >= (uint)blocks.Length)
            {
                throw new FyniteDefinitionException(
                    "A " + slot + " block of state '" + stateName + "' points at prototype index " + index +
                    " but the compiled graph only has " + blocks.Length + " prototypes.");
            }

            var prototype = blocks[index];
            if (prototype == null)
            {
                throw new FyniteDefinitionException(
                    "The " + slot + " block at prototype index " + index + " of state '" + stateName +
                    "' is missing. Its type was most likely deleted or renamed after the graph was compiled; " +
                    "reopen the graph and recompile it.");
            }

            return prototype;
        }

        private static void RequireAction(FyniteBlock prototype, string stateName, FynitePhase phase)
        {
            if (!(prototype is IFyniteAction<TContext>))
            {
                throw new FyniteDefinitionException(
                    "Block '" + prototype.GetType().FullName + "' used as a " + phase + " block of state '" +
                    stateName + "' does not implement IFyniteAction<" + typeof(TContext).FullName + ">.");
            }
        }

        private static void RequireIndex(int index, int count, string what)
        {
            if ((uint)index >= (uint)count)
            {
                throw new FyniteDefinitionException(
                    "Compiled graph refers to " + what + " index " + index + " which is out of range (" + count + ").");
            }
        }
    }
}
