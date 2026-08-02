using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// Context component the runner tests bind to. It lives in a runtime assembly because Unity refuses
    /// to attach components whose type comes from an editor-only assembly.
    /// </summary>
    public sealed class RunnerTestContext : MonoBehaviour
    {
        public int Entered { get; private set; }

        public int Exited { get; private set; }

        public int Ticks { get; private set; }

        public int FixedTicks { get; private set; }

        public int Advanced { get; private set; }

        internal void Enter() => Entered++;

        internal void Exit() => Exited++;

        internal void Tick() => Ticks++;

        internal void FixedTick() => FixedTicks++;

        internal void Advance() => Advanced++;
    }

    /// <summary>A component that is deliberately not the context the definition asks for.</summary>
    public sealed class UnrelatedBehaviour : MonoBehaviour
    {
    }

    public interface IRunnerTestContext
    {
    }

    public sealed class RunnerInterfaceContextA : MonoBehaviour, IRunnerTestContext
    {
    }

    public sealed class RunnerInterfaceContextB : MonoBehaviour, IRunnerTestContext
    {
    }

    public sealed class RunnerInterfaceDefinitionAsset : FyniteDefinitionAsset<IRunnerTestContext>
    {
        private static readonly FyniteDefinition<IRunnerTestContext> Compiled = Create();

        public override FyniteDefinition<IRunnerTestContext> GetDefinition() => Compiled;

        private static FyniteDefinition<IRunnerTestContext> Create()
        {
            var builder = new FyniteBuilder<IRunnerTestContext>();
            var root = builder.AddState("Root");
            builder.SetRoot(root);
            return builder.Build();
        }
    }

    /// <summary>
    /// A hand-written definition asset. The future <c>.fyn</c> importer will emit something equivalent;
    /// nothing downstream of this boundary needs to change when it does.
    /// </summary>
    public sealed class RunnerTestDefinitionAsset : FyniteDefinitionAsset<RunnerTestContext>
    {
        private static readonly FyniteBuilder<RunnerTestContext> Blueprint = CreateBuilder();
        private static readonly FyniteDefinition<RunnerTestContext> Compiled = Blueprint.Build();

        /// <summary>Signal that moves the machine from Ready to Working.</summary>
        public static SignalHandle Advance { get; private set; }

        /// <inheritdoc />
        public override FyniteDefinition<RunnerTestContext> GetDefinition() => Compiled;

        private static FyniteBuilder<RunnerTestContext> CreateBuilder()
        {
            var builder = new FyniteBuilder<RunnerTestContext>();

            var root = builder.AddState("Root");
            var ready = builder.AddState("Ready", root);
            var working = builder.AddState("Working", root);

            builder.SetRoot(root);
            builder.SetInitial(root, ready);

            Advance = builder.AddSignal("Advance");

            builder.State(root)
                .OnEnter(() => new CountEnter())
                .OnTick(() => new CountTick())
                .OnFixedTick(() => new CountFixedTick())
                .OnExit(() => new CountExit());

            builder.State(ready)
                .On(Advance)
                .Do(() => new CountAdvance())
                .TransitionTo(working);

            return builder;
        }

        private sealed class CountEnter : IFyniteAction<RunnerTestContext>
        {
            public void Execute(RunnerTestContext context, in FyniteExecution execution) => context.Enter();
        }

        private sealed class CountExit : IFyniteAction<RunnerTestContext>
        {
            public void Execute(RunnerTestContext context, in FyniteExecution execution) => context.Exit();
        }

        private sealed class CountTick : IFyniteAction<RunnerTestContext>
        {
            public void Execute(RunnerTestContext context, in FyniteExecution execution) => context.Tick();
        }

        private sealed class CountFixedTick : IFyniteAction<RunnerTestContext>
        {
            public void Execute(RunnerTestContext context, in FyniteExecution execution) => context.FixedTick();
        }

        private sealed class CountAdvance : IFyniteAction<RunnerTestContext>
        {
            public void Execute(RunnerTestContext context, in FyniteExecution execution) => context.Advance();
        }
    }
}
