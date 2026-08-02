using System.Collections.Generic;
using System.Linq;
using Fynite.GraphEditor;
using NUnit.Framework;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// The hierarchy invariants <see cref="FyniteStateOperations"/> has to keep standing.
    /// </summary>
    /// <remarks>
    /// The Initial mark belongs to a parent, not to a child: a composite has exactly one, a leaf has
    /// none, and no operation may leave a composite without one. That is why it is maintained by these
    /// operations instead of being a box on the node, and why every test here ends by checking the
    /// invariant rather than only the value it just wrote.
    /// </remarks>
    public sealed class StateHierarchyTests
    {
        private FyniteGraph _graph;

        [TearDown]
        public void TearDown()
        {
            _graph = null;
            TestGraphFactory.CleanUp();
        }

        private FyniteGraph NewGraph(string name)
        {
            _graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath(name));
            _graph.SetContextType(typeof(GraphTestContext));
            _graph.TouchForFieldChange();
            return _graph;
        }

        private FyniteRootStateNode Root => _graph.FindRoot();

        private FyniteStateNode Add(IFyniteIdentifiedNode parent, string name)
        {
            var state = FyniteStateOperations.CreateChild(_graph, parent, Vector2.zero);
            Assert.IsNotNull(state);
            FyniteStateOperations.Rename(_graph, state, name);
            return state;
        }

        /// <summary>No parent with two initial children, and no composite without one.</summary>
        private void AssertInitialInvariant()
        {
            var parents = new List<IFyniteIdentifiedNode> { Root };
            parents.AddRange(_graph.GetNodes().OfType<FyniteStateNode>().Cast<IFyniteIdentifiedNode>());

            foreach (var parent in parents)
            {
                var children = FyniteStateOperations.ChildrenOf(parent);
                if (children.Count == 0)
                {
                    continue;
                }

                Assert.AreEqual(
                    1,
                    children.Count(c => c.IsInitial),
                    "'" + FyniteStateOperations.PathOf(parent) + "' has " + children.Count + " children");
            }
        }

        [Test]
        public void TheFirstChildBecomesInitialAndTheSecondDoesNot()
        {
            NewGraph("hierarchy-create");

            var idle = Add(Root, "Idle");
            Assert.IsTrue(idle.IsInitial);

            var moving = Add(Root, "Moving");
            Assert.IsFalse(moving.IsInitial, "a later child never displaces the initial one");
            Assert.IsTrue(idle.IsInitial);

            AssertInitialInvariant();
        }

        [Test]
        public void SetAsInitialClearsOnlyTheDirectSiblings()
        {
            NewGraph("hierarchy-initial");

            var idle = Add(Root, "Idle");
            var moving = Add(Root, "Moving");
            var nested = Add(idle, "Waiting");

            Assert.IsTrue(FyniteStateOperations.SetAsInitial(_graph, moving));

            Assert.IsTrue(moving.IsInitial);
            Assert.IsFalse(idle.IsInitial, "the sibling loses the mark");
            Assert.IsTrue(nested.IsInitial, "a state deeper down holds it for its own parent");

            AssertInitialInvariant();
        }

        [Test]
        public void MovingTheInitialOutElectsASuccessorAmongTheRemainingSiblings()
        {
            NewGraph("hierarchy-move-initial");

            var grounded = Add(Root, "Grounded");
            var idle = Add(grounded, "Idle");
            var moving = Add(grounded, "Moving");
            var airborne = Add(Root, "Airborne");

            Assert.IsTrue(idle.IsInitial);
            Assert.IsTrue(FyniteStateOperations.MoveToParent(_graph, idle, airborne, out var error), error);

            Assert.IsTrue(moving.IsInitial, "Grounded still has a child, so it still needs an initial one");
            Assert.AreEqual("Root / Airborne / Idle", FyniteStateOperations.PathOf(idle));

            AssertInitialInvariant();
        }

        [Test]
        public void TheElectedSuccessorIsTheFirstRemainingChildInGraphOrder()
        {
            NewGraph("hierarchy-move-deterministic");

            var grounded = Add(Root, "Grounded");
            var idle = Add(grounded, "Idle");
            Add(grounded, "Walking");
            Add(grounded, "Running");
            var airborne = Add(Root, "Airborne");

            var remaining = FyniteStateOperations.ChildrenOf(grounded).Where(c => c != idle).ToList();
            var expected = _graph.GetNodes().OfType<FyniteStateNode>().First(remaining.Contains);

            Assert.IsTrue(FyniteStateOperations.MoveToParent(_graph, idle, airborne, out var error), error);

            Assert.IsTrue(expected.IsInitial, "the choice is the graph's own order, not an arbitrary one");
            AssertInitialInvariant();
        }

        [Test]
        public void MovingTheLastChildLeavesTheOldParentALeaf()
        {
            NewGraph("hierarchy-move-last");

            var grounded = Add(Root, "Grounded");
            var only = Add(grounded, "Idle");
            var airborne = Add(Root, "Airborne");

            Assert.IsTrue(FyniteStateOperations.MoveToParent(_graph, only, airborne, out var error), error);

            Assert.AreEqual(0, FyniteStateOperations.ChildrenOf(grounded).Count);
            AssertInitialInvariant();
        }

        [Test]
        public void MovingIntoAnEmptyParentMakesTheMovedStateInitial()
        {
            NewGraph("hierarchy-move-empty");

            var idle = Add(Root, "Idle");
            Add(Root, "Moving");
            var airborne = Add(Root, "Airborne");

            Assert.IsTrue(FyniteStateOperations.MoveToParent(_graph, idle, airborne, out var error), error);

            Assert.IsTrue(idle.IsInitial, "an only child is what its parent enters");
            AssertInitialInvariant();
        }

        [Test]
        public void MovingIntoAParentThatAlreadyHasAnInitialDoesNotCreateASecond()
        {
            NewGraph("hierarchy-move-occupied");

            var grounded = Add(Root, "Grounded");
            var occupant = Add(grounded, "Idle");
            var mover = Add(Root, "Airborne");

            Assert.IsTrue(FyniteStateOperations.MoveToParent(_graph, mover, grounded, out var error), error);

            Assert.IsTrue(occupant.IsInitial, "the occupant keeps the mark");
            Assert.IsFalse(mover.IsInitial, "the arrival does not take it");
            AssertInitialInvariant();
        }

        [Test]
        public void SelfParentIsRejected()
        {
            NewGraph("hierarchy-self");

            var idle = Add(Root, "Idle");

            Assert.IsFalse(FyniteStateOperations.MoveToParent(_graph, idle, idle, out var error));
            StringAssert.Contains("own Parent", error);
            AssertInitialInvariant();
        }

        [Test]
        public void AnIndirectCycleIsRejected()
        {
            NewGraph("hierarchy-cycle");

            var grounded = Add(Root, "Grounded");
            var idle = Add(grounded, "Idle");
            var waiting = Add(idle, "Waiting");

            Assert.IsFalse(FyniteStateOperations.MoveToParent(_graph, grounded, waiting, out var error));
            StringAssert.Contains("cycle", error);

            Assert.AreEqual("Root / Grounded", FyniteStateOperations.PathOf(grounded), "nothing moved");
            AssertInitialInvariant();
        }

        [Test]
        public void DuplicateGetsItsOwnIdentityAndIsNeverInitial()
        {
            NewGraph("hierarchy-duplicate");

            var idle = Add(Root, "Idle");
            Assert.IsTrue(idle.IsInitial);

            var copy = FyniteStateOperations.Duplicate(_graph, idle, Vector2.right);

            Assert.IsNotNull(copy);
            Assert.AreNotEqual(idle.FyniteGuid, copy.FyniteGuid, "a copy is a different state");
            Assert.IsFalse(copy.IsInitial, "duplicating an initial state never duplicates the mark");
            Assert.IsTrue(idle.IsInitial);

            AssertInitialInvariant();
        }
    }
}
