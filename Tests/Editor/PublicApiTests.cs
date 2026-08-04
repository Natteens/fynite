using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    public sealed class PublicApiTests
    {
        private static Assembly RuntimeAssembly => typeof(Machine).Assembly;

        [Test]
        public void EveryPublicTypeLivesInTheFyniteNamespace()
        {
            var strays = RuntimeAssembly
                .GetExportedTypes()
                .Where(type => type.Namespace != "Fynite")
                .Select(type => type.FullName)
                .ToArray();

            Assert.That(strays, Is.Empty);
        }

        [Test]
        public void NoTypeOfThePackageLivesInTheGlobalNamespace()
        {
            var strays = RuntimeAssembly
                .GetTypes()
                .Where(type => !type.IsNested && string.IsNullOrEmpty(type.Namespace))
                .Select(type => type.Name)
                .Where(name => !IsGenerated(name))
                .ToArray();

            Assert.That(strays, Is.Empty);
        }

        // Unity's own source generator emits a global type into every assembly it compiles.
        private static bool IsGenerated(string name)
            => name.StartsWith("<", StringComparison.Ordinal)
               || name.StartsWith("UnitySourceGenerated", StringComparison.Ordinal);

        [Test]
        public void ThereIsNoTypeNamedFynite()
        {
            var named = RuntimeAssembly
                .GetTypes()
                .Where(type => type.Name == "Fynite")
                .Select(type => type.FullName)
                .ToArray();

            Assert.That(named, Is.Empty);
        }

        [Test]
        public void MachineIsThePublicEntryPoint()
        {
            var machine = RuntimeAssembly.GetType("Fynite.Machine", false);

            Assert.That(machine, Is.Not.Null);
            Assert.That(machine.IsPublic, Is.True);
            Assert.That(machine.IsAbstract && machine.IsSealed, Is.True, "Machine must be static");
        }

        [Test]
        public void AttachIsGenericAndReturnsTheBuilder()
        {
            var attach = typeof(Machine).GetMethod("Attach", BindingFlags.Public | BindingFlags.Static);

            Assert.That(attach, Is.Not.Null);
            Assert.That(attach.IsGenericMethodDefinition, Is.True);
            Assert.That(attach.GetGenericArguments(), Has.Length.EqualTo(1));

            var parameters = attach.GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(2));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(UnityEngine.Object)));

            var closed = attach.MakeGenericMethod(typeof(ProbeContext));
            Assert.That(
                closed.ReturnType,
                Is.EqualTo(typeof(FyniteMachineBuilder<ProbeContext>)));
        }

        [Test]
        public void PublicApiHasNoPriorityMember()
        {
            var offenders = new List<string>();

            foreach (var type in RuntimeAssembly.GetExportedTypes())
            {
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                                       BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (member.Name.IndexOf("Priority", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        offenders.Add($"{type.FullName}.{member.Name}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void FyniteEventExposesNothingButPublish()
        {
            var type = RuntimeAssembly.GetType("Fynite.FyniteEvent", false);

            Assert.That(type, Is.Not.Null);
            Assert.That(type.IsPublic && type.IsSealed, Is.True);

            var members = type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance |
                            BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => member.Name)
                .ToArray();

            Assert.That(members, Is.EquivalentTo(new[] { ".ctor", "Publish" }));

            var publish = type.GetMethod("Publish");
            Assert.That(publish.GetParameters(), Is.Empty);
            Assert.That(publish.ReturnType, Is.EqualTo(typeof(void)));
        }

        [Test]
        public void ThereIsNoEventInterfaceAndNoGenericEvent()
        {
            Assert.That(RuntimeAssembly.GetType("Fynite.IFyniteEvent", false), Is.Null);
            Assert.That(RuntimeAssembly.GetType("Fynite.FyniteEvent`1", false), Is.Null);

            var strays = RuntimeAssembly
                .GetTypes()
                .Where(type => type.IsInterface && type.Name.IndexOf("Event", StringComparison.Ordinal) >= 0)
                .Where(type => type.IsPublic)
                .Select(type => type.FullName)
                .ToArray();

            Assert.That(strays, Is.Empty);
        }

        [Test]
        public void TheMachineHasNoManualEventEntryPoint()
        {
            var forbidden = new[] { "Send", "Raise", "Trigger", "Publish", "Subscribe", "Unsubscribe" };
            var offenders = new List<string>();

            foreach (var type in RuntimeAssembly.GetExportedTypes())
            {
                // FyniteEvent is where publishing lives, and an activity step declares one.
                if (type.Name == "FyniteEvent" || type.Name.StartsWith("FyniteActivityBuilder`", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                                       BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (Array.IndexOf(forbidden, member.Name) >= 0)
                    {
                        offenders.Add($"{type.FullName}.{member.Name}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void FyniteActivityBuilderExposesOnlyTheFiveSteps()
        {
            var type = typeof(FyniteActivityBuilder<ProbeContext>);

            Assert.That(type.IsPublic && type.IsSealed, Is.True);

            var members = type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance |
                            BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => member.Name)
                .ToArray();

            Assert.That(
                members,
                Is.EquivalentTo(new[] { "Do", "Wait", "WaitUntil", "WaitFor", "Publish" }),
                "the builder grew a member, or its constructor became public");

            foreach (var name in members)
            {
                Assert.That(type.GetMethod(name).ReturnType, Is.EqualTo(type), $"{name} does not chain");
            }
        }

        [Test]
        public void ActivityStepsTakeTheDeclaredParameters()
        {
            var type = typeof(FyniteActivityBuilder<ProbeContext>);

            Assert.That(
                type.GetMethod("Do").GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(Action<ProbeContext>)));
            Assert.That(
                type.GetMethod("Wait").GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(float)));
            Assert.That(
                type.GetMethod("WaitUntil").GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(Func<ProbeContext, bool>)));
            Assert.That(
                type.GetMethod("WaitFor").GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(Func<ProbeContext, FyniteEvent>)));
            Assert.That(
                type.GetMethod("Publish").GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(Func<ProbeContext, FyniteEvent>)));
        }

        [Test]
        public void ConfigureActivityIsProtectedAndVirtual()
        {
            var method = typeof(FyniteState<ProbeContext>).GetMethod(
                "ConfigureActivity",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(method, Is.Not.Null);
            Assert.That(method.IsFamily, Is.True, "ConfigureActivity must be protected");
            Assert.That(method.IsVirtual, Is.True);
            Assert.That(
                method.GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(FyniteActivityBuilder<ProbeContext>)));
        }

        [Test]
        public void ActivitiesAddNoOtherPublicType()
        {
            var exported = RuntimeAssembly
                .GetExportedTypes()
                .Select(type => type.Name)
                .ToArray();

            Assert.That(
                exported,
                Is.EquivalentTo(new[]
                {
                    "Machine",
                    "FyniteMachine`1",
                    "FyniteMachineBuilder`1",
                    "FyniteState`1",
                    "FyniteTransitions`1",
                    "FyniteTransitionSource`1",
                    "FyniteTransitionTarget`1",
                    "IFyniteTransitions`1",
                    "IPredicate`1",
                    "FyniteEvent",
                    "FyniteActivityBuilder`1"
                }));
        }

        [Test]
        public void ThereIsNoActivityRunnerHandleOrStatus()
        {
            var forbidden = new[]
            {
                "Fynite.FyniteActivity",
                "Fynite.IFyniteActivity",
                "Fynite.ActivityHandle",
                "Fynite.ActivityRunner",
                "Fynite.ActivityStatus",
                "Fynite.ActivityToken",
                "Fynite.Sequence",
                "Fynite.Parallel",
                "Fynite.Branch"
            };

            foreach (var name in forbidden)
            {
                Assert.That(RuntimeAssembly.GetType(name, false), Is.Null, name);
            }
        }

        [Test]
        public void OnIsDeclaredOnTheTransitionTarget()
        {
            var on = typeof(FyniteTransitionTarget<ProbeContext>)
                .GetMethod("On", BindingFlags.Public | BindingFlags.Instance);

            Assert.That(on, Is.Not.Null);
            Assert.That(on.ReturnType, Is.EqualTo(typeof(FyniteTransitions<ProbeContext>)));

            var parameters = on.GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(1));
            Assert.That(
                parameters[0].ParameterType,
                Is.EqualTo(typeof(Func<ProbeContext, FyniteEvent>)));
        }

        [Test]
        public void TheOtherWaysOfDeclaringATransitionSurvive()
        {
            var target = typeof(FyniteTransitionTarget<ProbeContext>);

            Assert.That(target.GetMethod("When", new[] { typeof(Func<ProbeContext, bool>) }), Is.Not.Null);
            Assert.That(
                target.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(method => method.Name == "When" && method.IsGenericMethodDefinition),
                Is.True);

            var transitions = typeof(FyniteTransitions<ProbeContext>);
            Assert.That(transitions.GetMethod("Any"), Is.Not.Null);
            Assert.That(transitions.GetMethod("From"), Is.Not.Null);
        }

        [Test]
        public void ThereIsNoAmbiguityDiagnosticsSwitch()
        {
            Assert.That(RuntimeAssembly.GetType("Fynite.FyniteDiagnostics", false), Is.Null);

            var offenders = RuntimeAssembly
                .GetTypes()
                .SelectMany(type => type.GetMembers(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(member => member.Name.IndexOf("Ambigu", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(member => member.Name)
                .ToArray();

            Assert.That(offenders, Is.Empty);
        }
    }
}
