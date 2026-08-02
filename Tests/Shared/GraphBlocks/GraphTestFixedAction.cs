namespace Fynite.Tests
{
    /// <summary>Counts fixed ticks.</summary>
    [System.Serializable]
    public sealed class GraphTestFixedAction : FyniteAction<GraphTestContext>
    {
        public override void Execute(GraphTestContext context, in FyniteExecution execution) => context.FixedTicks++;
    }
}
