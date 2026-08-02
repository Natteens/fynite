namespace Fynite.Samples.BasicHfsm
{
    /// <summary>Counts fixed steps, to show FixedTick running on its own cadence.</summary>
    [System.Serializable]
    public sealed class FixedStepAction : FyniteAction<PatrolContext>
    {
        /// <inheritdoc />
        public override void Execute(PatrolContext context, in FyniteExecution execution)
        {
            context.fixedSteps++;
        }
    }
}
