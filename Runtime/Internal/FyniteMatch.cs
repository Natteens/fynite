namespace Fynite
{
    /// <summary>
    /// The answer to "does anything here fire?". <c>Found</c> says whether the other two mean
    /// anything, so a lookup needs no out-of-band index to stand for "nothing matched".
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
