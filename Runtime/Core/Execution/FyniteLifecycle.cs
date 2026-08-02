namespace Fynite
{
    /// <summary>Observable lifecycle of a machine instance.</summary>
    public enum FyniteLifecycle
    {
        /// <summary>Constructed but never started.</summary>
        Created = 0,

        /// <summary>Started; an active path exists.</summary>
        Running = 1,

        /// <summary>Stopped cleanly; the machine can be started again.</summary>
        Stopped = 2,

        /// <summary>A block or the executor threw; the machine is irreversibly unusable except for disposal.</summary>
        Faulted = 3,

        /// <summary>Disposed; every operation other than a further <c>Dispose</c> is rejected.</summary>
        Disposed = 4
    }
}
