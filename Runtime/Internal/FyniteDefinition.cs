using System;

namespace Fynite
{
    /// <summary>
    /// Everything about a machine that is decided at <c>Build()</c> and never changes afterwards. The
    /// transitions live in their own tables, so the arrays that only mean something together are held
    /// together instead of arriving here as a row of interchangeable parameters.
    /// </summary>
    internal sealed class FyniteDefinition<TContext> where TContext : class
    {
        internal readonly FyniteState<TContext>[] States;
        internal readonly Type[] StateTypes;
        internal readonly FyniteHierarchy Hierarchy;
        internal readonly FynitePredicateTable<TContext> Predicates;
        internal readonly FyniteEventTable Events;

        /// <summary>The distinct event sources of the machine, in subscription slot order.</summary>
        internal readonly FyniteEvent[] EventSources;

        internal FyniteDefinition(
            FyniteState<TContext>[] states,
            Type[] stateTypes,
            FyniteHierarchy hierarchy,
            FynitePredicateTable<TContext> predicates,
            FyniteEventTable events,
            FyniteEvent[] eventSources)
        {
            States = states;
            StateTypes = stateTypes;
            Hierarchy = hierarchy;
            Predicates = predicates;
            Events = events;
            EventSources = eventSources;
        }
    }
}
