using System;
using NUnit.Framework;

namespace Fynite.Tests
{
    /// <summary>
    /// Owner tags must be unique across the whole process, not per closed generic type. These use two
    /// context types that appear nowhere else, so a per-<c>TContext</c> counter would hand both of them
    /// owner 1 and every cross-definition check below would silently pass the wrong handle through.
    /// </summary>
    [TestFixture]
    internal sealed class HandleOwnerIsolationTests
    {
        [Test]
        public void DefinitionsOfDifferentContextTypesDoNotShareStateHandles()
        {
            var left = BuildLeft();
            var right = BuildRight();

            Assert.AreEqual(0, left.Root.Index);
            Assert.AreEqual(0, right.Root.Index);
            Assert.AreNotEqual(left.Root, right.Root);
            Assert.AreNotEqual(left.Idle, right.Idle);
            Assert.IsFalse(left.Root.Equals(right.Root));
        }

        [Test]
        public void DefinitionsOfDifferentContextTypesDoNotShareSignalHandles()
        {
            var left = BuildLeft();
            var right = BuildRight();

            Assert.AreEqual(left.Go.Index, right.Go.Index);
            Assert.AreNotEqual(left.Go, right.Go);
        }

        [Test]
        public void StateAndSignalHandlesAreNeverEqual()
        {
            var left = BuildLeft();

            // Distinct struct types, so this can only ever be compared as objects.
            Assert.IsFalse(left.Root.Equals((object)left.Go));
            Assert.IsFalse(left.Go.Equals((object)left.Root));
            Assert.AreEqual(left.Root.Index, left.Go.Index);
        }

        [Test]
        public void OwnsRejectsHandlesFromTheOtherContextType()
        {
            var left = BuildLeft();
            var right = BuildRight();

            Assert.IsTrue(left.Definition.Owns(left.Root));
            Assert.IsTrue(left.Definition.Owns(left.Go));

            Assert.IsFalse(left.Definition.Owns(right.Root));
            Assert.IsFalse(left.Definition.Owns(right.Go));
            Assert.IsFalse(right.Definition.Owns(left.Root));
            Assert.IsFalse(right.Definition.Owns(left.Go));
        }

        [Test]
        public void DefinitionQueriesRejectHandlesFromTheOtherContextType()
        {
            var left = BuildLeft();
            var right = BuildRight();

            var error = Assert.Throws<ArgumentException>(() => left.Definition.GetStateName(right.Idle));
            StringAssert.Contains("does not belong to this definition", error.Message);

            Assert.Throws<ArgumentException>(() => left.Definition.GetSignalName(right.Go));
            Assert.Throws<ArgumentException>(() => left.Definition.GetDepth(right.Root));
        }

        [Test]
        public void AMachineRejectsASignalFromTheOtherContextTypesDefinition()
        {
            var left = BuildLeft();
            var right = BuildRight();

            using (var machine = left.Definition.CreateMachine(new LeftContext()))
            {
                machine.Start();

                var error = Assert.Throws<ArgumentException>(() => machine.Raise(right.Go));
                StringAssert.Contains("does not belong to this definition", error.Message);
                Assert.AreEqual(FyniteLifecycle.Running, machine.Lifecycle);
            }
        }

        [Test]
        public void AMachineRejectsAStateFromTheOtherContextTypesDefinition()
        {
            var left = BuildLeft();
            var right = BuildRight();

            using (var machine = left.Definition.CreateMachine(new LeftContext()))
            {
                machine.Start();

                var error = Assert.Throws<ArgumentException>(() => machine.IsActive(right.Idle));
                StringAssert.Contains("does not belong to this definition", error.Message);

                // The equivalent local handle is genuinely active, so this is not a false negative.
                Assert.IsTrue(machine.IsActive(left.Idle));
            }
        }

        [Test]
        public void OwnerTagsKeepIncreasingAcrossContextTypes()
        {
            var first = new FyniteBuilder<LeftContext>().AddState("Root");
            var second = new FyniteBuilder<RightContext>().AddState("Root");
            var third = new FyniteBuilder<LeftContext>().AddState("Root");

            Assert.AreNotEqual(first, second);
            Assert.AreNotEqual(second, third);
            Assert.AreNotEqual(first, third);
        }

        private static Fixture<LeftContext> BuildLeft()
        {
            var builder = new FyniteBuilder<LeftContext>();
            return Fixture<LeftContext>.Create(builder);
        }

        private static Fixture<RightContext> BuildRight()
        {
            var builder = new FyniteBuilder<RightContext>();
            return Fixture<RightContext>.Create(builder);
        }

        /// <summary>Identical two-state shape built for whichever context type, so indices line up.</summary>
        private sealed class Fixture<TContext> where TContext : class
        {
            private Fixture(FyniteDefinition<TContext> definition, StateHandle root, StateHandle idle, SignalHandle go)
            {
                Definition = definition;
                Root = root;
                Idle = idle;
                Go = go;
            }

            internal FyniteDefinition<TContext> Definition { get; }

            internal StateHandle Root { get; }

            internal StateHandle Idle { get; }

            internal SignalHandle Go { get; }

            internal static Fixture<TContext> Create(FyniteBuilder<TContext> builder)
            {
                var root = builder.AddState("Root");
                var idle = builder.AddState("Idle", root);
                builder.SetRoot(root);
                builder.SetInitial(root, idle);
                var go = builder.AddSignal("Go");

                return new Fixture<TContext>(builder.Build(), root, idle, go);
            }
        }

        private sealed class LeftContext
        {
        }

        private sealed class RightContext
        {
        }
    }
}
