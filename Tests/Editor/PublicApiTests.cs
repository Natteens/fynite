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
