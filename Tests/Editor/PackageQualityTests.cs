using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
            Assert.That(
                definition.Replace(" ", "").Replace("\n", "").Replace("\r", ""),
                Does.Contain("\"includePlatforms\":[\"Editor\"]"),
                "the assembly is not restricted to the Editor platform");
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

        /// <summary>
        /// Every relative link in the shipped Markdown points at a file that exists, spelled the way
        /// the file system spells it. A link that only works on a case-insensitive disk is broken.
        /// </summary>
        [Test]
        public void EveryRelativeDocumentationLinkResolves()
        {
            var broken = new List<string>();

            foreach (var document in EnumerateDocuments())
            {
                var folder = Path.GetDirectoryName(document);

                foreach (Match match in Regex.Matches(File.ReadAllText(document), @"\]\(([^)]+)\)"))
                {
                    var target = match.Groups[1].Value;

                    if (target.StartsWith("http", StringComparison.Ordinal) ||
                        target.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var path = Path.GetFullPath(
                        Path.Combine(folder, target.Split('#')[0]));

                    if (!ExistsWithThisExactName(path))
                    {
                        broken.Add($"{Path.GetFileName(document)} -> {target}");
                    }
                }
            }

            Assert.That(broken, Is.Empty);
        }

        /// <summary>Nothing the package ships still points at something the package removed.</summary>
        [Test]
        public void DocumentationDoesNotNameAnythingThatIsGone()
        {
            var gone = new[]
            {
                "FyniteActivity" + "Plan",
                "Window > Fynite",
                "Window/Fynite",
                "HasMovement.cs",
                "HasNoMovement.cs"
            };

            foreach (var document in EnumerateDocuments())
            {
                var text = File.ReadAllText(document);

                foreach (var term in gone)
                {
                    Assert.That(text, Does.Not.Contain(term), document);
                }
            }
        }

        /// <summary>
        /// Unity writes a <c>.meta</c> beside everything it imports and nothing else. An orphan means a
        /// file was deleted without its meta; a missing one means the asset arrives without its GUID.
        /// </summary>
        [Test]
        public void EveryImportedAssetHasItsMetaAndNoMetaIsOrphaned()
        {
            var missing = new List<string>();
            var orphaned = new List<string>();

            foreach (var folder in new[] { "Runtime", "Editor", "Tests" })
            {
                Audit(Path.Combine(PackageRoot, folder), missing, orphaned);
            }

            Audit(PackageRoot, missing, orphaned, recurse: false);

            Assert.That(missing, Is.Empty, "assets without a .meta");
            Assert.That(orphaned, Is.Empty, "meta files without an asset");
        }

        /// <summary>
        /// Unity ignores a folder whose name ends in <c>~</c>, so the documentation and the sample do
        /// not arrive as imported assets in a consumer's Project Browser.
        /// </summary>
        [Test]
        public void DocumentationAndSamplesAreHiddenFromTheAssetDatabase()
        {
            Assert.That(Directory.Exists(Path.Combine(PackageRoot, "Documentation~")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(PackageRoot, "docs")), Is.False);
            Assert.That(File.Exists(Path.Combine(PackageRoot, "docs.meta")), Is.False);

            foreach (var hidden in new[] { "Documentation~", "Samples~" })
            {
                var metas = Directory.GetFiles(
                    Path.Combine(PackageRoot, hidden),
                    "*.meta",
                    SearchOption.AllDirectories);

                Assert.That(metas, Is.Empty, $"{hidden} is hidden, so its meta files mean nothing");
            }
        }

        private static void Audit(
            string folder,
            List<string> missing,
            List<string> orphaned,
            bool recurse = true)
        {
            var search = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var file in Directory.GetFiles(folder, "*", search))
            {
                // Unity ignores anything whose name starts with a dot, so it never writes a meta for it.
                if (Path.GetFileName(file).StartsWith(".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (file.EndsWith(".meta", StringComparison.Ordinal))
                {
                    if (!File.Exists(file.Substring(0, file.Length - 5)) &&
                        !Directory.Exists(file.Substring(0, file.Length - 5)))
                    {
                        orphaned.Add(file);
                    }

                    continue;
                }

                if (!File.Exists(file + ".meta"))
                {
                    missing.Add(file);
                }
            }

            if (!recurse)
            {
                return;
            }

            foreach (var nested in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories))
            {
                if (!File.Exists(nested + ".meta"))
                {
                    missing.Add(nested);
                }
            }
        }

        private static bool ExistsWithThisExactName(string path)
        {
            var folder = Path.GetDirectoryName(path);

            if (folder == null || !Directory.Exists(folder))
            {
                return false;
            }

            var name = Path.GetFileName(path);

            foreach (var entry in Directory.GetFileSystemEntries(folder))
            {
                if (string.Equals(Path.GetFileName(entry), name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] EnumerateDocuments()
        {
            var documents = new List<string>
            {
                Path.Combine(PackageRoot, "README.md"),
                Path.Combine(PackageRoot, "CHANGELOG.md"),
                Path.Combine(PackageRoot, "Samples~", "CodeFirst", "README.md")
            };

            documents.AddRange(Directory.GetFiles(
                Path.Combine(PackageRoot, "Documentation~"),
                "*.md",
                SearchOption.AllDirectories));

            return documents.ToArray();
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
