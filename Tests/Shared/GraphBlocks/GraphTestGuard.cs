namespace Fynite.Tests
{
    /// <summary>Passes only while the context allows it.</summary>
    [System.Serializable]
    public sealed class GraphTestGuard : FyniteGuard<GraphTestContext>
    {
        public override bool Evaluate(GraphTestContext context, in FyniteExecution execution) => context.Allow;
    }
}
