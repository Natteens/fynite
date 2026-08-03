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

        public float LastDelta = float.NaN;
        public float LastFixedDelta = float.NaN;

        public Action OnEnter;
        public Action OnUpdate;
        public Action OnFixedUpdate;
        public Action OnExit;

        /// <summary>A log entry such as "GroundedProbe.Update" that makes that callback throw.</summary>
        public string ThrowOn;

        public string Trace => string.Join(",", Log);

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
            FyniteLoop.Clear();
        }
    }
}
