namespace Fynite
{
    /// <summary>
    /// Condition evaluated by a transition. Implementations must be side effect free.
    /// </summary>
    public interface IPredicate<in TContext>
    {
        bool Evaluate(TContext context);
    }
}
