using UnityEngine;

namespace Fynite.Samples.BasicHfsm
{
    /// <summary>Adds distance every tick, at a configured speed.</summary>
    [System.Serializable]
    public sealed class AdvanceAction : FyniteAction<PatrolContext>
    {
        [Tooltip("Units added to the context's distance per second.")]
        [SerializeField]
        private float speed = 2f;

        /// <inheritdoc />
        public override void Execute(PatrolContext context, in FyniteExecution execution)
        {
            context.distance += speed * execution.DeltaTime;
        }
    }
}
