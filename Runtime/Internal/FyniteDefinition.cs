using System;

namespace Fynite
{
    internal sealed class FyniteDefinition<TContext> where TContext : class
    {
        internal readonly FyniteState<TContext>[] States;
        internal readonly Type[] StateTypes;
        internal readonly FyniteTransitionRecord<TContext>[] Global;
        internal readonly FyniteTransitionRecord<TContext>[] Local;
        internal readonly int[] LocalStart;
        internal readonly int[] LocalCount;

        internal FyniteDefinition(
            FyniteState<TContext>[] states,
            Type[] stateTypes,
            FyniteTransitionRecord<TContext>[] global,
            FyniteTransitionRecord<TContext>[] local,
            int[] localStart,
            int[] localCount)
        {
            States = states;
            StateTypes = stateTypes;
            Global = global;
            Local = local;
            LocalStart = localStart;
            LocalCount = localCount;
        }
    }
}
