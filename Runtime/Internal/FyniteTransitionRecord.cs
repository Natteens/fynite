namespace Fynite
{
    internal readonly struct FyniteTransitionRecord<TContext> where TContext : class
    {
        internal readonly int From;
        internal readonly int To;
        internal readonly IPredicate<TContext> Predicate;

        internal FyniteTransitionRecord(int from, int to, IPredicate<TContext> predicate)
        {
            From = from;
            To = to;
            Predicate = predicate;
        }
    }
}
