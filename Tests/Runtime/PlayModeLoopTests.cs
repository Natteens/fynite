using System.Collections;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FyniteTests
{
    public sealed class PlayModeLoopTests
    {
        private GameObject owner;
        private PlayModeContext context;
        private FyniteMachine<PlayModeContext> machine;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("FynitePlayModeOwner");
            context = new PlayModeContext();
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
        public IEnumerator PlayerLoopDrivesUpdate()
        {
            machine = Build();
            var before = context.Updates;

            yield return null;
            yield return null;

            Assert.That(context.Updates, Is.GreaterThan(before));
            Assert.That(context.LastDelta, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator PlayerLoopDrivesFixedUpdate()
        {
            machine = Build();
            var before = context.FixedUpdates;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(context.FixedUpdates, Is.GreaterThan(before));
            Assert.That(context.LastFixedDelta, Is.EqualTo(Time.fixedDeltaTime).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator TransitionsRunWithoutManualUpdate()
        {
            machine = Build();
            context.Go = true;

            yield return null;
            yield return null;

            Assert.That(machine.IsIn<PlayModeTarget>(), Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyedOwnerStopsTheMachine()
        {
            machine = Build();

            yield return null;

            Object.Destroy(owner);

            yield return null;
            yield return null;

            var after = context.Updates;

            yield return null;

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(context.Updates, Is.EqualTo(after));
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator DisabledOwnerPausesAndResumes()
        {
            machine = Build();

            yield return null;

            owner.SetActive(false);
            var paused = context.Updates;

            yield return null;
            yield return null;

            Assert.That(context.Updates, Is.EqualTo(paused));
            Assert.That(machine.IsIn<PlayModeStart>(), Is.True);

            owner.SetActive(true);

            yield return null;

            Assert.That(context.Updates, Is.GreaterThan(paused));
            Assert.That(context.Exits, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator DisposeStopsTheMachine()
        {
            machine = Build();

            yield return null;

            machine.Dispose();
            var after = context.Updates;

            yield return null;
            yield return null;

            Assert.That(context.Updates, Is.EqualTo(after));
            Assert.That(context.Exits, Is.EqualTo(1));
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        private FyniteMachine<PlayModeContext> Build()
            => Machine
                .Attach(owner, context)
                .Start<PlayModeStart>()
                .Use<PlayModeTransitions>()
                .Build();
    }

    public sealed class PlayModeContext
    {
        public int Updates;
        public int FixedUpdates;
        public int Exits;
        public bool Go;
        public float LastDelta;
        public float LastFixedDelta;
    }

    public sealed class PlayModeStart : FyniteState<PlayModeContext>
    {
        protected override void Update()
        {
            Context.Updates++;
            Context.LastDelta = DeltaTime;
        }

        protected override void FixedUpdate()
        {
            Context.FixedUpdates++;
            Context.LastFixedDelta = FixedDeltaTime;
        }

        protected override void Exit() => Context.Exits++;
    }

    public sealed class PlayModeTarget : FyniteState<PlayModeContext>
    {
        protected override void Update() => Context.Updates++;
    }

    public sealed class PlayModeTransitions : IFyniteTransitions<PlayModeContext>
    {
        public void Configure(FyniteTransitions<PlayModeContext> transitions)
            => transitions
                .From<PlayModeStart>()
                .To<PlayModeTarget>()
                .When(context => context.Go);
    }
}
