using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class HandleTests
    {
        [Test]
        public void DefaultStateHandleIsInvalid()
        {
            Assert.IsFalse(default(StateHandle).IsValid);
            Assert.IsFalse(StateHandle.Invalid.IsValid);
            Assert.AreEqual(StateHandle.Invalid, default(StateHandle));
        }

        [Test]
        public void DefaultSignalHandleIsInvalid()
        {
            Assert.IsFalse(default(SignalHandle).IsValid);
            Assert.IsFalse(SignalHandle.Invalid.IsValid);
            Assert.IsFalse(default(SignalHandle<int>).IsValid);
        }

        [Test]
        public void DeclaredHandlesAreValid()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var state = builder.AddState("Root");
            var signal = builder.AddSignal("Go");

            Assert.IsTrue(state.IsValid);
            Assert.IsTrue(signal.IsValid);
        }

        [Test]
        public void SameHandleIsEqualToItself()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            var alias = root;

            Assert.IsTrue(root.Equals(alias));
            Assert.IsTrue(root == alias);
            Assert.IsFalse(root != alias);
            Assert.AreEqual(root.GetHashCode(), alias.GetHashCode());
            Assert.IsTrue(root.Equals((object)alias));
        }

        [Test]
        public void DistinctStatesOfTheSameBuilderAreNotEqual()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            var child = builder.AddState("Child", root);

            Assert.AreNotEqual(root, child);
            Assert.IsTrue(root != child);
        }

        [Test]
        public void HandlesOfDifferentBuildersWithTheSameIndexAreNotEqual()
        {
            var first = new FyniteBuilder<TraceContext>();
            var second = new FyniteBuilder<TraceContext>();

            var firstRoot = first.AddState("Root");
            var secondRoot = second.AddState("Root");

            Assert.AreEqual(0, firstRoot.Index);
            Assert.AreEqual(0, secondRoot.Index);
            Assert.AreNotEqual(firstRoot, secondRoot);
        }

        [Test]
        public void SignalHandlesOfDifferentBuildersAreNotEqual()
        {
            var first = new FyniteBuilder<TraceContext>();
            var second = new FyniteBuilder<TraceContext>();

            Assert.AreNotEqual(first.AddSignal("Go"), second.AddSignal("Go"));
        }

        [Test]
        public void TypedSignalConvertsToUntypedHandle()
        {
            var builder = new FyniteBuilder<TraceContext>();
            SignalHandle<int> typed = builder.AddSignal<int>("Damage");
            SignalHandle untyped = typed;

            Assert.AreEqual(typed.Handle, untyped);
            Assert.AreEqual(typed.Index, untyped.Index);
        }

        [Test]
        public void ToStringMentionsIndexAndValidity()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            var child = builder.AddState("Child", root);
            var signal = builder.AddSignal("Go");

            StringAssert.Contains("1", child.ToString());
            StringAssert.Contains("invalid", StateHandle.Invalid.ToString());
            StringAssert.Contains("invalid", SignalHandle.Invalid.ToString());
            StringAssert.Contains("SignalHandle", signal.ToString());
            StringAssert.Contains("Int32", default(SignalHandle<int>).ToString());
        }

        [Test]
        public void HandleIsNotEqualToOtherTypes()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");

            Assert.IsFalse(root.Equals("Root"));
            Assert.IsFalse(root.Equals(null));
        }
    }
}
