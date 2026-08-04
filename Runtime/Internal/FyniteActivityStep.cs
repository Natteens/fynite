using System;

namespace Fynite
{
    internal enum FyniteActivityStepKind
    {
        Do,
        Wait,
        WaitUntil,
        WaitFor,
        Publish
    }

    /// <summary>
    /// One compiled step of an activity. Only the fields its kind uses are set; the event sources are
    /// already resolved, so nothing here has to be looked up again while the machine runs.
    /// </summary>
    internal readonly struct FyniteActivityStep<TContext> where TContext : class
    {
        internal readonly FyniteActivityStepKind Kind;
        internal readonly Action<TContext> Action;
        internal readonly Func<TContext, bool> Condition;
        internal readonly FyniteEvent Source;
        internal readonly float Seconds;

        internal FyniteActivityStep(
            FyniteActivityStepKind kind,
            Action<TContext> action,
            Func<TContext, bool> condition,
            FyniteEvent source,
            float seconds)
        {
            Kind = kind;
            Action = action;
            Condition = condition;
            Source = source;
            Seconds = seconds;
        }

        internal static FyniteActivityStep<TContext> ForDo(Action<TContext> action)
            => new FyniteActivityStep<TContext>(FyniteActivityStepKind.Do, action, null, null, 0f);

        internal static FyniteActivityStep<TContext> ForWait(float seconds)
            => new FyniteActivityStep<TContext>(FyniteActivityStepKind.Wait, null, null, null, seconds);

        internal static FyniteActivityStep<TContext> ForWaitUntil(Func<TContext, bool> condition)
            => new FyniteActivityStep<TContext>(
                FyniteActivityStepKind.WaitUntil,
                null,
                condition,
                null,
                0f);

        internal static FyniteActivityStep<TContext> ForWaitFor(FyniteEvent source)
            => new FyniteActivityStep<TContext>(FyniteActivityStepKind.WaitFor, null, null, source, 0f);

        internal static FyniteActivityStep<TContext> ForPublish(FyniteEvent source)
            => new FyniteActivityStep<TContext>(FyniteActivityStepKind.Publish, null, null, source, 0f);
    }
}
