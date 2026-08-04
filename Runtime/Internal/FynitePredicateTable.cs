namespace Fynite
{
    /// <summary>
    /// The predicate transitions of a machine, compiled once. The local ones are packed per state, so
    /// the start and count arrays only mean anything together with the array they index.
    /// </summary>
    internal sealed class FynitePredicateTable<TContext> where TContext : class
    {
        private readonly FyniteTransitionRecord<TContext>[] global;
        private readonly FyniteTransitionRecord<TContext>[] local;
        private readonly int[] localStart;
        private readonly int[] localCount;

        internal FynitePredicateTable(
            FyniteTransitionRecord<TContext>[] global,
            FyniteTransitionRecord<TContext>[] local,
            int[] localStart,
            int[] localCount)
        {
            this.global = global;
            this.local = local;
            this.localStart = localStart;
            this.localCount = localCount;
        }

        internal FyniteMatch MatchGlobal(TContext context)
            => Match(context, global, 0, global.Length);

        internal FyniteMatch MatchLocal(TContext context, int state)
        {
            var start = localStart[state];
            return Match(context, local, start, start + localCount[state]);
        }

        private static FyniteMatch Match(
            TContext context,
            FyniteTransitionRecord<TContext>[] records,
            int start,
            int end)
        {
            for (var i = start; i < end; i++)
            {
                if (records[i].Predicate.Evaluate(context))
                {
                    return new FyniteMatch(records[i].From, records[i].To);
                }
            }

            return FyniteMatch.None;
        }
    }
}
