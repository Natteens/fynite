using Fynite;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace FyniteTests
{
    /// <summary>
    /// The per-frame path of a plain machine, measured after the refactor that moved matching into the
    /// compiled tables. The states here count instead of logging, because the probes used elsewhere
    /// build a string per callback.
    /// </summary>
    public sealed class MachineAllocationTests : MachineFixture
    {
        [Test]
        public void TickWithoutATransition_DoesNotAllocate()
        {
            var machine = Track(Attach().Start<QuietIdle>().Use<QuietModule>().Build());

            machine.Tick(0.1f);
            machine.TickFixed(0.02f);

            Assert.That(
                () =>
                {
                    machine.Tick(0.1f);
                    machine.TickFixed(0.02f);
                },
                Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void PredicateTransition_DoesNotAllocate()
        {
            var machine = Track(Attach().Start<QuietIdle>().Use<QuietLoopModule>().Build());

            // Always true both ways, so every tick switches and the target updates in the same cycle.
            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(() => machine.Tick(0.1f), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void HierarchyTraversal_DoesNotAllocate()
        {
            var machine = Track(Attach()
                .Start<QuietShell>()
                .Child<QuietShell, QuietIdle>()
                .Child<QuietShell, QuietWalk>()
                .Use<QuietBranchModule>()
                .Build());

            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(
                () =>
                {
                    machine.Tick(0.1f);
                    machine.TickFixed(0.02f);
                },
                Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void EventTransition_DoesNotAllocate()
        {
            var machine = Track(Attach().Start<QuietIdle>().Use<QuietEventModule>().Build());

            Context.Alpha.Publish();
            machine.Tick(0.1f);
            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(
                () =>
                {
                    Context.Alpha.Publish();
                    machine.Tick(0.1f);
                },
                Is.Not.AllocatingGCMemory());
        }

        private sealed class QuietShell : FyniteState<ProbeContext>
        {
            protected override void Update() => Context.Conditions++;
        }

        private sealed class QuietIdle : FyniteState<ProbeContext>
        {
            protected override void Update() => Context.Conditions++;
        }

        private sealed class QuietWalk : FyniteState<ProbeContext>
        {
            protected override void Update() => Context.Conditions++;
        }

        /// <summary>Nothing ever fires, so every tick walks the whole table and finds no match.</summary>
        private sealed class QuietModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<QuietIdle, QuietWalk>().When<Never>();
                transitions.Any<QuietWalk>().When<Never>();
            }
        }

        private sealed class QuietLoopModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<QuietIdle, QuietWalk>().When<Always>();
                transitions.From<QuietWalk, QuietIdle>().When<Always>();
            }
        }

        private sealed class QuietBranchModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<QuietIdle, QuietWalk>().When<Never>();
                transitions.From<QuietShell, QuietShell>().When<Never>();
            }
        }

        private sealed class QuietEventModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<QuietIdle, QuietWalk>().On(context => context.Alpha);
                transitions.From<QuietWalk, QuietIdle>().On(context => context.Alpha);
            }
        }
    }
}
