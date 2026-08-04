using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FyniteTests
{
    /// <summary>
    /// The bridge the Editor debugger reads through: which machines the loop reports, and how little
    /// of each one it is willing to say.
    /// </summary>
    public sealed class DebugViewTests : MachineFixture
    {
        private static readonly Regex Boom = new Regex("fynite-test-boom");

        private readonly List<IFyniteDebugView> views = new List<IFyniteDebugView>();

        /// <summary>The list is the caller's, so the caller is who stops holding the machines.</summary>
        [TearDown]
        public void ReleaseViews() => views.Clear();

        [Test]
        public void ARegisteredMachineIsReported()
        {
            var machine = BuildLocomotion();

            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Has.Count.EqualTo(1));
            Assert.That(views[0], Is.SameAs(machine));
        }

        [Test]
        public void TwoMachinesAreReportedSeparately()
        {
            var first = BuildLocomotion();
            var second = Track(Attach(NewOwner(), new ProbeContext())
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Has.Count.EqualTo(2));
            Assert.That(views, Contains.Item(first));
            Assert.That(views, Contains.Item(second));
        }

        [Test]
        public void TwoMachinesOnTheSameOwnerAreReportedSeparately()
        {
            var owner = NewOwner();
            var first = Track(Attach(owner, Context).Start<IdleProbe>().Build());
            var second = Track(Attach(owner, new ProbeContext()).Start<WalkProbe>().Build());

            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Has.Count.EqualTo(2));
            Assert.That(views[0], Is.Not.SameAs(views[1]));
            Assert.That(views, Contains.Item(first));
            Assert.That(views, Contains.Item(second));
        }

        [Test]
        public void APendingMachineIsReportedExactlyOnce()
        {
            BuildLocomotion();

            var lateContext = new ProbeContext();
            var lateOwner = NewOwner();

            Context.OnUpdate = () =>
            {
                Context.OnUpdate = null;

                // Registered while the loop is walking, so it is waiting rather than in the registry.
                Track(Attach(lateOwner, lateContext).Start<IdleProbe>().Build());
                FyniteLoop.CollectDebugViews(views);
            };

            FyniteLoop.Tick(0.1f, false);

            Assert.That(views, Has.Count.EqualTo(2));
            Assert.That(views[0], Is.Not.SameAs(views[1]));
        }

        [Test]
        public void ADisposedMachineIsNotReported()
        {
            var machine = BuildLocomotion();
            machine.Dispose();

            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Is.Empty);
        }

        [Test]
        public void ADestroyedOwnerRemovesTheMachineAfterItsNextTick()
        {
            var owner = NewOwner();
            var machine = Track(Attach(owner, Context).Start<IdleProbe>().Build());

            Object.DestroyImmediate(owner.gameObject);
            machine.Tick(0.1f);

            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Is.Empty);
        }

        [Test]
        public void AFaultedMachineIsNotReported()
        {
            var machine = BuildLocomotion();
            Context.OnUpdate = () => throw new System.InvalidOperationException("fynite-test-boom");

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Is.Empty);
        }

        [Test]
        public void ClearLeavesNothingToReport()
        {
            BuildLocomotion();
            Track(Attach(NewOwner(), new ProbeContext()).Start<IdleProbe>().Build());

            FyniteLoop.Clear();
            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Is.Empty);
        }

        [Test]
        public void BootstrapLeavesNothingToReport()
        {
            var original = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();

            try
            {
                BuildLocomotion();
                FyniteLoopBootstrapper.Bootstrap();

                FyniteLoop.CollectDebugViews(views);

                Assert.That(views, Is.Empty);
            }
            finally
            {
                UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(original);
            }
        }

        [Test]
        public void TheDestinationIsClearedBeforeItIsFilled()
        {
            views.Add(null);
            views.Add(null);

            BuildLocomotion();
            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Has.Count.EqualTo(1));
            Assert.That(views[0], Is.Not.Null);
        }

        [Test]
        public void RepeatedCollectionsDoNotAccumulate()
        {
            BuildLocomotion();

            for (var i = 0; i < 5; i++)
            {
                FyniteLoop.CollectDebugViews(views);
            }

            Assert.That(views, Has.Count.EqualTo(1));
        }

        [Test]
        public void AFlatMachineReportsOneState()
        {
            var machine = BuildLocomotion();

            FyniteLoop.CollectDebugViews(views);
            var view = views[0];

            Assert.That(view.DebugIsRunning, Is.True);
            Assert.That(view.DebugActiveStateCount, Is.EqualTo(1));
            Assert.That(view.DebugGetActiveStateType(0), Is.EqualTo(typeof(IdleProbe)));
            Assert.That(view.DebugGetActiveStateType(0), Is.EqualTo(machine.CurrentStateType));
        }

        [Test]
        public void AHierarchicalMachineReportsRootToLeaf()
        {
            Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<LocomotionProbe, IdleProbe>()
                .Use<DeepBranchModule>()
                .Build());

            FyniteLoop.CollectDebugViews(views);
            var view = views[0];

            Assert.That(view.DebugActiveStateCount, Is.EqualTo(3));
            Assert.That(view.DebugGetActiveStateType(0), Is.EqualTo(typeof(GroundedProbe)));
            Assert.That(view.DebugGetActiveStateType(1), Is.EqualTo(typeof(LocomotionProbe)));
            Assert.That(view.DebugGetActiveStateType(2), Is.EqualTo(typeof(IdleProbe)));
        }

        [Test]
        public void TheOwnerAndContextTypeAreReported()
        {
            BuildLocomotion();

            FyniteLoop.CollectDebugViews(views);
            var view = views[0];

            Assert.That(view.DebugOwner, Is.SameAs(Owner));
            Assert.That(view.DebugContextType, Is.EqualTo(typeof(ProbeContext)));
        }

        [Test]
        public void TheViewExposesTypesOnlyAndNoInstances()
        {
            var contract = typeof(IFyniteDebugView);

            var members = new List<string>();
            foreach (var member in contract.GetMembers())
            {
                members.Add(member.Name);
            }

            Assert.That(
                members,
                Is.EquivalentTo(new[]
                {
                    "get_DebugOwner", "DebugOwner",
                    "get_DebugContextType", "DebugContextType",
                    "get_DebugIsRunning", "DebugIsRunning",
                    "get_DebugActiveStateCount", "DebugActiveStateCount",
                    "DebugGetActiveStateType"
                }),
                "the debug contract grew; it must stay read only and minimal");

            // The context and the states are described by their Type, never handed over.
            Assert.That(
                contract.GetProperty("DebugContextType").PropertyType,
                Is.EqualTo(typeof(System.Type)));
            Assert.That(
                contract.GetMethod("DebugGetActiveStateType").ReturnType,
                Is.EqualTo(typeof(System.Type)));

            foreach (var property in contract.GetProperties())
            {
                Assert.That(property.CanWrite, Is.False, $"{property.Name} is writable");
            }
        }
    }
}
