namespace Fynite
{
    /// <summary>Identifies which part of the machine's cycle is currently executing a block.</summary>
    public enum FynitePhase
    {
        /// <summary>A state is being entered.</summary>
        Enter = 0,

        /// <summary>Per-frame update of an active state.</summary>
        Tick = 1,

        /// <summary>Fixed-step update of an active state.</summary>
        FixedTick = 2,

        /// <summary>A state is being exited.</summary>
        Exit = 3,

        /// <summary>A guard of a candidate reaction is being evaluated.</summary>
        Guard = 4,

        /// <summary>An effect of the selected reaction is being executed.</summary>
        Effect = 5
    }
}
