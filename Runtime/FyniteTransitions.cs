using System;
using System.Collections.Generic;

namespace Fynite
{
    /// <summary>
    /// Collects the transitions of a machine. States referenced by <c>From</c>, <c>To</c> and
    /// <c>Start</c> are registered automatically. A module never creates one: it receives the
    /// machine's own instance in <see cref="IFyniteTransitions{TContext}.Configure"/>.
    /// </summary>
    public sealed class FyniteTransitions<TContext> where TContext : class
    {
        internal const int AnyState = -1;

        private readonly Dictionary<Type, int> indexByType = new Dictionary<Type, int>();
        private readonly List<FyniteState<TContext>> states = new List<FyniteState<TContext>>();
        private readonly List<Type> stateTypes = new List<Type>();
        private readonly List<FyniteTransitionRecord<TContext>> records =
            new List<FyniteTransitionRecord<TContext>>();
        private readonly List<FyniteEventBinding<TContext>> events =
            new List<FyniteEventBinding<TContext>>();

        private bool sealedForBuild;

        internal FyniteTransitions()
        {
        }

        public FyniteTransitionSource<TContext> From<TState>()
            where TState : FyniteState<TContext>, new()
            => new FyniteTransitionSource<TContext>(this, Register<TState>());

        /// <summary>
        /// The two states of a transition in one call, for when naming them apart adds nothing.
        /// Identical to <c>From&lt;TFrom&gt;().To&lt;TTo&gt;()</c>.
        /// </summary>
        public FyniteTransitionTarget<TContext> From<TFrom, TTo>()
            where TFrom : FyniteState<TContext>, new()
            where TTo : FyniteState<TContext>, new()
            => new FyniteTransitionTarget<TContext>(this, Register<TFrom>(), Register<TTo>());

        public FyniteTransitionSource<TContext> Any()
            => new FyniteTransitionSource<TContext>(this, AnyState);

        /// <summary>
        /// A transition out of every state, in one call. Identical to <c>Any().To&lt;TTo&gt;()</c>.
        /// </summary>
        public FyniteTransitionTarget<TContext> Any<TTo>()
            where TTo : FyniteState<TContext>, new()
            => new FyniteTransitionTarget<TContext>(this, AnyState, Register<TTo>());

        internal int Register<TState>() where TState : FyniteState<TContext>, new()
        {
            ThrowIfSealed();

            var type = typeof(TState);
            if (indexByType.TryGetValue(type, out var existing))
            {
                return existing;
            }

            var index = states.Count;
            states.Add(new TState());
            stateTypes.Add(type);
            indexByType.Add(type, index);
            return index;
        }

        internal FyniteTransitions<TContext> Add(int from, int to, IPredicate<TContext> predicate)
        {
            ThrowIfSealed();

            if (predicate == null)
            {
                throw new ArgumentNullException(
                    nameof(predicate),
                    $"Fynite: transition from '{DescribeState(from)}' to '{DescribeState(to)}' has no condition.");
            }

            records.Add(new FyniteTransitionRecord<TContext>(from, to, predicate));
            return this;
        }

        internal FyniteTransitions<TContext> AddEvent(int from, int to, Func<TContext, FyniteEvent> source)
        {
            ThrowIfSealed();

            if (source == null)
            {
                throw new ArgumentNullException(
                    nameof(source),
                    $"Fynite: event transition from '{DescribeState(from)}' to '{DescribeState(to)}' " +
                    "has no event source.");
            }

            events.Add(new FyniteEventBinding<TContext>(from, to, source));
            return this;
        }

        internal FyniteDefinition<TContext> Compile(FyniteHierarchyBuilder hierarchy, TContext context)
        {
            sealedForBuild = true;

            var stateCount = states.Count;
            var types = stateTypes.ToArray();
            var layout = hierarchy.Compile(stateCount, types);

            var predicates = CompilePredicates(stateCount);

            var slots = new int[events.Count];
            var sources = ResolveEventSources(context, slots);

            return new FyniteDefinition<TContext>(
                states.ToArray(),
                types,
                layout,
                predicates,
                CompileEvents(stateCount, slots),
                sources);
        }

        /// <summary>
        /// Packs the declared transitions into the rules that apply everywhere and one run per state,
        /// both in declaration order, so matching a state walks its own slice and nothing else.
        /// </summary>
        private FynitePredicateTable<TContext> CompilePredicates(int stateCount)
        {
            var localCount = new int[stateCount];
            var globalCount = 0;

            for (var i = 0; i < records.Count; i++)
            {
                var from = records[i].From;
                if (from == AnyState)
                {
                    globalCount++;
                }
                else
                {
                    localCount[from]++;
                }
            }

            var global = new FyniteTransitionRecord<TContext>[globalCount];
            var local = new FyniteTransitionRecord<TContext>[records.Count - globalCount];
            var localStart = new int[stateCount];

            var offset = 0;
            for (var i = 0; i < stateCount; i++)
            {
                localStart[i] = offset;
                offset += localCount[i];
            }

            var globalCursor = 0;
            var cursors = new int[stateCount];

            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record.From == AnyState)
                {
                    global[globalCursor++] = record;
                    continue;
                }

                local[localStart[record.From] + cursors[record.From]++] = record;
            }

