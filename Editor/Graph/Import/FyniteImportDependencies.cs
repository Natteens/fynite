using System.Text;
using Fynite.Authoring;
using UnityEditor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Makes graph imports depend on the shape of the block and context types they compile against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A graph's compiled output is a function of the file <em>and</em> of the types it names. Deleting
    /// a block class must turn a healthy graph into a reporting one; adding a field to a block must show
    /// up as a new configuration option. Without a declared dependency the importer would keep serving
    /// its cached result and the graph would look fine while being wrong.
    /// </para>
    /// <para>
    /// The dependency is a hash over every block and context type currently available, so it changes
    /// exactly when that set changes — not on every recompile, which would reimport every graph in the
    /// project for no reason and is what an import loop is made of.
    /// </para>
    /// </remarks>
    public static class FyniteImportDependencies
    {
        /// <summary>Name of the custom dependency graph imports register against.</summary>
        public const string ScriptsDependency = "Fynite/BlockAndContextTypes";

        [InitializeOnLoadMethod]
        private static void Register()
        {
            // Runs after every script reload, which is exactly when the answer can have changed.
            AssetDatabase.RegisterCustomDependency(ScriptsDependency, ComputeHash());
        }

        private static Hash128 ComputeHash()
        {
            var text = new StringBuilder();

            var blocks = FyniteBlockCatalog.All;
            for (int i = 0; i < blocks.Count; i++)
            {
                var info = blocks[i];
                text.Append(info.Kind).Append(' ').Append(info.Reference.Identity);

                for (int c = 0; c < info.ContextContracts.Length; c++)
                {
                    text.Append('<').Append(info.ContextContracts[c].FullName).Append('>');
                }

                // Field shape matters too: adding a field to a block changes what its occurrences can
                // store, so a graph using it has to be recompiled.
                foreach (var field in FyniteBlockNodeBase.EnumerateConfigurableFields(info.Type))
                {
                    text.Append('|').Append(field.Name).Append(':').Append(field.FieldType.FullName);
                }

                text.Append('\n');
            }

            var contexts = FyniteContextCatalog.All;
            for (int i = 0; i < contexts.Count; i++)
            {
                text.Append("ctx ").Append(contexts[i].FullName).Append('\n');
            }

            return Hash128.Compute(text.ToString());
        }
    }
}
