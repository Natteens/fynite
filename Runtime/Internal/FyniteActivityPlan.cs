namespace Fynite
{
    /// <summary>
    /// The compiled steps of one state's activity, together with how far through them that state has
    /// got. There is exactly one of these per state instance, so the steps and the progress can live
    /// side by side: everything is allocated by <c>Build()</c>, and running, resetting and cancelling
    /// only move the cursor.
    /// </summary>
    internal sealed class FyniteActivityPlan<TContext> : IFyniteEventSink where TContext : class
    {
        private readonly FyniteActivityStep<TContext>[] steps;

        private int cursor;
        private bool started;
        private float remaining;
        private bool signaled;
        private FyniteEvent listening;

        internal FyniteActivityPlan(FyniteActivityStep<TContext>[] steps) => this.steps = steps;

        /// <summary>
        /// Set by the source a <c>WaitFor</c> step is listening to. It only records that the
        /// occurrence happened; the step finishes on the activity's next tick, never inside
        /// <c>Publish()</c>.
        /// </summary>
        void IFyniteEventSink.Signal(int slot) => signaled = true;

        /// <summary>Puts the activity back at its first step, dropping any wait in progress.</summary>
        internal void Reset()
        {
            if (listening != null)
            {
                listening.Unsubscribe(this);
                listening = null;
            }

            cursor = 0;
            started = false;
            remaining = 0f;
            signaled = false;
        }

        /// <summary>
        /// Stops the activity where it is, so none of the remaining steps run. Gameplay cleanup is not
        /// this method's business: that stays in the state's <c>Exit</c>.
        /// </summary>
        internal void Cancel() => Reset();

        /// <summary>
        /// Runs as far as it can. Immediate steps chain within the same tick; a wait returns and picks
        /// up where it left off. Once the last step is done the activity stays done until the state is
        /// entered again.
        /// </summary>
        internal void Tick(TContext context, float deltaTime)
        {
            while (cursor < steps.Length)
            {
                var step = steps[cursor];

                switch (step.Kind)
                {
                    case FyniteActivityStepKind.Do:
                        step.Action(context);
                        break;

                    case FyniteActivityStepKind.Publish:
                        step.Source.Publish();
                        break;

                    case FyniteActivityStepKind.Wait:
                        if (!started)
                        {
                            started = true;
                            remaining = step.Seconds;
                        }

                        if (remaining > 0f)
                        {
                            remaining -= deltaTime;
                            if (remaining > 0f)
                            {
                                return;
                            }
                        }

                        remaining = 0f;
                        break;

                    case FyniteActivityStepKind.WaitUntil:
                        if (!step.Condition(context))
                        {
                            return;
                        }

                        break;

                    case FyniteActivityStepKind.WaitFor:
                        if (!started)
                        {
                            // Listening starts here, so anything published earlier is not this wait's.
                            started = true;
                            listening = step.Source;
                            listening.Subscribe(this, cursor);
                        }

                        if (!signaled)
                        {
                            return;
                        }

                        listening.Unsubscribe(this);
                        listening = null;
                        signaled = false;
                        break;
                }

                started = false;
                cursor++;
            }
        }
    }
}
