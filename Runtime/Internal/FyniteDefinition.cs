using System;

namespace Fynite
{
    internal sealed class FyniteDefinition<TContext> where TContext : class
    {
        internal readonly FyniteState<TContext>[] States;
        internal readonly Type[] StateTypes;
        internal readonly FyniteHierarchy Hierarchy;
        internal readonly FyniteTransitionRecord<TContext>[] Global;
        internal readonly FyniteTransitionRecord<TContext>[] Local;
        internal readonly int[] LocalStart;
        internal readonly int[] LocalCount;

        /// <summary>The distinct event sources of the machine, in subscription slot order.</summary>
        internal readonly FyniteEvent[] EventSources;
        internal readonly FyniteEventRecord[] GlobalEvents;
        internal readonly FyniteEventRecord[] LocalEvents;
        internal readonly int[] LocalEventStart;
        internal readonly int[] LocalEventCount;

        internal FyniteDefinition(
            FyniteState<TContext>[] states,
            Type[] stateTypes,
            FyniteHierarchy hierarchy,
            FyniteTransitionRecord<TContext>[] global,
            FyniteTransitionRecord<TContext>[] local,
            int[] localStart,
            int[] localCount,
            FyniteEvent[] eventSources,
            FyniteEventRecord[] globalEvents,
            FyniteEventRecord[] localEvents,
            int[] localEventStart,
            int[] localEventCount)
        {
            States = states;
            StateTypes = stateTypes;
            Hierarchy = hierarchy;
            Global = global;
            Local = local;
            LocalStart = localStart;
            LocalCount = localCount;
            EventSources = eventSources;
            GlobalEvents = globalEvents;
            LocalEvents = localEvents;
            LocalEventStart = localEventStart;
            LocalEventCount = localEventCount;
        }
    }
}
