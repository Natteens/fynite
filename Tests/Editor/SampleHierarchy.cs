namespace Fynite.Tests
{
    /// <summary>
    /// The canonical tree used by most execution tests.
    /// <code>
    /// Root
    /// └── Alive            [initial of Root]
    ///     ├── Grounded     [initial of Alive]
    ///     │   ├── Idle     [initial of Grounded]
    ///     │   └── Moving
    ///     └── Airborne
    ///         ├── Rising   [initial of Airborne]
    ///         └── Falling
    /// </code>
    /// The builder is left open so each test can attach its own blocks and reactions.
    /// </summary>
    internal sealed class SampleHierarchy
    {
        internal SampleHierarchy()
        {
            Builder = new FyniteBuilder<TraceContext>();

            Root = Builder.AddState("Root");
            Alive = Builder.AddState("Alive", Root);
            Grounded = Builder.AddState("Grounded", Alive);
            Idle = Builder.AddState("Idle", Grounded);
            Moving = Builder.AddState("Moving", Grounded);
            Airborne = Builder.AddState("Airborne", Alive);
            Rising = Builder.AddState("Rising", Airborne);
            Falling = Builder.AddState("Falling", Airborne);

            Builder.SetRoot(Root);
            Builder.SetInitial(Root, Alive);
            Builder.SetInitial(Alive, Grounded);
            Builder.SetInitial(Grounded, Idle);
            Builder.SetInitial(Airborne, Rising);

            Move = Builder.AddSignal("Move");
            Stop = Builder.AddSignal("Stop");
            Jump = Builder.AddSignal("Jump");
            Fall = Builder.AddSignal("Fall");
            Damage = Builder.AddSignal("Damage");
            Interact = Builder.AddSignal("Interact");
        }

        internal FyniteBuilder<TraceContext> Builder { get; }

        internal StateHandle Root { get; }

        internal StateHandle Alive { get; }

        internal StateHandle Grounded { get; }

        internal StateHandle Idle { get; }

        internal StateHandle Moving { get; }

        internal StateHandle Airborne { get; }

        internal StateHandle Rising { get; }

        internal StateHandle Falling { get; }

        internal SignalHandle Move { get; }

        internal SignalHandle Stop { get; }

        internal SignalHandle Jump { get; }

        internal SignalHandle Fall { get; }

        internal SignalHandle Damage { get; }

        internal SignalHandle Interact { get; }

        /// <summary>Attaches a <see cref="PhaseAction"/> to every phase of every state.</summary>
        internal SampleHierarchy TraceAllPhases()
        {
            TracePhases(Root, "Root");
            TracePhases(Alive, "Alive");
            TracePhases(Grounded, "Grounded");
            TracePhases(Idle, "Idle");
            TracePhases(Moving, "Moving");
            TracePhases(Airborne, "Airborne");
            TracePhases(Rising, "Rising");
            TracePhases(Falling, "Falling");
            return this;
        }

        internal void TracePhases(StateHandle state, string label)
        {
            Builder.State(state)
                .OnEnter(() => new PhaseAction(label))
                .OnTick(() => new PhaseAction(label))
                .OnFixedTick(() => new PhaseAction(label))
                .OnExit(() => new PhaseAction(label));
        }

        internal FyniteDefinition<TraceContext> Build() => Builder.Build();
    }
}
