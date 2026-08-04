using Fynite;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace FyniteTests
{
    /// <summary>
    /// Reading the active path is a look at memory that already exists. No collection is built, so
    /// walking it every frame costs nothing. The states here do not log, because the probes used
    /// elsewhere build a string per callback.
    /// </summary>
    public sealed class ActivePathAllocationTests : MachineFixture
    {
        [Test]
        public void TheCount_DoesNotAllocate()
        {
            var machine = Track(Attach().Start<QuietTop>().Use<QuietModule>().Build());

            machine.Tick(0.1f);

            Assert.That(() => Read(machine), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ReadingAFlatPath_DoesNotAllocate()
        {
            var machine = Track(Attach().Start<QuietTop>().Use<QuietModule>().Build());

            machine.Tick(0.1f);
            WalkPath(machine);

            Assert.That(() => WalkPath(machine), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ReadingAHierarchicalPath_DoesNotAllocate()
        {
            var machine = BuildQuietBranch();

            machine.Tick(0.1f);
            WalkPath(machine);

            Assert.That(machine.ActiveStateCount, Is.EqualTo(3));
            Assert.That(() => WalkPath(machine), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ReadingAfterTransitions_DoesNotAllocate()
        {
            var machine = BuildQuietBranch();

            // Alternates between two branches, so the path is rebuilt on every tick.
            Context.ToAirborne = true;
            machine.Tick(0.1f);
            WalkPath(machine);

            Context.ToAirborne = false;
            Context.ToGrounded = true;
            machine.Tick(0.1f);
            WalkPath(machine);

            Assert.That(
                () =>
                {
                    machine.Tick(0.1f);
                    WalkPath(machine);
                },
                Is.Not.AllocatingGCMemory());
        }

        private FyniteMachine<ProbeContext> BuildQuietBranch()
            => Track(Attach()
                .Start<QuietTop>()
                .Child<QuietTop, QuietMiddle>()
                .Child<QuietMiddle, QuietLeaf>()
                .Child<QuietOther, QuietLeaf2>()
                .Use<QuietBranchModule>()
                .Build());

        /// <summary>Kept so the reads below cannot be optimised away.</summary>
        private int observed;

        private void Read(FyniteMachine<ProbeContext> machine) => observed = machine.ActiveStateCount;

        /// <summary>The loop from the README, with nothing in it that could allocate on its own.</summary>
        private void WalkPath(FyniteMachine<ProbeContext> machine)
        {
            var seen = 0;

            for (var i = 0; i < machine.ActiveStateCount; i++)
            {
                if (machine.GetActiveStateType(i) != null)
                {
                    seen++;
                }
            }

            observed = seen;
        }

        private sealed class QuietTop : FyniteState<ProbeContext>
        {
        }

        private sealed class QuietMiddle : FyniteState<ProbeContext>
        {
        }

        private sealed class QuietLeaf : FyniteState<ProbeContext>
        {
        }

        private sealed class QuietOther : FyniteState<ProbeContext>
        {
        }

        private sealed class QuietLeaf2 : FyniteState<ProbeContext>
        {
        }

        private sealed class QuietModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<QuietTop, QuietOther>().When<Never>();
        }

        private sealed class QuietBranchModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<QuietTop, QuietOther>().When<ToAirborne>();
                transitions.From<QuietOther, QuietTop>().When<ToGrounded>();
            }
        }
    }
}
