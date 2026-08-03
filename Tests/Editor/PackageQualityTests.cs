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

            foreach (var folder in new[] { "Runtime", "Tests", "Samples~" })
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