            return new FynitePredicateTable<TContext>(global, local, localStart, localCount);
        }

        /// <summary>
        /// The same packing for the event transitions. <paramref name="slots"/> carries the
        /// subscription slot each declared transition resolved to.
        /// </summary>
        private FyniteEventTable CompileEvents(int stateCount, int[] slots)
        {
            var localCount = new int[stateCount];
            var globalCount = 0;

            for (var i = 0; i < events.Count; i++)
            {
                var from = events[i].From;
                if (from == AnyState)
                {
                    globalCount++;
                }
                else
                {
                    localCount[from]++;
                }
            }

            var global = new FyniteEventRecord[globalCount];
            var local = new FyniteEventRecord[events.Count - globalCount];
            var localStart = new int[stateCount];

            var offset = 0;
            for (var i = 0; i < stateCount; i++)
            {
                localStart[i] = offset;
                offset += localCount[i];
            }

            var globalCursor = 0;
            var cursors = new int[stateCount];

            for (var i = 0; i < events.Count; i++)
            {
                var binding = events[i];
                var record = new FyniteEventRecord(binding.From, binding.To, slots[i]);

                if (record.From == AnyState)
                {
                    global[globalCursor++] = record;
                    continue;
                }

                local[localStart[record.From] + cursors[record.From]++] = record;
            }

            return new FyniteEventTable(global, local, localStart, localCount);
        }

        /// <summary>
        /// Runs every selector exactly once and collapses the results to the distinct sources, by
        /// instance identity. <paramref name="slots"/> receives, per declared event transition, the
        /// index of its source in the returned array.
        /// </summary>
        private FyniteEvent[] ResolveEventSources(TContext context, int[] slots)
        {
            var distinct = new FyniteEvent[events.Count];
            var distinctCount = 0;

            for (var i = 0; i < events.Count; i++)
            {
                var binding = events[i];
                var source = binding.Source(context);

                if (source == null)
                {
                    throw new InvalidOperationException(
                        $"Fynite: event transition from '{DescribeState(binding.From)}' to " +
                        $"'{DescribeState(binding.To)}' resolved to a null event source.");
                }

                var slot = -1;
                for (var j = 0; j < distinctCount; j++)
                {
                    if (ReferenceEquals(distinct[j], source))
                    {
                        slot = j;
                        break;
                    }
                }

                if (slot < 0)
                {
                    slot = distinctCount;
                    distinct[distinctCount++] = source;
                }

                slots[i] = slot;
            }

            var sources = new FyniteEvent[distinctCount];
            Array.Copy(distinct, sources, distinctCount);
            return sources;
        }

        internal string DescribeState(int index)
            => index == AnyState ? "Any" : stateTypes[index].Name;

        private void ThrowIfSealed()
        {
            if (sealedForBuild)
            {
                throw new InvalidOperationException(
                    "Fynite: transitions cannot be changed after the machine has been built.");
            }
        }

        /// <summary>
        /// What the fluent structs raise when they were defaulted into existence rather than handed
        /// out by this class. Lives here so both of them say the same thing.
        /// </summary>
        internal static InvalidOperationException Detached()
            => new InvalidOperationException(
                "Fynite: this transition builder was not created by FyniteTransitions. Start from " +
                "From<TState>(), From<TFrom, TTo>() or Any() on the instance Configure receives.");
    }

    public readonly struct FyniteTransitionSource<TContext> where TContext : class
    {
        private readonly FyniteTransitions<TContext> transitions;
        private readonly int from;

        internal FyniteTransitionSource(FyniteTransitions<TContext> transitions, int from)
        {
            this.transitions = transitions;
            this.from = from;
        }

        public FyniteTransitionTarget<TContext> To<TState>()
            where TState : FyniteState<TContext>, new()
        {
            var owner = Owner();
            return new FyniteTransitionTarget<TContext>(owner, from, owner.Register<TState>());
        }

        /// <summary>
        /// A public struct can always be defaulted into existence, and a defaulted one points at no
        /// machine. Saying that beats the <c>NullReferenceException</c> the field would raise.
        /// </summary>
        private FyniteTransitions<TContext> Owner()
            => transitions ?? throw FyniteTransitions<TContext>.Detached();
    }

    public readonly struct FyniteTransitionTarget<TContext> where TContext : class
    {
        private readonly FyniteTransitions<TContext> transitions;
        private readonly int from;
        private readonly int to;

        internal FyniteTransitionTarget(FyniteTransitions<TContext> transitions, int from, int to)
        {
            this.transitions = transitions;
            this.from = from;
            this.to = to;
        }

        public FyniteTransitions<TContext> When<TPredicate>()
            where TPredicate : IPredicate<TContext>, new()
            => Owner().Add(from, to, new TPredicate());

        /// <param name="predicate">
        /// Asked on every cycle the transition is considered, so it must be side effect free.
        /// </param>
        public FyniteTransitions<TContext> When(Func<TContext, bool> predicate)
        {
            var owner = Owner();

            return owner.Add(
                from,
                to,
                predicate == null ? null : new FyniteDelegatePredicate<TContext>(predicate));
        }

        /// <summary>
        /// Runs this transition when the event happens, instead of when a condition holds.
        /// </summary>
        /// <param name="source">
        /// Points at the event to listen to, as in <c>context =&gt; context.Health.Damaged</c>. Called
        /// exactly once, during <c>Build()</c>, and never again; returning null is an error.
        /// </param>
        public FyniteTransitions<TContext> On(Func<TContext, FyniteEvent> source)
            => Owner().AddEvent(from, to, source);

        /// <summary>
        /// A public struct can always be defaulted into existence, and a defaulted one points at no
        /// machine. Saying that beats the <c>NullReferenceException</c> the field would raise, and it
        /// happens before the delegate is even looked at.
        /// </summary>
        private FyniteTransitions<TContext> Owner()
            => transitions ?? throw FyniteTransitions<TContext>.Detached();
    }
}
