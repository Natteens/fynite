namespace Fynite.Tests
{
    /// <summary>Counts ticks.</summary>
    [System.Serializable]
    public sealed class GraphTestTickAction : FyniteAction<GraphTestContext>
    {
        public override void Execute(GraphTestContext context, in FyniteExecution execution) => context.Ticks++;
    }
}
