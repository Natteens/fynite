using System;
using System.Collections.Generic;
using Fynite;

namespace FyniteTests
{
    public sealed class ProbeContext
    {
        public readonly List<string> Log = new List<string>();

        public bool ToWalk;
        public bool ToIdle;
        public bool ToDead;
        public bool ToRun;
        public bool ToAttack;
        public bool ToGrounded;
        public bool ToAirborne;
        public bool ToLocomotion;
        public bool ToSelf;

        public readonly FyniteEvent Alpha = new FyniteEvent();
        public readonly FyniteEvent Beta = new FyniteEvent();
        public readonly FyniteEvent Gamma = new FyniteEvent();

        /// <summary>How often a selector asked for <see cref="Counted"/>.</summary>
        public int Lookups;

        /// <summary>How often <see cref="AskedPredicate"/> was evaluated.</summary>
        public int PredicateCalls;

        /// <summary>What the <c>WaitUntil</c> of the activity probes is waiting for.</summary>
        public bool Unlocked;

        /// <summary>How often the activity probes evaluated a counted <c>WaitUntil</c>.</summary>
        public int Conditions;

        public float LastDelta = float.NaN;
        public float LastFixedDelta = float.NaN;

        public Action OnEnter;
        public Action OnUpdate;
        public Action OnFixedUpdate;
        public Action OnExit;

        /// <summary>A log entry such as "GroundedProbe.Update" that makes that callback throw.</summary>
        public string ThrowOn;

        /// <summary>The same instance as <see cref="Alpha"/>, counting every read.</summary>
        public FyniteEvent Counted
        {
            get
            {
                Lookups++;
                return Alpha;
            }
        }

        /// <summary>An event source that was never created.</summary>
        public FyniteEvent Missing => null;

        public string Trace => string.Join(",", Log);

        /// <summary>
        /// Writes into the same log the state callbacks use, so a trace shows activity steps and
        /// callbacks interleaved in the order they really ran.
        /// </summary>
        public void Mark(string entry) => Log.Add(entry);

        public int CountOf(string entry)
        {
            var total = 0;
            for (var i = 0; i < Log.Count; i++)
            {
                if (Log[i] == entry)
                {
                    total++;
                }
            }

            return total;
        }
    }

    public abstract class ProbeState : FyniteState<ProbeContext>
    {
        public static int Instances;

        protected ProbeState() => Instances++;

        protected override void Enter()
        {
            Record("Enter");
            Context.OnEnter?.Invoke();
        }

        protected override void Update()
        {
            Record("Update");
            Context.LastDelta = DeltaTime;
            Context.OnUpdate?.Invoke();
        }

        protected override void FixedUpdate()
        {
            Record("FixedUpdate");
            Context.LastFixedDelta = FixedDeltaTime;
            Context.OnFixedUpdate?.Invoke();
        }

        protected override void Exit()
        {
            Record("Exit");
            Context.OnExit?.Invoke();
        }

        private void Record(string callback)
        {
            var entry = GetType().Name + "." + callback;
            Context.Log.Add(entry);

            if (Context.ThrowOn == entry)
            {
                throw new InvalidOperationException("fynite-test-boom");
            }
        }
    }

    public sealed class IdleProbe : ProbeState
    {
    }

    public sealed class WalkProbe : ProbeState
    {
    }

    public sealed class RunProbe : ProbeState
    {
    }

    public sealed class DeadProbe : ProbeState
    {
    }

    public sealed class ToWalk : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => context.ToWalk;
    }

    public sealed class ToIdle : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => context.ToIdle;
    }

    public sealed class ToRun : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => context.ToRun;
    }

    public sealed class ToDead : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => context.ToDead;
    }

    public sealed class Never : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => false;
    }

    public sealed class Always : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => true;
    }

    /// <summary>Never matches, and records that it was asked at all.</summary>
    public sealed class AskedPredicate : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context)
        {
            context.PredicateCalls++;
            return false;
        }
    }

    /// <summary>Publishes while it is being evaluated, which is the worst moment to publish.</summary>
    public sealed class PublishingPredicate : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context)
        {
            context.Alpha.Publish();
            return false;
        }
    }

    public sealed class ThrowingPredicate : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context)
            => throw new InvalidOperationException("fynite-test-predicate");
    }

    public sealed class LocomotionModule : IFyniteTransitions<ProbeContext>
    {
        public static int Configured;

        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            Configured++;

            transitions
                .From<IdleProbe>()
                .To<WalkProbe>()
                .When<ToWalk>();

            transitions
                .From<WalkProbe>()
                .To<IdleProbe>()
                .When<ToIdle>();
        }
    }

    public sealed class DeathModule : IFyniteTransitions<ProbeContext>
    {
        public static int Configured;

        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            Configured++;

            transitions
                .Any()
                .To<DeadProbe>()
                .When<ToDead>();
        }
    }

    public static class TestReset
    {
        public static void All()
        {
            ProbeState.Instances = 0;
            LocomotionModule.Configured = 0;
            DeathModule.Configured = 0;
            ActivityReset.All();
            FyniteLoop.Clear();
        }
    }
}
