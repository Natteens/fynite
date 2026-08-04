using System;
using System.Collections.Generic;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FyniteTests
{
    /// <summary>
    /// What a state sees when it asks the machine about itself while it is leaving. A state is taken
    /// off the active path before its <c>Exit</c> runs, so the answer is whatever stayed above it —
    /// and nothing at all once the top level state is going.
    /// </summary>
    public sealed class CurrentStateTests
    {
        private readonly List<FyniteMachine<ExitContext>> machines =
            new List<FyniteMachine<ExitContext>>();

        private readonly List<GameObject> owners = new List<GameObject>();

        private ExitContext context;
        private ProbeOwner owner;

        [SetUp]
        public void SetUp()
        {
            FyniteLoop.Clear();
            context = new ExitContext();
            owner = NewOwner();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < machines.Count; i++)
            {
                machines[i].Dispose();
            }

            machines.Clear();

            for (var i = 0; i < owners.Count; i++)
            {
                if (owners[i] != null)
                {
                    Object.DestroyImmediate(owners[i]);
                }
            }

            owners.Clear();
            FyniteLoop.Clear();
        }

        [Test]
        public void TheOnlyStateOfAFlatMachineSeesNothingWhileItLeaves()
        {
            var machine = BuildFlat();
            context.ToNext = true;

            machine.Tick(0.1f);

            Assert.That(context.Trace, Is.EqualTo("SoloState=<none>"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(NextState)));
        }

        [Test]
        public void AChildSeesTheParentThatStayedActive()
        {
            var machine = BuildNested();
            context.ToSibling = true;

            machine.Tick(0.1f);

            Assert.That(context.Trace, Is.EqualTo("NestFirst=NestRoot"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(NestSecond)));
        }

        [Test]
        public void TheTopLevelStateSeesNothingWhileItLeaves()
        {
            var machine = BuildNested();
            context.ToOther = true;

            machine.Tick(0.1f);

            Assert.That(context.Trace, Is.EqualTo("NestFirst=NestRoot,NestRoot=<none>"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(OtherChild)));
        }

        [Test]
        public void ASelfTransitionSeesNothingWhileTheStateLeavesItself()
        {
            var machine = BuildFlat();
            context.ToSelf = true;

            machine.Tick(0.1f);

            Assert.That(context.Trace, Is.EqualTo("SoloState=<none>"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(SoloState)));
        }

        [Test]
        public void ACrossBranchTransitionWalksUpToNothing()
        {
            var machine = BuildNested();

            context.ToSibling = true;
            machine.Tick(0.1f);

            context.ToSibling = false;
            context.ToOtherChild = true;
            context.Observed.Clear();

            machine.Tick(0.1f);

            Assert.That(context.Trace, Is.EqualTo("NestSecond=NestRoot,NestRoot=<none>"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(OtherChild)));
            Assert.That(machine.IsIn<NestRoot>(), Is.False);
        }

        [Test]
        public void DisposeLeavesEveryStateWithNothingToReport()
        {
            var machine = BuildNested();

            machine.Dispose();

            Assert.That(context.Trace, Is.EqualTo("NestFirst=<none>,NestRoot=<none>"));
            Assert.That(machine.CurrentStateType, Is.Null);
            Assert.That(machine.ActiveStateCount, Is.Zero);
        }

        [Test]
        public void ALoopResetLeavesEveryStateWithNothingToReport()
        {
            var machine = BuildNested();

            FyniteLoop.Clear();

            Assert.That(context.Trace, Is.EqualTo("NestFirst=<none>,NestRoot=<none>"));
            Assert.That(machine.CurrentStateType, Is.Null);
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void TheActivePathApiAgreesWithTheCurrentStateWhileAChildLeaves()
        {
            var machine = BuildNested();
            context.OnExit = () =>
            {
                context.ObservedCount = machine.ActiveStateCount;
                context.ObservedLeaf = machine.ActiveStateCount > 0
                    ? machine.GetActiveStateType(machine.ActiveStateCount - 1)
                    : null;
            };

            context.ToSibling = true;
            machine.Tick(0.1f);

            Assert.That(context.ObservedCount, Is.EqualTo(1));
            Assert.That(context.ObservedLeaf, Is.EqualTo(typeof(NestRoot)));
        }

        private ProbeOwner NewOwner()
        {
            var gameObject = new GameObject("FyniteExitOwner");
            owners.Add(gameObject);
            return gameObject.AddComponent<ProbeOwner>();
        }

        private FyniteMachine<ExitContext> Track(FyniteMachine<ExitContext> machine)
        {
            machines.Add(machine);
            context.Machine = machine;
            context.Observed.Clear();
            return machine;
        }

        private FyniteMachine<ExitContext> BuildFlat()
            => Track(Machine
                .Attach(owner, context)
                .Start<SoloState>()
                .Use<FlatExitModule>()
                .Build());

        private FyniteMachine<ExitContext> BuildNested()
            => Track(Machine
                .Attach(owner, context)
                .Start<NestRoot>()
                .Child<NestRoot, NestFirst>()
                .Child<NestRoot, NestSecond>()
                .Child<OtherRoot, OtherChild>()
                .Use<NestedExitModule>()
                .Build());
    }

    public sealed class ExitContext
    {
        public readonly List<string> Observed = new List<string>();

        public FyniteMachine<ExitContext> Machine;

        public bool ToNext;
        public bool ToSelf;
        public bool ToSibling;
        public bool ToOther;
        public bool ToOtherChild;

        public Action OnExit;

        public int ObservedCount = -1;
        public Type ObservedLeaf;

        public string Trace => string.Join(",", Observed);

        /// <summary>
        /// What the machine answers, asked through the context rather than by the state itself, which
        /// is how game code would reach it from an <c>Exit</c>.
        /// </summary>
        public void Observe(string state)
        {
            var current = Machine?.CurrentStateType;

            Observed.Add(state + "=" + (current == null ? "<none>" : current.Name));
            OnExit?.Invoke();
        }
    }

    public abstract class ExitProbe : FyniteState<ExitContext>
    {
        protected override void Exit() => Context.Observe(GetType().Name);
    }

    public sealed class SoloState : ExitProbe
    {
    }

    public sealed class NextState : ExitProbe
    {
    }

    public sealed class NestRoot : ExitProbe
    {
    }

    public sealed class NestFirst : ExitProbe
    {
    }

    public sealed class NestSecond : ExitProbe
    {
    }

    public sealed class OtherRoot : ExitProbe
    {
    }

    public sealed class OtherChild : ExitProbe
    {
    }

    public sealed class FlatExitModule : IFyniteTransitions<ExitContext>
    {
        public void Configure(FyniteTransitions<ExitContext> transitions)
        {
            transitions.From<SoloState, NextState>().When(context => context.ToNext);
            transitions.From<SoloState, SoloState>().When(context => context.ToSelf);
        }
    }

    public sealed class NestedExitModule : IFyniteTransitions<ExitContext>
    {
        public void Configure(FyniteTransitions<ExitContext> transitions)
        {
            transitions.From<NestFirst, NestSecond>().When(context => context.ToSibling);
            transitions.From<NestFirst, OtherRoot>().When(context => context.ToOther);
            transitions.From<NestSecond, OtherChild>().When(context => context.ToOtherChild);
        }
    }
}
