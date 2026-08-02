namespace Fynite.Tests
{
    /// <summary>An action for a different context, used to prove incompatible blocks are rejected.</summary>
    [System.Serializable]
    public sealed class GraphTestUnrelatedAction : FyniteAction<UnrelatedGraphContext>
    {
        public override void Execute(UnrelatedGraphContext context, in FyniteExecution execution)
        {
        }
    }
}
