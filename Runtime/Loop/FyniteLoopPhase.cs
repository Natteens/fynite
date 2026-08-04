namespace Fynite
{
    /// <summary>
    /// What the loop is doing right now. One value instead of a set of booleans, because the states
    /// are mutually exclusive and the combinations the booleans allowed were never valid.
    /// </summary>
    internal enum FyniteLoopPhase
    {
        /// <summary>Between frames. Machines register straight away and a reset runs immediately.</summary>
        Idle,

        /// <summary>Walking the registered machines. New machines wait, and a reset is deferred.</summary>
        Ticking,

        /// <summary>Ending every machine it holds. Nothing new may be built or registered.</summary>
        ShuttingDown
    }
}
