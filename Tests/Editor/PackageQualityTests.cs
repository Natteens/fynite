using System;
using System.IO;
using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    public sealed class PackageQualityTests
    {
        private const string GraphToolkitTerm = "Graph" + "Toolkit";
        private const string GraphTypeTerm = "Fynite" + "Graph";
        private const string ReflectionTerm = "System." + "Reflection";
        private const string ActivatorTerm = "Activator." + "CreateInstance";
        private const string RunnerTerm = "Fynite" + "Runner";
        private const string OldFacadeTerm = "Fynite" + ".Attach";
        private const string AmbiguityTerm = "WarnOn" + "AmbiguousTransitions";

        private static string PackageRoot => Path.GetFullPath("Packages/com.natteens.fynite");

        [Test]
        public void RuntimeDoesNotUseReflection()
        {
            AssertRuntimeFree(ReflectionTerm);
            AssertRuntimeFree(ActivatorTerm);
            AssertRuntimeFree("Type.GetType");
        }

        [Test]
        public void RuntimeHasNoPriorityApi()
        {
            AssertRuntimeFree("Priority");
        }

        [Test]
        public void RuntimeSequencesWithoutCoroutinesOrTasks()
        {
            AssertRuntimeFree("IEnumerator");
            AssertRuntimeFree("Coroutine");
            AssertRuntimeFree("Task");
            AssertRuntimeFree("async ");
            AssertRuntimeFree("await ");
            AssertRuntimeFree("yield ");
        }

        [Test]
        public void RuntimeHasNoParallelOrBranchingActivities()
        {
            AssertRuntimeFree("Parallel");
            AssertRuntimeFree("branch =>");
        }

        /// <summary>
        /// The loop reset is bounded by what is registered when it starts, never by a number someone
        /// picked. Anything below would be a machine allowed to escape a reset.
        /// </summary>
        [Test]
        public void RuntimeResetHasNoArbitraryLimitOrBacklog()
        {
            AssertRuntimeFree("Shutdown" + "Passes");
            AssertRuntimeFree("Runaway" + "ShutdownLimit");
            AssertRuntimeFree("Force" + "Shutdown");
            AssertRuntimeFree("closing");
            AssertRuntimeFree("Closing");
        }

        /// <summary>
        /// An activity step stays an enum plus a struct plus a switch. Anything below would be an
        /// object, and a virtual call, per step of every state.
        /// </summary>
        [Test]
        public void RuntimeHasNoObjectPerActivityStep()
        {
            AssertRuntimeFree("DoStep");
            AssertRuntimeFree("WaitStep");
            AssertRuntimeFree("PublishStep");
            AssertRuntimeFree("IActivityStep");
        }

        [Test]
        public void RuntimeDoesNotUseLinq()
        {
            AssertRuntimeFree("System." + "Linq");
        }

        /// <summary>
        /// The debugger is pull based. Nothing in the Runtime pushes, records or remembers anything for
        /// it, so a machine costs the same whether the window is open or does not exist.
        /// </summary>
        [Test]
        public void RuntimeHasNoObserverOrHistory()
        {
            AssertRuntimeFree("IFynite" + "Observer");
            AssertRuntimeFree("Fynite" + "Observer");
            AssertRuntimeFree("OnState" + "Entered");
            AssertRuntimeFree("OnState" + "Exited");
            AssertRuntimeFree("OnTransition");

            // Narrower than "StateChanged" on purpose: Unity's own playModeStateChanged is what the
            // loop hooks to clean up when play mode ends, and that is not a Fynite observer.
            AssertRuntimeFree("OnState" + "Changed");
            AssertRuntimeFree("history");
            AssertRuntimeFree("History");
            AssertRuntimeFree("event Action");
        }

        [Test]
        public void RuntimeTouchesUnityEditorOnlyBehindTheEditorGuard()
        {
            var runtime = Path.Combine(PackageRoot, "Runtime");

            foreach (var file in Directory.GetFiles(runtime, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);

                if (text.IndexOf("UnityEditor", StringComparison.Ordinal) < 0 &&
                    text.IndexOf("IFyniteDebugView", StringComparison.Ordinal) < 0 &&
                    text.IndexOf("CollectDebugViews", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                Assert.That(
                    text,
                    Does.Contain("#if UNITY_EDITOR"),
                    $"{file} reaches for the Editor without a UNITY_EDITOR guard");
            }
        }

        /// <summary>The Editor assembly is Editor only and never steers a machine.</summary>
        [Test]
        public void EditorAssemblyIsReadOnlyAndEditorOnly()
        {
            var definition = File.ReadAllText(
                Path.Combine(PackageRoot, "Editor", "Fynite.Editor.asmdef"));

            Assert.That(definition, Does.Contain("\"name\": \"Fynite.Editor\""));
            Assert.That(definition, Does.Contain("\"Editor\""), "the assembly is not Editor only");
            Assert.That(definition.ToLowerInvariant(), Does.Not.Contain("graphtoolkit"));

            foreach (var file in Directory.GetFiles(
                         Path.Combine(PackageRoot, "Editor"),
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);

                Assert.That(text, Does.Not.Contain(ReflectionTerm), file);
                Assert.That(text, Does.Not.Contain("BindingFlags"), file);
                Assert.That(text, Does.Not.Contain(ActivatorTerm), file);
                Assert.That(text, Does.Not.Contain(GraphToolkitTerm), file);

                // Nothing the window can click may change a machine.
                Assert.That(text, Does.Not.Contain(".Dispose()"), file);
                Assert.That(text, Does.Not.Contain("Force" + "Transition"), file);
                Assert.That(text, Does.Not.Contain("Set" + "State"), file);
                Assert.That(text, Does.Not.Contain("FyniteLoop.Clear"), file);
                Assert.That(text, Does.Not.Contain(".Tick("), file);
            }
        }

        [Test]
        public void PackageHasNoGraphToolkitReferences()
        {
            foreach (var file in EnumerateSources())
            {
                var text = File.ReadAllText(file);
                Assert.That(text, Does.Not.Contain(GraphToolkitTerm), file);
                Assert.That(text, Does.Not.Contain(GraphTypeTerm), file);
                Assert.That(text, Does.Not.Contain(RunnerTerm), file);
            }
        }

        [Test]
        public void PackageHasNoGraphAssets()
        {
            var graphs = Directory.GetFiles(PackageRoot, "*.fyn", SearchOption.AllDirectories);

            Assert.That(graphs, Is.Empty);
        }

        [Test]
        public void ManifestDoesNotDependOnGraphToolkit()
        {
            var manifest = File.ReadAllText(Path.Combine(PackageRoot, "package.json"));

            Assert.That(manifest.ToLowerInvariant(), Does.Not.Contain("graphtoolkit"));
        }

        [Test]
        public void SampleControllerHasNoManualLoop()
        {
            var controller = Path.Combine(
                PackageRoot,
                "Samples~",
                "CodeFirst",
                "Runtime",
                "ExampleController.cs");

            Assert.That(File.Exists(controller), Is.True, controller);

            var text = File.ReadAllText(controller);

            Assert.That(text, Does.Not.Contain("void Update"));
            Assert.That(text, Does.Not.Contain("void FixedUpdate"));
            Assert.That(text, Does.Not.Contain("machine.Update"));
            Assert.That(text, Does.Not.Contain("Tick("));
            Assert.That(text, Does.Not.Contain("OnDestroy"));
            Assert.That(text, Does.Contain("using Fynite;"));
            Assert.That(text, Does.Contain("Machine"));
            Assert.That(text, Does.Contain(".Attach(this, context)"));
            Assert.That(text, Does.Contain(".Build()"));
        }

        /// <summary>
        /// A condition only one module asks belongs in that module. The sample used to ship a class
        /// per answer, one of which was nothing but the negation of the other.
        /// </summary>
        [Test]
        public void SampleKeepsALocalConditionInItsTransitionModule()
        {
            var sample = Path.Combine(PackageRoot, "Samples~", "CodeFirst", "Runtime");

            Assert.That(File.Exists(Path.Combine(sample, "HasMovement.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(sample, "HasNoMovement.cs")), Is.False);

            var module = File.ReadAllText(Path.Combine(sample, "LocomotionTransitions.cs"));

            Assert.That(module, Does.Contain("private static bool HasMovement"));
            Assert.That(module, Does.Contain(".When(HasMovement)"));
        }

        [Test]
        public void DocumentationUsesTheMachineFacade()
        {
            foreach (var readme in new[] { "README.md", Path.Combine("Samples~", "CodeFirst", "README.md") })
            {
                var text = File.ReadAllText(Path.Combine(PackageRoot, readme));

                Assert.That(text, Does.Contain("Machine"), readme);
                Assert.That(text, Does.Contain(".Attach("), readme);
            }
        }

        [Test]
        public void NothingReferencesTheRemovedApis()
        {
            foreach (var file in EnumerateSources())
            {
                var text = File.ReadAllText(file);
                Assert.That(text, Does.Not.Contain(OldFacadeTerm), file);
                Assert.That(text, Does.Not.Contain(AmbiguityTerm), file);
            }
        }

        private static void AssertRuntimeFree(string term)
        {
            var runtime = Path.Combine(PackageRoot, "Runtime");

            foreach (var file in Directory.GetFiles(runtime, "*.cs", SearchOption.AllDirectories))
            {
                Assert.That(File.ReadAllText(file), Does.Not.Contain(term), file);
            }
        }

        private static string[] EnumerateSources()
        {
            var files = new System.Collections.Generic.List<string>();

            foreach (var folder in new[] { "Runtime", "Editor", "Tests", "Samples~" })
            {
                var path = Path.Combine(PackageRoot, folder);
                if (!Directory.Exists(path))
                {
                    continue;
                }

                files.AddRange(Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories));
                files.AddRange(Directory.GetFiles(path, "*.md", SearchOption.AllDirectories));
                files.AddRange(Directory.GetFiles(path, "*.asmdef", SearchOption.AllDirectories));
            }

            files.Add(Path.Combine(PackageRoot, "package.json"));
            files.Add(Path.Combine(PackageRoot, "README.md"));

            // This file names the forbidden terms in order to look for them.
            files.RemoveAll(file => Path.GetFileName(file) == "PackageQualityTests.cs");

            return files.ToArray();
        }
    }
}
