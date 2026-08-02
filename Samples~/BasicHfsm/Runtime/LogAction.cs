using UnityEngine;

namespace Fynite.Samples.BasicHfsm
{
    /// <summary>
    /// Writes a configured line into the context's trace.
    /// </summary>
    /// <remarks>
    /// This is the sample's proof that a block carries its own configuration: the graph uses it four
    /// times with four different messages, and each occurrence gets its own instance per machine, so the
    /// counter below never runs together across states or across entities.
    /// </remarks>
    [System.Serializable]
    public sealed class LogAction : FyniteAction<PatrolContext>
    {
        [Tooltip("Text recorded every time this block runs. Configured per occurrence in the graph.")]
        [SerializeField]
        private string message = "ran";

        [Tooltip("How many times this particular occurrence has run on this machine.")]
        [SerializeField]
        private int runCount;

        /// <inheritdoc />
        public override void Execute(PatrolContext context, in FyniteExecution execution)
        {
            runCount++;
            context.Record(message + " (" + runCount + ")");
        }
    }
}
