using System;
using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class DefinitionValidationTests
    {
        [Test]
        public void EmptyDefinitionIsRejected()
        {
            var builder = new FyniteBuilder<TraceContext>();

            var error = Assert.Throws<FyniteDefinitionException>(() => builder.Build());
            StringAssert.Contains("empty", error.Message);
        }

        [Test]
        public void MissingRootIsRejected()
        {
            var builder = new FyniteBuilder<TraceContext>();
            builder.AddState("Root");

            var error = Assert.Throws<FyniteDefinitionException>(() => builder.Build());
            StringAssert.Contains("No root state was set", error.Message);
        }

        [Test]
        public void RootWithParentIsRejected()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var outer = builder.AddState("Outer");
            var inner = builder.AddState("Inner", outer);
            builder.SetInitial(outer, inner);
            builder.SetRoot(inner);

            var error = Assert.Throws<FyniteDefinitionException>(() => builder.Build());
            StringAssert.Contains("must not have a parent", error.Message);
        }

        [Test]
        public void SecondStructuralRootIsRejectedAsUnreachable()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            builder.AddState("Orphan");
            builder.SetRoot(root);

            var error = Assert.Throws<FyniteDefinitionException>(() => builder.Build());
            StringAssert.Contains("'Orphan' is not reachable from root 'Root'", error.Message);
            StringAssert.Contains("Only the root state may be parentless", error.Message);
        }

        [Test]
        public void ParentCycleIsRejected()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            var a = builder.AddState("A", root);
            var b = builder.AddState("B", a);
            var c = builder.AddState("C", b);
            builder.SetParent(a, c);
            builder.SetRoot(root);

            var error = Assert.Throws<FyniteDefinitionException>(() => builder.Build());
            StringAssert.Contains("parent cycle was detected", error.Message);
            StringAssert.Contains("'A'", error.Message);
            StringAssert.Contains("'C'", error.Message);
        }

        [Test]
        public void SelfParentIsRejected()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            var loop = builder.AddState("Loop", root);
            builder.SetParent(loop, loop);
            builder.SetRoot(root);

            var error = Assert.Throws<FyniteDefinitionException>(() => builder.Build());
            StringAssert.Contains("parent cycle was detected", error.Message);
        }

        [Test]
        public void CompositeWithoutInitialChildIsRejected()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            var grounded = builder.AddState("Grounded", root);
            builder.AddState("Idle", grounded);
            builder.SetRoot(root);
            builder.SetInitial(root, grounded);

            var error = Assert.Throws<FyniteDefinitionException>(() => builder.Build());
            StringAssert.Contains("State 'Grounded' has children but no initial child", error.Message);
        }

        [Test]
        public void LeafWithInitialChildIsRejected()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            var idle = builder.AddState("Idle", root);
            var moving = builder.AddState("Moving", root);
            builder.SetRoot(root);
            builder.SetInitial(root, idle);
            builder.SetInitial(idle, moving);

            var error = Assert.Throws<FyniteDefinitionException>(() => builder.Build());
            StringAssert.Contains("State 'Idle' is a leaf but declares 'Moving' as its initial child", error.Message);
        }

        [Test]
        public void InitialChildFromAnotherBranchIsRejected()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            var grounded = builder.AddState("Grounded", root);
            var airborne = builder.AddState("Airborne", root);
            var idle = builder.AddState("Idle", grounded);
            var rising = builder.AddState("Rising", airborne);

            builder.SetRoot(root);
            builder.SetInitial(root, grounded);
            builder.SetInitial(grounded, rising);
            builder.SetInitial(airborne, rising);

            var error = Assert.Throws<FyniteDefinitionException>(() => builder.Build());
            StringAssert.Contains(
                "State 'Grounded' cannot use 'Rising' as initial child because it is not its direct child",
                error.Message);
            Assert.IsTrue(idle.IsValid);
        }

        [Test]
        public void StateHandleFromAnotherBuilderIsRejected()
        {
            var first = new FyniteBuilder<TraceContext>();
            var second = new FyniteBuilder<TraceContext>();

            var foreign = second.AddState("Foreign");
            first.AddState("Root");

            var error = Assert.Throws<ArgumentException>(() => first.AddState("Child", foreign));
            StringAssert.Contains("was not produced by this builder", error.Message);
        }

        [Test]
        public void TransitionTargetFromAnotherBuilderIsRejected()
        {
            var sample = new SampleHierarchy();
            var other = new FyniteBuilder<TraceContext>();
            var foreign = other.AddState("Foreign");

            var error = Assert.Throws<ArgumentException>(() =>
                sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(foreign));
            StringAssert.Contains("was not produced by this builder", error.Message);
        }

        [Test]
        public void SignalFromAnotherBuilderIsRejected()
        {
            var sample = new SampleHierarchy();
            var other = new FyniteBuilder<TraceContext>();
            var foreign = other.AddSignal("Foreign");

            var error = Assert.Throws<ArgumentException>(() => sample.Builder.State(sample.Idle).On(foreign));
            StringAssert.Contains("was not produced by this builder", error.Message);
        }

        [Test]
        public void InvalidHandlesAreRejected()
        {
            var sample = new SampleHierarchy();

            Assert.Throws<ArgumentException>(() => sample.Builder.State(StateHandle.Invalid));
            Assert.Throws<ArgumentException>(() => sample.Builder.State(sample.Idle).On(SignalHandle.Invalid));
        }

        [Test]
        public void NullBlockFactoriesAreRejected()
        {
            var sample = new SampleHierarchy();

            Assert.Throws<ArgumentNullException>(() => sample.Builder.State(sample.Idle).OnEnter(null));
            Assert.Throws<ArgumentNullException>(() => sample.Builder.State(sample.Idle).OnTick(null));
            Assert.Throws<ArgumentNullException>(() => sample.Builder.State(sample.Idle).OnFixedTick(null));
            Assert.Throws<ArgumentNullException>(() => sample.Builder.State(sample.Idle).OnExit(null));
        }

        [Test]
        public void NullGuardAndEffectFactoriesAreRejected()
        {
            var sample = new SampleHierarchy();
            var reaction = sample.Builder.State(sample.Idle).On(sample.Move);

            Assert.Throws<ArgumentNullException>(() => reaction.When(null));
            Assert.Throws<ArgumentNullException>(() => reaction.Do(null));
        }

        [Test]
        public void FactoryReturningNullIsRejectedWhenTheMachineIsCreated()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).OnEnter(() => null);
            var definition = sample.Build();

            var error = Assert.Throws<InvalidOperationException>(() => definition.CreateMachine(new TraceContext()));
            StringAssert.Contains("returned null", error.Message);
        }

        [Test]
        public void NamesAreRequired()
        {
            var builder = new FyniteBuilder<TraceContext>();

            Assert.Throws<ArgumentException>(() => builder.AddState(null));
            Assert.Throws<ArgumentException>(() => builder.AddState("  "));
            Assert.Throws<ArgumentException>(() => builder.AddSignal(""));
        }

        [Test]
        public void BuilderCannotBeReusedAfterBuild()
        {
            var sample = new SampleHierarchy();
            sample.Build();

            Assert.Throws<InvalidOperationException>(() => sample.Builder.AddState("Late"));
            Assert.Throws<InvalidOperationException>(() => sample.Builder.AddSignal("Late"));
            Assert.Throws<InvalidOperationException>(() => sample.Builder.State(sample.Idle));
            Assert.Throws<InvalidOperationException>(() => sample.Builder.Build());
        }

        [Test]
        public void SingleStateDefinitionIsValid()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var only = builder.AddState("Only");
            builder.SetRoot(only);

            var definition = builder.Build();

            Assert.AreEqual(1, definition.StateCount);
            Assert.AreEqual(1, definition.MaxDepth);
            Assert.AreEqual(only, definition.Root);
            Assert.IsFalse(definition.GetInitialChild(only).IsValid);
            Assert.IsFalse(definition.GetParent(only).IsValid);
        }

        [Test]
        public void BuiltDefinitionExposesStructure()
        {
            var sample = new SampleHierarchy();
            var definition = sample.Build();

            Assert.AreEqual(8, definition.StateCount);
            Assert.AreEqual(6, definition.SignalCount);
            Assert.AreEqual(4, definition.MaxDepth);
            Assert.AreEqual(typeof(TraceContext), definition.ContextType);

            Assert.AreEqual("Grounded", definition.GetStateName(sample.Grounded));
            Assert.AreEqual("Jump", definition.GetSignalName(sample.Jump));
            Assert.AreEqual(sample.Alive, definition.GetParent(sample.Grounded));
            Assert.AreEqual(sample.Idle, definition.GetInitialChild(sample.Grounded));
            Assert.AreEqual(3, definition.GetDepth(sample.Idle));
            Assert.AreEqual(2, definition.GetChildCount(sample.Grounded));
            Assert.AreEqual(sample.Idle, definition.GetChild(sample.Grounded, 0));
            Assert.AreEqual(sample.Moving, definition.GetChild(sample.Grounded, 1));
            Assert.IsNull(definition.GetPayloadType(sample.Jump));
        }

        [Test]
        public void DefinitionRejectsHandlesFromAnotherDefinition()
        {
            var definition = new SampleHierarchy().Build();
            var foreign = new SampleHierarchy().Build();

            Assert.IsFalse(definition.Owns(foreign.Root));
            Assert.Throws<ArgumentException>(() => definition.GetStateName(foreign.Root));
        }

        [Test]
        public void PayloadTypeIsRecorded()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            builder.SetRoot(root);
            var damage = builder.AddSignal<int>("Damage");

            var definition = builder.Build();

            Assert.AreEqual(typeof(int), definition.GetPayloadType(damage));
        }

        [Test]
        public void OptionsAreValidated()
        {
            var definition = new SampleHierarchy().Build();

            Assert.Throws<ArgumentOutOfRangeException>(() => definition.CreateMachine(
                new TraceContext(),
                null,
                new FyniteMachineOptions { InitialSignalCapacity = 0 }));

            Assert.Throws<ArgumentOutOfRangeException>(() => definition.CreateMachine(
                new TraceContext(),
                null,
                new FyniteMachineOptions { MaxMicrostepsPerPump = 0 }));
        }

        [Test]
        public void ContextIsRequired()
        {
            var definition = new SampleHierarchy().Build();

            Assert.Throws<ArgumentNullException>(() => definition.CreateMachine(null));
        }
    }
}
