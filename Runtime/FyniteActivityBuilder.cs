using System;
using System.Collections.Generic;

namespace Fynite
{
    /// <summary>
    /// Declares what a state does over time, as a straight chain of steps. A state receives one of
    /// these in <c>ConfigureActivity</c>, during <c>Build()</c>, and the chain it declares is compiled
    /// once: entering the state later only rewinds it.
    /// </summary>
    public sealed class FyniteActivityBuilder<TContext> where TContext : class
    {
        private readonly List<FyniteActivityDraft<TContext>> drafts =
            new List<FyniteActivityDraft<TContext>>();

        private readonly Type state;

        internal FyniteActivityBuilder(Type state) => this.state = state;

        /// <summary>
        /// Runs <paramref name="action"/> once, the moment the chain reaches this step, and moves on
        /// in the same tick.
        /// </summary>
        public FyniteActivityBuilder<TContext> Do(Action<TContext> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action), Describe("Do() has no action"));
            }

            drafts.Add(FyniteActivityDraft<TContext>.ForDo(action));
            return this;
        }

        /// <summary>
        /// Holds the chain for <paramref name="seconds"/> of <c>DeltaTime</c>. A disabled owner pauses
        /// the count, and <c>Wait(0)</c> passes straight through.
        /// </summary>
        public FyniteActivityBuilder<TContext> Wait(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seconds),
                    seconds,
                    Describe("Wait() needs a finite duration of zero or more seconds"));
            }

            drafts.Add(FyniteActivityDraft<TContext>.ForWait(seconds));
            return this;
        }

        /// <summary>
        /// Holds the chain until <paramref name="condition"/> answers true. It is asked at most once
        /// per update, and must be side effect free.
        /// </summary>
        public FyniteActivityBuilder<TContext> WaitUntil(Func<TContext, bool> condition)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(
                    nameof(condition),
                    Describe("WaitUntil() has no condition"));
            }

            drafts.Add(FyniteActivityDraft<TContext>.ForWaitUntil(condition));
            return this;
        }

        /// <summary>
        /// Holds the chain until the event happens. Listening starts when the step is reached, so a
        /// publication from before that is not this wait's, and repeated publications count once.
        /// </summary>
        /// <param name="source">
        /// Points at the event, as in <c>context =&gt; context.Attack.Finished</c>. Called exactly
        /// once, during <c>Build()</c>; returning null is an error.
        /// </param>
        public FyniteActivityBuilder<TContext> WaitFor(Func<TContext, FyniteEvent> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(
                    nameof(source),
                    Describe("WaitFor() has no event source"));
            }

            drafts.Add(FyniteActivityDraft<TContext>.ForWaitFor(source));
            return this;
        }

        /// <summary>
        /// Publishes the event once and moves on in the same tick. Any transition listening for it
        /// still resolves later, on a safe update, so the chain never transitions from inside itself.
        /// </summary>
        /// <param name="source">
        /// Resolved exactly once, during <c>Build()</c>; returning null is an error.
        /// </param>
        public FyniteActivityBuilder<TContext> Publish(Func<TContext, FyniteEvent> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(
                    nameof(source),
                    Describe("Publish() has no event source"));
            }

            drafts.Add(FyniteActivityDraft<TContext>.ForPublish(source));
            return this;
        }

        /// <summary>
        /// Resolves every selector against the context, once, and freezes the chain. Returns null when
        /// the state declared no steps, which is what keeps a plain state exactly as cheap as before.
        /// </summary>
        internal FyniteActivityPlan<TContext> Compile(TContext context)
        {
            if (drafts.Count == 0)
            {
                return null;
            }

            var steps = new FyniteActivityStep<TContext>[drafts.Count];

            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];

                switch (draft.Kind)
                {
                    case FyniteActivityStepKind.Do:
                        steps[i] = FyniteActivityStep<TContext>.ForDo(draft.Action);
                        break;

                    case FyniteActivityStepKind.Wait:
                        steps[i] = FyniteActivityStep<TContext>.ForWait(draft.Seconds);
                        break;

                    case FyniteActivityStepKind.WaitUntil:
                        steps[i] = FyniteActivityStep<TContext>.ForWaitUntil(draft.Condition);
                        break;

                    case FyniteActivityStepKind.WaitFor:
                        steps[i] = FyniteActivityStep<TContext>.ForWaitFor(
                            Resolve(draft.Selector, context, i, "WaitFor"));
                        break;

                    case FyniteActivityStepKind.Publish:
                        steps[i] = FyniteActivityStep<TContext>.ForPublish(
                            Resolve(draft.Selector, context, i, "Publish"));
                        break;
                }
            }

            return new FyniteActivityPlan<TContext>(steps);
        }

        private FyniteEvent Resolve(
            Func<TContext, FyniteEvent> selector,
            TContext context,
            int index,
            string step)
        {
            var source = selector(context);

            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Fynite: activity step {index + 1} of '{state.Name}' is a {step} that resolved " +
                    "to a null event source.");
            }

            return source;
        }

        private string Describe(string problem) => $"Fynite: the activity of '{state.Name}': {problem}.";
    }
}
