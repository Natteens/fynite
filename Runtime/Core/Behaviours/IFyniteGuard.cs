namespace Fynite
{
    /// <summary>
    /// A predicate deciding whether a reaction is allowed to run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Guards of a reaction are evaluated in registration order and short-circuit on the first
    /// <c>false</c>.
    /// </para>
    /// <para>
    /// A guard must be a pure predicate with respect to the machine. Raising a signal from
    /// <see cref="FyniteExecution.Signals"/> during guard evaluation throws
    /// <see cref="System.InvalidOperationException"/> and faults the machine: a guard that re-queues
    /// the signal it just rejected would spin forever without ever selecting a reaction, so the
    /// microstep budget could never stop it. Put the raise in an effect of the reaction instead.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContext">Context contract this guard requires.</typeparam>
    public interface IFyniteGuard<in TContext> where TContext : class
    {
        /// <summary>Returns true when the reaction may proceed.</summary>
        bool Evaluate(TContext context, in FyniteExecution execution);
    }
}
