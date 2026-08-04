using System.Collections;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FyniteTests
{
    public sealed class PlayModeHierarchyTests
    {
        private GameObject owner;
        private PlayModeBranchContext context;
        private FyniteMachine<PlayModeBranchContext> machine;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("FynitePlayModeBranchOwner");
            context = new PlayModeBranchContext();
        }

        [TearDown]
        public void TearDown()
        {
            machine?.Dispose();
            machine = null;

            if (owner != null)
            {
                Object.Destroy(owner);
            }
        }

        [UnityTest]
        public IEnumerator PlayerLoopDrivesTheWholeActivePath()
        {
            machine = Build();
            var parent = context.ParentUpdates;
            var child = context.ChildUpdates;

            yield return null;
            yield return null;

            Assert.That(context.ParentUpdates, Is.GreaterThan(parent));
            Assert.That(context.ChildUpdates, Is.GreaterThan(child));
            Assert.That(machine.IsIn<PlayModeParent>(), Is.True);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(PlayModeChild)));
        }

        [UnityTest]
        public IEnumerator SiblingTransitionsRunWithoutManualUpdate()
        {
            machine = Build();
            context.Go = true;

            yield return null;
            yield return null;

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(PlayModeSibling)));
            Assert.That(machine.IsIn<PlayModeParent>(), Is.True);
            Assert.That(context.ParentExits, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator DestroyedOwnerExitsTheWholeBranch()
        {
            machine = Build();

            yield return null;

            Object.Destroy(owner);

            yield return null;
            yield return null;

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(context.ParentExits, Is.EqualTo(1));
            Assert.That(context.ChildExits, Is.EqualTo(1));
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        private FyniteMachine<PlayModeBranchContext> Build()
            => Machine
                .Attach(owner, context)
                .Start<PlayModeParent>()
                .Child<PlayModeParent, PlayModeChild>()
                .Child<PlayModeParent, PlayModeSibling>()
                .Use<PlayModeBranchTransitions>()
                .Build();
    }

    public sealed class PlayModeBranchContext
    {
        public int ParentUpdates;
        public int ChildUpdates;
        public int ParentExits;
        public int ChildExits;
        public bool Go;
    }

    public sealed class PlayModeParent : FyniteState<PlayModeBranchContext>
    {
        protected override void Update() => Context.ParentUpdates++;

        protected override void Exit() => Context.ParentExits++;
    }

    public sealed class PlayModeChild : FyniteState<PlayModeBranchContext>
    {
        protected override void Update() => Context.ChildUpdates++;

        protected override void Exit() => Context.ChildExits++;
    }

    public sealed class PlayModeSibling : FyniteState<PlayModeBranchContext>
    {
    }

    public sealed class PlayModeBranchTransitions : IFyniteTransitions<PlayModeBranchContext>
    {
        public void Configure(FyniteTransitions<PlayModeBranchContext> transitions)
            => transitions
                .From<PlayModeChild>()
                .To<PlayModeSibling>()
                .When(context => context.Go);
    }
}
