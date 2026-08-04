namespace Fynite
{
    /// <summary>
    /// What the loop is doing right now. The three are mutually exclusive: a tick and a reset never
    /// overlap, and neither ever starts a second one of itself.
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
