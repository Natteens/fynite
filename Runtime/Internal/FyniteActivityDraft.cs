using System;

namespace Fynite
{
    /// <summary>
    /// A step as it was declared, before <c>Build()</c> resolves its selector against the context.
    /// </summary>
    internal readonly struct FyniteActivityDraft<TContext> where TContext : class
    {
        internal readonly FyniteActivityStepKind Kind;
        internal readonly Action<TContext> Action;
        internal readonly Func<TContext, bool> Condition;
        internal readonly Func<TContext, FyniteEvent> Selector;
        internal readonly float Seconds;

        private FyniteActivityDraft(
            FyniteActivityStepKind kind,
            Action<TContext> action,
            Func<TContext, bool> condition,
            Func<TContext, FyniteEvent> selector,
            float seconds)
        {
            Kind = kind;
            Action = action;
            Condition = condition;
            Selector = selector;
            Seconds = seconds;
        }

        internal static FyniteActivityDraft<TContext> ForDo(Action<TContext> action)
            => new FyniteActivityDraft<TContext>(FyniteActivityStepKind.Do, action, null, null, 0f);

        internal static FyniteActivityDraft<TContext> ForWait(float seconds)
            => new FyniteActivityDraft<TContext>(FyniteActivityStepKind.Wait, null, null, null, seconds);

        internal static FyniteActivityDraft<TContext> ForWaitUntil(Func<TContext, bool> condition)
            => new FyniteActivityDraft<TContext>(
                FyniteActivityStepKind.WaitUntil,
                null,
                condition,
                null,
                0f);

        internal static FyniteActivityDraft<TContext> ForWaitFor(Func<TContext, FyniteEvent> selector)
            => new FyniteActivityDraft<TContext>(
                FyniteActivityStepKind.WaitFor,
                null,
                null,
                selector,
                0f);

        internal static FyniteActivityDraft<TContext> ForPublish(Func<TContext, FyniteEvent> selector)
            => new FyniteActivityDraft<TContext>(
                FyniteActivityStepKind.Publish,
                null,
                null,
                selector,
                0f);
    }
}
