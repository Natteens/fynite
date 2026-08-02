using System.Threading;

namespace Fynite
{
    /// <summary>
    /// Allocates the owner tag stamped into every <see cref="StateHandle"/> and <see cref="SignalHandle"/>.
    /// </summary>
    /// <remarks>
    /// This counter must be non-generic. A <c>static</c> field on <c>FyniteBuilder&lt;TContext&gt;</c>
    /// would give every closed generic type its own sequence, so definitions built for two different
    /// context types would hand out colliding owner tags and stop rejecting each other's handles.
    /// Owners start at one so that a default handle (owner zero) is always invalid.
    /// </remarks>
    internal static class FyniteHandleOwner
    {
        private static int _next;

        /// <summary>Reserves a process-wide unique owner tag. Safe to call from any thread.</summary>
        internal static int Next() => Interlocked.Increment(ref _next);
    }
}
