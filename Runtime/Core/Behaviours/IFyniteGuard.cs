namespace Fynite
{
    /// <summary>
    /// A predicate deciding whether a reaction is allowed to run.
    /// </summary>
    /// <remarks>
    /// Guards of a reaction are evaluated in registration order and short-circuit on the first
    /// <c>false</c>. A guard must not mutate the machine; raising signals from a guard is possible
    /// through the sink but is queued like any other signal.
    /// </remarks>
    /// <typeparam name="TContext">Context contract this guard requires.</typeparam>
    public interface IFyniteGuard<in TContext> where TContext : class
    {
        /// <summary>Returns true when the reaction may proceed.</summary>
        bool Evaluate(TContext context, in FyniteExecution execution);
    }
}
