using System;

namespace Fynite
{
    internal sealed class FyniteDelegatePredicate<TContext> : IPredicate<TContext>
    {
        private readonly Func<TContext, bool> predicate;

        internal FyniteDelegatePredicate(Func<TContext, bool> predicate) => this.predicate = predicate;

        public bool Evaluate(TContext context) => predicate(context);
    }
}
