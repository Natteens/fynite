namespace Fynite
{
    /// <summary>A compiled event transition. <c>Source</c> indexes the distinct sources of a machine.</summary>
    internal readonly struct FyniteEventRecord
    {
        internal readonly int From;
        internal readonly int To;
        internal readonly int Source;

        internal FyniteEventRecord(int from, int to, int source)
        {
            From = from;
            To = to;
            Source = source;
        }
    }
}
