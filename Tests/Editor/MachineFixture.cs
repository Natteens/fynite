using System.Collections.Generic;
using Fynite;
using NUnit.Framework;
using UnityEngine;

namespace FyniteTests
{
    public abstract class MachineFixture
    {
        private readonly List<GameObject> owners = new List<GameObject>();
        private readonly List<FyniteMachine<ProbeContext>> machines =
            new List<FyniteMachine<ProbeContext>>();

        protected ProbeContext Context { get; private set; }

        protected ProbeOwner Owner { get; private set; }

        [SetUp]
        public void SetUpFixture()
        {
            TestReset.All();
            Context = new ProbeContext();
            Owner = NewOwner();
        }

        [TearDown]
        public void TearDownFixture()
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

        protected ProbeOwner NewOwner()
        {
            var gameObject = new GameObject("FyniteTestOwner");
            owners.Add(gameObject);
            return gameObject.AddComponent<ProbeOwner>();
        }

        protected FyniteMachineBuilder<ProbeContext> Attach()
            => Machine.Attach(Owner, Context);

        protected FyniteMachineBuilder<ProbeContext> Attach(ProbeOwner owner, ProbeContext context)
            => Machine.Attach(owner, context);

        protected FyniteMachine<ProbeContext> Track(FyniteMachine<ProbeContext> machine)
        {
            machines.Add(machine);
            return machine;
        }

        protected FyniteMachine<ProbeContext> BuildLocomotion()
            => Track(Attach()
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

        /// <summary>Grounded &gt; Locomotion (initial), Attack — plus Airborne &gt; Falling.</summary>
        protected FyniteMachine<ProbeContext> BuildBranches()
            => Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<AirborneProbe, FallingProbe>()
                .Use<PlayerBranchModule>()
                .Build());

        /// <summary>Shell &gt; Left (initial), Right — next to Outside. Every state has an activity.</summary>
        protected FyniteMachine<ProbeContext> BuildShell()
            => Track(Attach()
                .Start<ShellProbe>()
                .Child<ShellProbe, LeftProbe>()
                .Child<ShellProbe, RightProbe>()
                .Use<ShellModule>()
                .Build());

        /// <summary>The same shape as <see cref="BuildBranches"/>, routed entirely by events.</summary>
        protected FyniteMachine<ProbeContext> BuildEventBranches()
            => Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<AirborneProbe, FallingProbe>()
                .Use<BranchEventModule>()
                .Build());
    }
}
