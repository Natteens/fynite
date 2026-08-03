namespace Fynite
{
    /// <summary>
    /// A reusable group of transitions. Registered with <c>Use&lt;T&gt;()</c> and configured once
    /// during <c>Build()</c>.
    /// </summary>
    public interface IFyniteTransitions<TContext> where TContext : class
    {
        void Configure(FyniteTransitions<TContext> transitions);
    }
}
