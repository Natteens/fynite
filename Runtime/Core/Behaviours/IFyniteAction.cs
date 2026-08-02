namespace Fynite
{
    /// <summary>
    /// A reusable behaviour block: state enter/tick/fixed-tick/exit work, or a reaction effect.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TContext"/> is contravariant, so a block written against a narrow contract
    /// (for example <c>IFyniteAction&lt;IMovementContext&gt;</c>) can be used by any definition whose
    /// context implements that contract.
    /// One runtime instance is created per configured occurrence per machine, so a block may hold
    /// mutable state without leaking it across entities. Implement <see cref="System.IDisposable"/>
    /// to receive disposal when the owning machine is disposed.
    /// </remarks>
    /// <typeparam name="TContext">Context contract this block requires.</typeparam>
    public interface IFyniteAction<in TContext> where TContext : class
    {
        /// <summary>Runs the block against the machine's context.</summary>
        void Execute(TContext context, in FyniteExecution execution);
    }
}
