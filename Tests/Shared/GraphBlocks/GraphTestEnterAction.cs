using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>Records a configured label. Used to prove per-occurrence configuration and isolation.</summary>
    [System.Serializable]
    public sealed class GraphTestEnterAction : FyniteAction<GraphTestContext>
    {
        [SerializeField] private string label = "enter";

        /// <summary>Counts runs of this instance, so shared prototypes would be visible immediately.</summary>
        public int runCount;

        public override void Execute(GraphTestContext context, in FyniteExecution execution)
        {
            runCount++;
            context.Record(label + "#" + runCount);
        }
    }
}
