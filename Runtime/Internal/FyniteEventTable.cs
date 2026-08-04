namespace Fynite
{
    /// <summary>
    /// The event transitions of a machine, compiled once and keyed by the slot of the source that was
    /// published. Packed exactly like <see cref="FynitePredicateTable{TContext}"/>.
    /// </summary>
    internal sealed class FyniteEventTable
    {
        private readonly FyniteEventRecord[] global;
        private readonly FyniteEventRecord[] local;
        private readonly int[] localStart;
        private readonly int[] localCount;

        internal FyniteEventTable(
            FyniteEventRecord[] global,
            FyniteEventRecord[] local,
            int[] localStart,
            int[] localCount)
        {
            this.global = global;
            this.local = local;
            this.localStart = localStart;
            this.localCount = localCount;
        }

        internal FyniteMatch MatchGlobal(int source)
            => Match(global, 0, global.Length, source);

        internal FyniteMatch MatchLocal(int source, int state)
        {
            var start = localStart[state];
            return Match(local, start, start + localCount[state], source);
        }

        private static FyniteMatch Match(FyniteEventRecord[] records, int start, int end, int source)
        {
            for (var i = start; i < end; i++)
            {
                if (records[i].Source == source)
                {
                    return new FyniteMatch(records[i].From, records[i].To);
                }
            }

            return FyniteMatch.None;
        }
    }
}
