using System.Reflection;
using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    public sealed class TimeTests : MachineFixture
    {
        [Test]
        public void DeltaTimeComesFromTheUpdateCycle()
        {
            var machine = BuildLocomotion();

            machine.Tick(0.25f);

            Assert.That(Context.LastDelta, Is.EqualTo(0.25f));
        }

        [Test]
        public void FixedDeltaTimeComesFromTheFixedCycle()
        {
            var machine = BuildLocomotion();

            machine.TickFixed(0.02f);

            Assert.That(Context.LastFixedDelta, Is.EqualTo(0.02f));
        }

        [Test]
        public void UpdateAndFixedTimesAreIndependent()
        {
            var machine = BuildLocomotion();

            machine.Tick(0.25f);
            machine.TickFixed(0.02f);

            Assert.That(Context.LastDelta, Is.EqualTo(0.25f));
            Assert.That(Context.LastFixedDelta, Is.EqualTo(0.02f));
        }

        [Test]
        public void LoopTickFeedsTheActiveState()
        {
            BuildLocomotion();

            FyniteLoop.Tick(0.5f, false);

            Assert.That(Context.LastDelta, Is.EqualTo(0.5f));
        }

        [Test]
        public void StateCallbacksTakeNoParameters()
        {
            var stateType = typeof(FyniteState<ProbeContext>);
            var names = new[] { "Enter", "Update", "FixedUpdate", "Exit" };

            foreach (var name in names)
            {
                var method = stateType.GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                Assert.That(method, Is.Not.Null, name + " is missing");
                Assert.That(method.GetParameters(), Is.Empty, name + " must take no parameters");
            }
        }

        [Test]
        public void TimeIsExposedAsProtectedProperties()
        {
            var stateType = typeof(FyniteState<ProbeContext>);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;

            Assert.That(stateType.GetProperty("Context", flags), Is.Not.Null);
            Assert.That(stateType.GetProperty("DeltaTime", flags), Is.Not.Null);
            Assert.That(stateType.GetProperty("FixedDeltaTime", flags), Is.Not.Null);
        }
    }
}
