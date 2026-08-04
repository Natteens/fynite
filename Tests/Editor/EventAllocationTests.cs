using Fynite;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace FyniteTests
{
    /// <summary>
    /// Everything an event needs is allocated by <c>Build()</c>, so the path a publication takes has
    /// nothing left to allocate. The states here record nothing, because the probes used everywhere
    /// else build a log entry per callback.
    /// </summary>
    public sealed class EventAllocationTests : MachineFixture
    {
        [Test]
        public void Publish_DoesNotAllocate()
        {
            var machine = Track(Attach().Start<QuietIdle>().Use<QuietModule>().Build());

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(
                () =>
                {
                    Context.Alpha.Publish();
                    Context.Alpha.Publish();
                    Context.Beta.Publish();
                },
                Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void EventDrivenUpdate_DoesNotAllocate()
        {
            var machine = Track(Attach().Start<QuietIdle>().Use<QuietLoopModule>().Build());

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

        [Test]
        public void UnmatchedEventAndPredicates_DoNotAllocate()
        {
            var machine = Track(Attach().Start<QuietIdle>().Use<QuietUnmatchedModule>().Build());

            Context.Beta.Publish();
            machine.Tick(0.1f);

            Assert.That(
                () =>
                {
                    Context.Beta.Publish();
                    machine.Tick(0.1f);
                    machine.TickFixed(0.02f);
                },
                Is.Not.AllocatingGCMemory());
        }

        private abstract class QuietState : FyniteState<ProbeContext>
        {
        }

        private sealed class QuietIdle : QuietState
        {
        }

        private sealed class QuietWalk : QuietState
        {
        }

        private sealed class QuietRun : QuietState
        {
        }

        private sealed class QuietModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<QuietIdle>().To<QuietWalk>().On(context => context.Alpha);
                transitions.From<QuietWalk>().To<QuietRun>().On(context => context.Beta);
            }
        }

        /// <summary>Alpha alternates between the two states forever, so every cycle switches.</summary>
        private sealed class QuietLoopModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<QuietIdle>().To<QuietWalk>().On(context => context.Alpha);
                transitions.From<QuietWalk>().To<QuietIdle>().On(context => context.Alpha);
            }
        }

        /// <summary>Beta never matches from Idle, so the cycle falls through to the predicate.</summary>
        private sealed class QuietUnmatchedModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<QuietWalk>().To<QuietRun>().On(context => context.Beta);
                transitions.From<QuietIdle>().To<QuietRun>().When<AskedPredicate>();
            }
        }
    }
}
