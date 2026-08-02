using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>Records a configured label when a state is exited.</summary>
    [System.Serializable]
    public sealed class GraphTestExitAction : FyniteAction<GraphTestContext>
    {
        [SerializeField] private string label = "exit";

        public override void Execute(GraphTestContext context, in FyniteExecution execution) => context.Record(label);
    }
}
