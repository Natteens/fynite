namespace Fynite.Samples.BasicHfsm
{
    /// <summary>
    /// Lets Idle → Moving through only while the context allows it.
    /// </summary>
    /// <remarks>
    /// A guard is a pure predicate: it may read the context and the execution, and it must not raise
    /// signals or change anything. Raising from a guard faults the machine on purpose.
    /// </remarks>
    [System.Serializable]
    public sealed class CanMoveGuard : FyniteGuard<PatrolContext>
    {
        /// <inheritdoc />
        public override bool Evaluate(PatrolContext context, in FyniteExecution execution) => context.canMove;
    }
}
