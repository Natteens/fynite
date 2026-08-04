namespace Fynite
{
    /// <summary>
    /// The answer a lookup gives. <c>Found</c> says whether the other two mean anything, so nothing
    /// has to reserve an index to stand for "nothing matched".
    /// </summary>
    internal readonly struct FyniteMatch
    {
        internal static readonly FyniteMatch None = default;

        internal readonly bool Found;
        internal readonly int From;
        internal readonly int To;

        internal FyniteMatch(int from, int to)
        {
            Found = true;
            From = from;
            To = to;
        }
    }
}
