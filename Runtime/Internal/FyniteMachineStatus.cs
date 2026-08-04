namespace Fynite
{
    internal enum FyniteMachineStatus
    {
        /// <summary>Built but not yet launched, or launched and abandoned before it started.</summary>
        Idle,

        Running,

        /// <summary>Stopped by an exception. Already released, and never ticks again.</summary>
        Faulted,

        /// <summary>Stopped on purpose: Dispose, a destroyed owner, or a loop reset.</summary>
        Disposed
    }
}
