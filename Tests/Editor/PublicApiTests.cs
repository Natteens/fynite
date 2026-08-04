using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    public sealed class PublicApiTests
    {
        private static Assembly RuntimeAssembly => typeof(Machine).Assembly;

        /// <summary>
        /// Every type a consumer can name, and every member they can call or override. Adding a line
        /// here is a deliberate widening of the package; removing or changing one is a breaking change.
        /// </summary>
        private static readonly string[] FrozenSurface =
        {
            "Fynite.FyniteActivityBuilder`1 [sealed class] <TContext : class>",
            "Fynite.FyniteActivityBuilder`1..ctor(Type) [internal]",
            "Fynite.FyniteActivityBuilder`1.Do<0>(Action`1)",
            "Fynite.FyniteActivityBuilder`1.Publish<0>(Func`2)",
            "Fynite.FyniteActivityBuilder`1.Wait<0>(Single)",
            "Fynite.FyniteActivityBuilder`1.WaitFor<0>(Func`2)",
            "Fynite.FyniteActivityBuilder`1.WaitUntil<0>(Func`2)",

            "Fynite.FyniteEvent [sealed class]",
            "Fynite.FyniteEvent..ctor() [public]",
            "Fynite.FyniteEvent.Publish<0>()",

            "Fynite.FyniteMachineBuilder`1 [sealed class] <TContext : class>",
            "Fynite.FyniteMachineBuilder`1..ctor(Object, TContext) [internal]",
            "Fynite.FyniteMachineBuilder`1.Build<0>()",
            "Fynite.FyniteMachineBuilder`1.Child<2>() <TParent : FyniteState`1, new(); TChild : FyniteState`1, new()>",
            "Fynite.FyniteMachineBuilder`1.InitialChild<2>() <TParent : FyniteState`1, new(); TChild : FyniteState`1, new()>",
            "Fynite.FyniteMachineBuilder`1.Start<1>() <TState : FyniteState`1, new()>",
            "Fynite.FyniteMachineBuilder`1.Use<1>() <TTransitions : IFyniteTransitions`1, new()>",

            "Fynite.FyniteMachine`1 [sealed class] <TContext : class>",
            "Fynite.FyniteMachine`1..ctor(Object, TContext, FyniteDefinition`1) [internal]",
            "Fynite.FyniteMachine`1.ActiveStateCount { get; }",
            "Fynite.FyniteMachine`1.CurrentStateType { get; }",
            "Fynite.FyniteMachine`1.Dispose<0>()",
            "Fynite.FyniteMachine`1.GetActiveStateType<0>(Int32)",
            "Fynite.FyniteMachine`1.IsIn<1>() <TState : FyniteState`1>",
            "Fynite.FyniteMachine`1.IsRunning { get; }",

            "Fynite.FyniteState`1 [abstract class] <TContext : class>",
            "Fynite.FyniteState`1..ctor() [protected]",
            "Fynite.FyniteState`1.ConfigureActivity<0>(FyniteActivityBuilder`1)",
            "Fynite.FyniteState`1.Context { get; }",
            "Fynite.FyniteState`1.DeltaTime { get; }",
            "Fynite.FyniteState`1.Enter<0>()",
            "Fynite.FyniteState`1.Exit<0>()",
            "Fynite.FyniteState`1.FixedDeltaTime { get; }",
            "Fynite.FyniteState`1.FixedUpdate<0>()",
            "Fynite.FyniteState`1.Update<0>()",

            "Fynite.FyniteTransitionSource`1 [readonly struct] <TContext : class>",
            "Fynite.FyniteTransitionSource`1..ctor(FyniteTransitions`1, Int32) [internal]",
            "Fynite.FyniteTransitionSource`1.To<1>() <TState : FyniteState`1, new()>",

            "Fynite.FyniteTransitionTarget`1 [readonly struct] <TContext : class>",
            "Fynite.FyniteTransitionTarget`1..ctor(FyniteTransitions`1, Int32, Int32) [internal]",
            "Fynite.FyniteTransitionTarget`1.On<0>(Func`2)",
            "Fynite.FyniteTransitionTarget`1.When<0>(Func`2)",
            "Fynite.FyniteTransitionTarget`1.When<1>() <TPredicate : IPredicate`1, new()>",

            "Fynite.FyniteTransitions`1 [sealed class] <TContext : class>",
            "Fynite.FyniteTransitions`1..ctor() [internal]",
            "Fynite.FyniteTransitions`1.Any<0>()",
            "Fynite.FyniteTransitions`1.Any<1>() <TTo : FyniteState`1, new()>",
            "Fynite.FyniteTransitions`1.From<1>() <TState : FyniteState`1, new()>",
            "Fynite.FyniteTransitions`1.From<2>() <TFrom : FyniteState`1, new(); TTo : FyniteState`1, new()>",

            "Fynite.IFyniteTransitions`1 [interface] <TContext : class>",
            "Fynite.IFyniteTransitions`1.Configure<0>(FyniteTransitions`1)",

            "Fynite.IPredicate`1 [interface] <in TContext>",
            "Fynite.IPredicate`1.Evaluate<0>(TContext)",

            "Fynite.Machine [static class]",
            "Fynite.Machine.Attach<1>(Object, TContext) <TContext : class>"
        };

        /// <summary>
        /// The freeze itself. Every other test in this file describes the shape of one corner of the
        /// surface; this one is what notices a corner nobody described appearing at all.
        /// </summary>
        [Test]
        public void ThePublicSurfaceIsFrozen()
        {
            var surface = new List<string>();

            foreach (var type in RuntimeAssembly.GetExportedTypes())
            {
                surface.Add(DescribeType(type));

                // Constructors are listed whatever their accessibility: which of these types a user
                // may create, and which the package hands them, is part of the contract.
                foreach (var constructor in type.GetConstructors(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    surface.Add(
                        $"{type.FullName}..ctor({Parameters(constructor)}) [{Access(constructor)}]");
                }

                foreach (var member in type.GetMembers(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                             BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (member is ConstructorInfo || !IsReachable(member))
                    {
                        continue;
                    }

                    surface.Add(type.FullName + "." + Signature(member));
                }
            }

            Assert.That(surface, Is.EquivalentTo(FrozenSurface));
        }

        /// <summary>
        /// The API reference claims to be the whole surface in one page, so it is checked both ways:
        /// nothing reachable is missing from it, and nothing in it has stopped existing.
        /// </summary>
        [Test]
        public void TheApiReferenceAndTheSurfaceAgree()
        {
            var reference = File.ReadAllText(Path.Combine(
                Path.GetFullPath("Packages/com.natteens.fynite"),
                "Documentation~",
                "api.md"));

            var types = new HashSet<string>();
            var members = new HashSet<string>();
            var undocumented = new List<string>();

            foreach (var type in RuntimeAssembly.GetExportedTypes())
            {
                var name = type.Name.Split('`')[0];
                types.Add(name);

                if (!reference.Contains(name))
                {
                    undocumented.Add(type.FullName);
                }

                foreach (var member in type.GetMembers(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                             BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (!IsReachable(member) || member is ConstructorInfo)
                    {
                        continue;
                    }

                    members.Add(member.Name);

                    if (!reference.Contains(member.Name))
                    {
                        undocumented.Add($"{type.FullName}.{member.Name}");
                    }
                }
            }

            Assert.That(undocumented, Is.Empty, "the API reference is missing part of the surface");

            var stale = new List<string>();

            foreach (Match fence in Regex.Matches(reference, "```csharp(.*?)```", RegexOptions.Singleline))
            {
                foreach (Match token in Regex.Matches(fence.Groups[1].Value, @"(\w+)?(?:<[^>]*>)?\.(\w+)"))
                {
                    var type = token.Groups[1].Value;
                    var member = token.Groups[2].Value;

                    if (type.Length > 0 && type.StartsWith("Fynite", StringComparison.Ordinal) &&
                        !types.Contains(type))
                    {
                        stale.Add(type);
                    }

                    if (!members.Contains(member))
                    {
                        stale.Add(member);
                    }
                }
            }

            Assert.That(stale, Is.Empty, "the API reference names something that no longer exists");
        }

        /// <summary>
        /// What a consumer outside the assembly can actually get at: public members, and the protected
        /// ones they inherit by deriving from a state. Property accessors are left out because the
        /// property itself already stands for them.
        /// </summary>
        private static bool IsReachable(MemberInfo member)
        {
            switch (member)
            {
                case MethodInfo method:
                    return !method.IsSpecialName && (method.IsPublic || method.IsFamily);
                case ConstructorInfo constructor:
                    return constructor.IsPublic || constructor.IsFamily;
                case PropertyInfo property:
                    var getter = property.GetMethod;
                    return getter != null && (getter.IsPublic || getter.IsFamily);
                case FieldInfo field:
                    return field.IsPublic || field.IsFamily;
                case EventInfo _:
                    return true;
                default:
                    return false;
            }
        }

        private static string Signature(MemberInfo member)
        {
            switch (member)
            {
                case MethodInfo method:
                    return $"{method.Name}<{method.GetGenericArguments().Length}>" +
                           $"({Parameters(method)}){Constraints(method.GetGenericArguments())}";

                case PropertyInfo property:
                    return $"{property.Name} {{{(property.CanRead ? " get;" : "")}" +
                           $"{(property.CanWrite ? " set;" : "")} }}";

                default:
                    return member.Name;
            }
        }

        private static string Parameters(MethodBase method)
            => string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));

        private static string Access(MethodBase method)
        {
            if (method.IsPublic)
            {
                return "public";
            }

            if (method.IsFamily)
            {
                return "protected";
            }

            return method.IsPrivate ? "private" : "internal";
        }

        /// <summary>The kind of type it is, and what its own generic parameter is allowed to be.</summary>
        private static string DescribeType(Type type)
        {
            string kind;

            if (type.IsInterface)
            {
                kind = "interface";
            }
            else if (type.IsValueType)
            {
                kind = IsReadOnly(type) ? "readonly struct" : "struct";
            }
            else if (type.IsAbstract && type.IsSealed)
            {
                kind = "static class";
            }
            else if (type.IsAbstract)
            {
                kind = "abstract class";
            }
            else
            {
                kind = type.IsSealed ? "sealed class" : "class";
            }

            return $"{type.FullName} [{kind}]{Constraints(type.GetGenericArguments())}";
        }

        private static bool IsReadOnly(Type type)
            => type.GetCustomAttributes(false)
                .Any(attribute => attribute.GetType().Name == "IsReadOnlyAttribute");

        /// <summary>
        /// Variance and constraints, in source order: <c>in TContext : class</c>. A generic parameter
        /// that loses <c>new()</c> or its base type stops accepting the very code it was written for.
        /// </summary>
        private static string Constraints(Type[] parameters)
        {
            if (parameters.Length == 0)
            {
                return string.Empty;
            }

            var described = parameters.Select(parameter =>
            {
                var attributes = parameter.GenericParameterAttributes;
                var variance = string.Empty;

                if ((attributes & GenericParameterAttributes.Contravariant) != 0)
                {
                    variance = "in ";
                }
                else if ((attributes & GenericParameterAttributes.Covariant) != 0)
                {
                    variance = "out ";
                }

                var limits = new List<string>();

                if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                {
                    limits.Add("class");
                }

                limits.AddRange(parameter.GetGenericParameterConstraints().Select(c => c.Name));

                if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                {
                    limits.Add("new()");
                }

                return limits.Count == 0
                    ? variance + parameter.Name
                    : $"{variance}{parameter.Name} : {string.Join(", ", limits)}";
            });

            return " <" + string.Join("; ", described) + ">";
        }

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
        public void FyniteEventIsSealedAndPublishTakesNothing()
        {
            var type = RuntimeAssembly.GetType("Fynite.FyniteEvent", false);

            Assert.That(type, Is.Not.Null);
            Assert.That(type.IsPublic && type.IsSealed, Is.True);

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
        public void EveryActivityStepChainsBackToTheBuilder()
        {
            var type = typeof(FyniteActivityBuilder<ProbeContext>);

            Assert.That(type.IsPublic && type.IsSealed, Is.True);

            foreach (var name in new[] { "Do", "Wait", "WaitUntil", "WaitFor", "Publish" })
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

            // Named by arity, because From and Any now have a one-call form each.
            var transitions = typeof(FyniteTransitions<ProbeContext>)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance);

            Assert.That(transitions.Any(Named("Any", 0)), Is.True);
            Assert.That(transitions.Any(Named("From", 1)), Is.True);

            var source = typeof(FyniteTransitionSource<ProbeContext>);
            Assert.That(source.GetMethod("To"), Is.Not.Null);
        }


        [Test]
        public void TheShorthandsReturnTheSameTargetTheLongFormBuilds()
        {
            var methods = typeof(FyniteTransitions<ProbeContext>)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance);

            var fromPair = methods.Single(Named("From", 2));
            var anyTarget = methods.Single(Named("Any", 1));

            var target = typeof(FyniteTransitionTarget<ProbeContext>);

            Assert.That(fromPair.ReturnType, Is.EqualTo(target));
            Assert.That(anyTarget.ReturnType, Is.EqualTo(target));
            Assert.That(fromPair.GetParameters(), Is.Empty);
            Assert.That(anyTarget.GetParameters(), Is.Empty);

            // The long form still ends at the very same type, so both share every When and On.
            Assert.That(
                typeof(FyniteTransitionSource<ProbeContext>).GetMethod("To").ReturnType,
                Is.EqualTo(target));
        }



        [Test]
        public void TheActivePathAccessorsHaveTheDeclaredShape()
        {
            var machine = typeof(FyniteMachine<ProbeContext>);

            var count = machine.GetProperty("ActiveStateCount");
            Assert.That(count, Is.Not.Null);
            Assert.That(count.PropertyType, Is.EqualTo(typeof(int)));
            Assert.That(count.CanRead, Is.True);
            Assert.That(count.CanWrite, Is.False, "the count must be read only");

            var get = machine.GetMethod("GetActiveStateType");
            Assert.That(get, Is.Not.Null);
            Assert.That(get.ReturnType, Is.EqualTo(typeof(Type)));
            Assert.That(get.IsGenericMethodDefinition, Is.False);

            var parameters = get.GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(1));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(parameters[0].Name, Is.EqualTo("index"));
        }

        /// <summary>
        /// The active path is read one index at a time. Handing out a sequence would mean copying it,
        /// or exposing the array the machine transitions through.
        /// </summary>
        [Test]
        public void NoPublicMemberHandsOutASequenceOfStates()
        {
            var sequences = typeof(FyniteMachine<ProbeContext>)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => method.ReturnType)
                .Concat(typeof(FyniteMachine<ProbeContext>)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(property => property.PropertyType))
                .Where(returned => returned != typeof(Type) && typeof(IEnumerable).IsAssignableFrom(returned))
                .ToArray();

            Assert.That(sequences, Is.Empty, "a public member returns a collection of states");
        }

        /// <summary>
        /// The debugger reads through an internal contract. Nothing it needs became public, and no
        /// public member hands out an owner, a context or a state instance.
        /// </summary>
        [Test]
        public void TheDebugBridgeStaysInternal()
        {
            var view = RuntimeAssembly.GetType("Fynite.IFyniteDebugView", false);

            Assert.That(view, Is.Not.Null, "the debug contract is missing");
            Assert.That(view.IsVisible, Is.False, "IFyniteDebugView became part of the public API");

            var collect = typeof(FyniteLoop).GetMethod(
                "CollectDebugViews",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(List<>).MakeGenericType(view) },
                null);

            Assert.That(collect, Is.Not.Null, "the collection entry point is missing");
            Assert.That(collect.IsPublic, Is.False, "CollectDebugViews became public");

            // Explicit implementations only, so none of the debug members widen the machine.
            foreach (var member in typeof(FyniteMachine<ProbeContext>).GetMembers(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.That(
                    member.Name.StartsWith("Debug", StringComparison.Ordinal),
                    Is.False,
                    $"{member.Name} is public");
            }
        }

        [Test]
        public void NoPublicMemberHandsOutAnOwnerContextOrState()
        {
            var offenders = new List<string>();

            foreach (var type in RuntimeAssembly.GetExportedTypes())
            {
                foreach (var property in type.GetProperties(
                             BindingFlags.Public | BindingFlags.Instance |
                             BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    var returned = property.PropertyType;

                    if (typeof(UnityEngine.Object).IsAssignableFrom(returned))
                    {
                        offenders.Add($"{type.FullName}.{property.Name} exposes a Unity object");
                    }

                    if (returned.IsGenericType &&
                        returned.GetGenericTypeDefinition() == typeof(FyniteState<>))
                    {
                        offenders.Add($"{type.FullName}.{property.Name} exposes a state instance");
                    }
                }
            }

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void TheLifecycleInternalsStayInternal()
        {
            var internals = new[]
            {
                "Fynite.FyniteLoop",
                "Fynite.FyniteLoopPhase",
                "Fynite.FyniteEventInbox",
                "Fynite.FyniteEventTable",
                "Fynite.FynitePredicateTable`1",
                "Fynite.FyniteMatch",
                "Fynite.FyniteDefinition`1",
                "Fynite.FyniteMachineStatus",
                "Fynite.FyniteActivityExecution`1",
                "Fynite.FyniteActivityStep`1",
                "Fynite.FyniteActivityStepKind"
            };

            foreach (var name in internals)
            {
                var type = RuntimeAssembly.GetType(name, false);

                Assert.That(type, Is.Not.Null, $"{name} is missing");
                Assert.That(type.IsVisible, Is.False, $"{name} became part of the public API");
            }

            // The runtime of an activity is more than the plan it was compiled from, and the old name
            // said otherwise.
            Assert.That(RuntimeAssembly.GetType("Fynite.FyniteActivityPlan`1", false), Is.Null);
        }

        /// <summary>
        /// One enum, one struct and one switch. A step is never an object, and never dispatched
        /// through a type of its own.
        /// </summary>
        [Test]
        public void ThereIsNoObjectPerActivityStep()
        {
            var strays = new[]
            {
                "Fynite.DoStep", "Fynite.WaitStep", "Fynite.PublishStep", "Fynite.WaitForStep",
                "Fynite.WaitUntilStep", "Fynite.IFyniteActivityStep", "Fynite.FyniteActivityProgram"
            };

            foreach (var name in strays)
            {
                Assert.That(RuntimeAssembly.GetType(name, false), Is.Null, name);
            }

            var step = RuntimeAssembly.GetType("Fynite.FyniteActivityStep`1", false);

            Assert.That(step.IsValueType, Is.True, "a step became a reference type");
        }

        [Test]
        public void TheTickableContractStaysInternal()
        {
            var tickable = RuntimeAssembly.GetType("Fynite.IFyniteTickable", false);

            Assert.That(tickable, Is.Not.Null);
            Assert.That(tickable.IsPublic, Is.False, "IFyniteTickable must not be exported");
            Assert.That(tickable.GetMethod("LoopShutdown"), Is.Not.Null, "the shutdown hook is missing");
        }

        private static Func<MethodInfo, bool> Named(string name, int arity)
            => method => method.Name == name && method.GetGenericArguments().Length == arity;

        [Test]
        public void ThereIsNoAmbiguityDiagnosticsSwitch()
        {
            Assert.That(RuntimeAssembly.GetType("Fynite.FyniteDiagnostics", false), Is.Null);
        }
    }
}
