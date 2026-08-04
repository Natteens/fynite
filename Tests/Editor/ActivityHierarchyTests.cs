using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>
    /// Every state on the active path may have its own activity. A parent keeps its own running while
    /// its children come and go, and only the states a transition actually removes are cancelled.
    /// </summary>
    public sealed class ActivityHierarchyTests : MachineFixture
    {
        [Test]
        public void ParentRunsBeforeChild()
        {
            var machine = BuildShell();
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("ShellProbe.Update,Shell.Begin,LeftProbe.Update,Left.Begin"));
        }

        [Test]
        public void ParentActivity_SurvivesASiblingTransition()
        {
            var machine = BuildShell();
            machine.Tick(0.1f);

            Assert.That(Context.Gamma.SubscriberCount, Is.EqualTo(1));
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<RightProbe>(), Is.True);
            Assert.That(machine.IsIn<ShellProbe>(), Is.True);
            Assert.That(Context.CountOf("Shell.Begin"), Is.EqualTo(1), "the parent chain restarted");
            Assert.That(Context.Gamma.SubscriberCount, Is.EqualTo(1), "the parent stopped listening");
        }

        [Test]
        public void ChildActivity_IsCancelledWhenItLeaves()
        {
            var machine = BuildShell();
            machine.Tick(0.1f);

            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(Context.CountOf("Left.End"), Is.Zero, "the cancelled chain kept going");
        }

        [Test]
        public void NewChild_StartsItsOwnActivity()
        {
            var machine = BuildShell();
            machine.Tick(0.1f);
            Context.Log.Clear();

            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("Right.Begin"), Is.EqualTo(1));
        }

        [Test]
        public void CrossBranchTransition_CancelsEveryStateItRemoves()
        {
            var machine = BuildShell();
            machine.Tick(0.1f);

            Context.ToAirborne = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<OutsideProbe>(), Is.True);
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(Context.Gamma.SubscriberCount, Is.Zero);
            Assert.That(Context.CountOf("Outside.Begin"), Is.EqualTo(1));
        }

        [Test]
        public void AncestorTarget_KeepsTheAncestorActivity()
        {
            var machine = BuildShell();
            machine.Tick(0.1f);

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Context.ToWalk = false;

            Assert.That(machine.IsIn<RightProbe>(), Is.True);

            Context.ToLocomotion = true;
            machine.Tick(0.1f);

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LeftProbe)));
            Assert.That(Context.CountOf("Shell.Begin"), Is.EqualTo(1), "the ancestor chain restarted");
            Assert.That(Context.Gamma.SubscriberCount, Is.EqualTo(1));
            Assert.That(Context.CountOf("Left.Begin"), Is.EqualTo(2), "the branch did not start over");
        }

        [Test]
        public void SelfTransitionOfAComposite_RestartsTheWholeBranch()
        {
            var machine = BuildShell();
            machine.Tick(0.1f);

            Context.ToSelf = true;
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("Shell.Begin"), Is.EqualTo(2));
            Assert.That(Context.CountOf("Left.Begin"), Is.EqualTo(2));
            Assert.That(Context.Gamma.SubscriberCount, Is.EqualTo(1));
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));
        }

        [Test]
        public void ParentActivity_CompletesIndependentlyOfTheChildren()
        {
            var machine = BuildShell();
            machine.Tick(0.1f);

            Context.Gamma.Publish();
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("Shell.End"), Is.EqualTo(1));
            Assert.That(Context.CountOf("Left.End"), Is.Zero);
            Assert.That(machine.IsIn<LeftProbe>(), Is.True);
        }

        [Test]
        public void Dispose_CancelsEveryActivityOnThePath()
        {
            var machine = BuildShell();
            machine.Tick(0.1f);

            machine.Dispose();

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(Context.Gamma.SubscriberCount, Is.Zero);
        }
    }
}
