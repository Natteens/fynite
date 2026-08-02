using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>Records a configured label, used as a reaction effect.</summary>
    [System.Serializable]
    public sealed class GraphTestEffectAction : FyniteAction<GraphTestContext>
    {
        [SerializeField] private string label = "effect";

        public override void Execute(GraphTestContext context, in FyniteExecution execution) => context.Record(label);
    }
}
