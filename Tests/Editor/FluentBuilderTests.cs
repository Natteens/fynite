using System;
using System.Reflection;
using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>
    /// Who is allowed to create the configuration types, and what the fluent structs do when someone
    /// gets hold of one the package never handed out.
    /// </summary>
    public sealed class FluentBuilderTests : MachineFixture
    {
        [Test]
        public void FyniteTransitionsCannotBeCreatedFromOutsideThePackage()
        {
            var constructors = typeof(FyniteTransitions<ProbeContext>).GetConstructors(
                BindingFlags.Public | BindingFlags.Instance);

            Assert.That(constructors, Is.Empty, "a module could create its own transitions");

            var internals = typeof(FyniteTransitions<ProbeContext>).GetConstructors(
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(internals, Has.Length.EqualTo(1));
            Assert.That(internals[0].IsAssembly, Is.True, "the package must still create one");
            Assert.That(internals[0].GetParameters(), Is.Empty);
        }

        [Test]
        public void ThePackageStillCreatesTheTransitionsItHandsToAModule()
        {
            RecordingModule.Received = null;

            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<RecordingModule>()
                .Build());

            Assert.That(RecordingModule.Received, Is.Not.Null, "Configure was handed nothing");

            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(WalkProbe)));
        }

        [Test]
        public void FyniteEventStaysDirectlyCreatable()
        {
            var constructors = typeof(FyniteEvent).GetConstructors(
                BindingFlags.Public | BindingFlags.Instance);

            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(constructors[0].GetParameters(), Is.Empty);
            Assert.That(new FyniteEvent(), Is.Not.Null);
        }

        [Test]
        public void ADefaultedSourceRefusesToNameATarget()
            => Assert.That(
                () => default(FyniteTransitionSource<ProbeContext>).To<WalkProbe>(),
                Throws.InvalidOperationException.With.Message.Contains("not created by FyniteTransitions"));

        [Test]
        public void ADefaultedTargetRefusesANamedPredicate()
            => Assert.That(
                () => default(FyniteTransitionTarget<ProbeContext>).When<ToWalk>(),
                Throws.InvalidOperationException.With.Message.Contains("not created by FyniteTransitions"));

        [Test]
        public void ADefaultedTargetRefusesADelegatePredicate()
            => Assert.That(
                () => default(FyniteTransitionTarget<ProbeContext>).When(context => true),
                Throws.InvalidOperationException.With.Message.Contains("not created by FyniteTransitions"));

        [Test]
        public void ADefaultedTargetRefusesAnEventSource()
            => Assert.That(
                () => default(FyniteTransitionTarget<ProbeContext>).On(context => context.Alpha),
                Throws.InvalidOperationException.With.Message.Contains("not created by FyniteTransitions"));

        /// <summary>
        /// A defaulted builder is the first thing wrong, so it is the first thing reported — a null
        /// delegate on one of them must not turn into an argument complaint.
        /// </summary>
        [Test]
        public void ADefaultedTargetComplainsBeforeItLooksAtTheDelegate()
        {
            Assert.That(
                () => default(FyniteTransitionTarget<ProbeContext>).When((Func<ProbeContext, bool>)null),
                Throws.InvalidOperationException);

            Assert.That(
                () => default(FyniteTransitionTarget<ProbeContext>).On(null),
                Throws.InvalidOperationException);
        }

        [Test]
        public void ARealTargetStillRejectsANullDelegate()
        {
            var machine = Attach().Start<IdleProbe>().Use<NullDelegateModule>();

            Assert.That(() => machine.Build(), Throws.ArgumentNullException);
        }

        [Test]
        public void AttachStillRejectsAMissingOwnerOrContext()
        {
            Assert.That(() => Machine.Attach(null, Context), Throws.ArgumentNullException);
            Assert.That(
                () => Machine.Attach(Owner, (ProbeContext)null),
                Throws.ArgumentNullException);
        }

        /// <summary>Every way of naming a rule still reaches the same machine.</summary>
        [Test]
        public void SourceTargetLambdasNamedPredicatesAndEventsAllStillRoute()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<EveryFormModule>()
                .Build());

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(WalkProbe)), "named predicate");

            Context.ToWalk = false;
            Context.ToRun = true;
            machine.Tick(0.1f);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(RunProbe)), "lambda");

            Context.Alpha.Publish();
            machine.Tick(0.1f);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(DeadProbe)), "event");
        }

        [Test]
        public void TheShorthandAndTheLongFormStillBuildTheSameMachine()
        {
            var shorthand = Track(Attach().Start<IdleProbe>().Use<ShorthandModule>().Build());
            Context.ToWalk = true;
            shorthand.Tick(0.1f);
            var byShorthand = Context.Trace;

            var context = new ProbeContext();
            var longForm = Track(Attach(NewOwner(), context)
                .Start<IdleProbe>()
                .Use<LongFormModule>()
                .Build());

            context.ToWalk = true;
            longForm.Tick(0.1f);

            Assert.That(context.Trace, Is.EqualTo(byShorthand));
            Assert.That(longForm.CurrentStateType, Is.EqualTo(shorthand.CurrentStateType));
        }

        private sealed class RecordingModule : IFyniteTransitions<ProbeContext>
        {
            internal static FyniteTransitions<ProbeContext> Received;

            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                Received = transitions;
                transitions.From<IdleProbe, WalkProbe>().When<ToWalk>();
            }
        }

        private sealed class NullDelegateModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe, WalkProbe>().When((Func<ProbeContext, bool>)null);
        }

        private sealed class EveryFormModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe>().To<WalkProbe>().When<ToWalk>();
                transitions.From<WalkProbe, RunProbe>().When(context => context.ToRun);
                transitions.Any<DeadProbe>().On(context => context.Alpha);
            }
        }

        private sealed class ShorthandModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe, WalkProbe>().When<ToWalk>();
        }

        private sealed class LongFormModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe>().To<WalkProbe>().When<ToWalk>();
        }
    }
}
