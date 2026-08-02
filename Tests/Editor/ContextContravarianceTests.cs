using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class ContextContravarianceTests
    {
        [Test]
        public void ABlockWrittenAgainstANarrowContractRunsOnAConcreteContext()
        {
            var builder = new FyniteBuilder<PlayerContext>();
            var root = builder.AddState("Root");
            builder.SetRoot(root);

            // ApplyMovement only knows IMovementContext; PlayerContext implements it.
            builder.State(root).OnTick(() => new ApplyMovement());

            var context = new PlayerContext();
            using (var machine = builder.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Tick(0.5f);
                machine.Tick(0.25f);

                Assert.AreEqual(0.75f, context.MovedBy, 0.0001f);
            }
        }

        [Test]
        public void AGuardWrittenAgainstANarrowContractRunsOnAConcreteContext()
        {
            var builder = new FyniteBuilder<PlayerContext>();
            var root = builder.AddState("Root");
            var idle = builder.AddState("Idle", root);
            var moving = builder.AddState("Moving", root);
            builder.SetRoot(root);
            builder.SetInitial(root, idle);

            var move = builder.AddSignal("Move");
            builder.State(idle).On(move).When(() => new CanMove()).TransitionTo(moving);

            var context = new PlayerContext();
            using (var machine = builder.Build().CreateMachine(context))
            {
                machine.Start();

                machine.Raise(move);
                Assert.AreEqual(idle, machine.ActiveLeaf);

                context.Allowed = true;
                machine.Raise(move);
                Assert.AreEqual(moving, machine.ActiveLeaf);
            }
        }

        [Test]
        public void BlocksOfSeveralNarrowContractsCanShareOneContext()
        {
            var builder = new FyniteBuilder<PlayerContext>();
            var root = builder.AddState("Root");
            builder.SetRoot(root);

            builder.State(root)
                .OnEnter(() => new DrainHealth())
                .OnTick(() => new ApplyMovement());

            var context = new PlayerContext();
            using (var machine = builder.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Tick(1f);

                Assert.AreEqual(1, context.Damage);
                Assert.AreEqual(1f, context.MovedBy, 0.0001f);
            }
        }

        private interface IMovementContext
        {
            void ApplyMovement(float deltaTime);
        }

        private interface IHealthContext
        {
            void TakeDamage(int amount);
        }

        private interface IControlContext
        {
            bool Allowed { get; }
        }

        private sealed class PlayerContext : IMovementContext, IHealthContext, IControlContext
        {
            internal float MovedBy { get; private set; }

            internal int Damage { get; private set; }

            public bool Allowed { get; set; }

            public void ApplyMovement(float deltaTime) => MovedBy += deltaTime;

            public void TakeDamage(int amount) => Damage += amount;
        }

        private sealed class ApplyMovement : IFyniteAction<IMovementContext>
        {
            public void Execute(IMovementContext context, in FyniteExecution execution) =>
                context.ApplyMovement(execution.DeltaTime);
        }

        private sealed class DrainHealth : IFyniteAction<IHealthContext>
        {
            public void Execute(IHealthContext context, in FyniteExecution execution) => context.TakeDamage(1);
        }

        private sealed class CanMove : IFyniteGuard<IControlContext>
        {
            public bool Evaluate(IControlContext context, in FyniteExecution execution) => context.Allowed;
        }
    }
}
