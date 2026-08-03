using System;
using System.Collections.Generic;

namespace Fynite
{
    /// <summary>
    /// Collects the transitions of a machine. States referenced by <c>From</c>, <c>To</c> and
    /// <c>Start</c> are registered automatically.
    /// </summary>
    public sealed class FyniteTransitions<TContext> where TContext : class
    {
        internal const int AnyState = -1;

        private readonly Dictionary<Type, int> indexByType = new Dictionary<Type, int>();
        private readonly List<FyniteState<TContext>> states = new List<FyniteState<TContext>>();
        private readonly List<Type> stateTypes = new List<Type>();
        private readonly List<FyniteTransitionRecord<TContext>> records =
            new List<FyniteTransitionRecord<TContext>>();

        private bool sealedForBuild;

        public FyniteTransitionSource<TContext> From<TState>()
            where TState : FyniteState<TContext>, new()
            => new FyniteTransitionSource<TContext>(this, Register<TState>());

        public FyniteTransitionSource<TContext> Any()
            => new FyniteTransitionSource<TContext>(this, AnyState);

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

        internal FyniteDefinition<TContext> Compile(FyniteHierarchyBuilder hierarchy)
        {
            sealedForBuild = true;

            var stateCount = states.Count;
            var types = stateTypes.ToArray();
            var layout = hierarchy.Compile(stateCount, types);
            var globalCount = 0;
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i].From == AnyState)
                {
                    globalCount++;
                }
            }

            var global = new FyniteTransitionRecord<TContext>[globalCount];
            var local = new FyniteTransitionRecord<TContext>[records.Count - globalCount];
            var localStart = new int[stateCount];
            var localCount = new int[stateCount];

            for (var i = 0; i < records.Count; i++)
            {
                var from = records[i].From;
                if (from != AnyState)
                {
                    localCount[from]++;
                }
            }

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

            return new FyniteDefinition<TContext>(
                states.ToArray(),
                types,
                layout,
                global,
                local,
                localStart,
                localCount);
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
            => new FyniteTransitionTarget<TContext>(transitions, from, transitions.Register<TState>());
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
            => transitions.Add(from, to, new TPredicate());

        /// <param name="predicate">Prefer a <c>static</c> lambda so no closure is allocated.</param>
        public FyniteTransitions<TContext> When(Func<TContext, bool> predicate)
            => transitions.Add(
                from,
                to,
                predicate == null ? null : new FyniteDelegatePredicate<TContext>(predicate));
    }
}
