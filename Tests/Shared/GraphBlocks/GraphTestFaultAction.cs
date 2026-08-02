using System;

namespace Fynite.Tests
{
    /// <summary>Throws, so fault handling can be observed through the compiled pipeline.</summary>
    [System.Serializable]
    public sealed class GraphTestFaultAction : FyniteAction<GraphTestContext>
    {
        public override void Execute(GraphTestContext context, in FyniteExecution execution) =>
            throw new InvalidOperationException("graph test fault");
    }
}
