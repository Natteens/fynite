using System;

namespace Fynite
{
    /// <summary>
    /// A declared event transition, before <c>Build()</c> resolves its selector against the context.
    /// </summary>
    internal readonly struct FyniteEventBinding<TContext> where TContext : class
    {
        internal readonly int From;
        internal readonly int To;
        internal readonly Func<TContext, FyniteEvent> Source;

        internal FyniteEventBinding(int from, int to, Func<TContext, FyniteEvent> source)
        {
            From = from;
            To = to;
            Source = source;
        }
    }
}
