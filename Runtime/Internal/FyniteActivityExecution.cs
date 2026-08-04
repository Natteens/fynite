using System;

namespace Fynite
{
    /// <summary>
    /// The compiled steps of one state's activity, together with how far through them that state has
    /// got. There is exactly one of these per state instance, so the steps and the progress can live
    /// side by side: everything is allocated by <c>Build()</c>, and running, resetting and cancelling
    /// only move the cursor.
    /// </summary>
    internal sealed class FyniteActivityExecution<TContext> : IFyniteEventSink where TContext : class
    {
        private readonly FyniteActivityStep<TContext>[] steps;

        private int cursor;
        private bool stepStarted;
        private float remainingSeconds;
        private bool eventReceived;
        private FyniteEvent listeningSource;

        /// <summary>
        /// Bumped by every reset and cancel. A step runs code that belongs to the game, and that code
        /// is free to end the machine or leave the state, while the tick it interrupted is still on
        /// the stack. Comparing this against the value that tick started with is how it notices the
        /// run it was executing no longer exists, instead of advancing a cursor that was rewound
        /// underneath it.
        /// </summary>
        private int revision;

        internal FyniteActivityExecution(FyniteActivityStep<TContext>[] steps) => this.steps = steps;

        /// <summary>
        /// Set by the source a <c>WaitFor</c> step is listening to. It only records that the
        /// occurrence happened; the step finishes on the activity's next tick, never inside
        /// <c>Publish()</c>.
        /// </summary>
        void IFyniteEventSink.Signal(int slot) => eventReceived = true;

        /// <summary>Puts the activity back at its first step, dropping any wait in progress.</summary>
        internal void Reset()
        {
            revision++;

            if (listeningSource != null)
            {
                listeningSource.Unsubscribe(this);
                listeningSource = null;
            }

            cursor = 0;
            stepStarted = false;
            remainingSeconds = 0f;
            eventReceived = false;
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
            var running = revision;

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
                        if (!stepStarted)
                        {
                            stepStarted = true;
                            remainingSeconds = step.Seconds;
                        }

                        if (remainingSeconds > 0f)
                        {
                            remainingSeconds -= deltaTime;
                            if (remainingSeconds > 0f)
                            {
                                return;
                            }
                        }

                        remainingSeconds = 0f;
                        break;

                    case FyniteActivityStepKind.WaitUntil:
                        if (!step.Condition(context))
                        {
                            return;
                        }

                        break;

                    case FyniteActivityStepKind.WaitFor:
                        if (!stepStarted)
                        {
                            // Listening starts here, so anything published earlier is not this wait's.
                            stepStarted = true;
                            listeningSource = step.Source;
                            listeningSource.Subscribe(this, cursor);
                        }

                        if (!eventReceived)
                        {
                            return;
                        }

                        listeningSource.Unsubscribe(this);
                        listeningSource = null;
                        eventReceived = false;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Fynite: unsupported activity step '{step.Kind}'.");
                }

                if (revision != running)
                {
                    // The step ended this run. Neither the cursor nor the wait belongs to it any more,
                    // so the steps after it are none of this tick's business.
                    return;
                }

                stepStarted = false;
                cursor++;
            }
        }
    }
}
