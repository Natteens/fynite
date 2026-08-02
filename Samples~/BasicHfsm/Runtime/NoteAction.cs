namespace Fynite.Samples.BasicHfsm
{
    /// <summary>Reads the payload of the Notify signal into the context.</summary>
    /// <remarks>
    /// Used as the effect of a reaction that has no target, which is how a signal can do work without
    /// disturbing the active path.
    /// </remarks>
    [System.Serializable]
    public sealed class NoteAction : FyniteAction<PatrolContext>
    {
        /// <inheritdoc />
        public override void Execute(PatrolContext context, in FyniteExecution execution)
        {
            if (execution.Signal.TryGetPayload(out string note))
            {
                context.lastNote = note;
                context.Record("noted '" + note + "'");
            }
        }
    }
}
