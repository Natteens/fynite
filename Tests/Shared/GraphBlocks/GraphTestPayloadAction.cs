namespace Fynite.Tests
{
    /// <summary>Copies a string payload into the context.</summary>
    [System.Serializable]
    public sealed class GraphTestPayloadAction : FyniteAction<GraphTestContext>
    {
        public override void Execute(GraphTestContext context, in FyniteExecution execution)
        {
            if (execution.Signal.TryGetPayload(out string payload))
            {
                context.LastPayload = payload;
            }
        }
    }
}
