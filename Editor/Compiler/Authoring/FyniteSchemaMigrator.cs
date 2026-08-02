using System.Collections.Generic;

namespace Fynite.Authoring
{
    /// <summary>
    /// Brings older authoring documents up to the current schema version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Migration is deterministic and forward-only: each step upgrades exactly one version, they run in
    /// order, and none of them looks at anything outside the document. Running the same migration twice
    /// on the same input produces the same output.
    /// </para>
    /// <para>
    /// A document from a <em>newer</em> schema is refused rather than read on a best-effort basis. Field
    /// names surviving does not mean their meaning survived, and quietly reinterpreting a graph is worse
    /// than telling the user to upgrade.
    /// </para>
    /// </remarks>
    public static class FyniteSchemaMigrator
    {
        /// <summary>
        /// Decides whether a schema version can be handled at all.
        /// </summary>
        /// <returns>False when the document must be refused.</returns>
        public static bool CheckVersion(int version, List<FyniteDiagnostic> diagnostics)
        {
            if (version < FyniteGraphDocument.MinimumSupportedSchemaVersion)
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.SchemaVersionMissing,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Schema,
                    version <= 0
                        ? "The graph declares no schema version. It was not written by Fynite, or it was truncated."
                        : "The graph declares schema version " + version + ", which is older than the oldest " +
                          "version this build can migrate (" + FyniteGraphDocument.MinimumSupportedSchemaVersion + ")."));
                return false;
            }

            if (version > FyniteGraphDocument.CurrentSchemaVersion)
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.SchemaVersionFromFuture,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Schema,
                    "The graph was written with schema version " + version + " but this build of Fynite only " +
                    "understands up to version " + FyniteGraphDocument.CurrentSchemaVersion +
                    ". Upgrade Fynite rather than opening it, so the graph is not reinterpreted."));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Upgrades a document in place, recording what was changed.
        /// </summary>
        /// <returns>True when the document was modified.</returns>
        public static bool Migrate(FyniteGraphDocument document, List<FyniteDiagnostic> diagnostics)
        {
            if (document == null || document.schemaVersion >= FyniteGraphDocument.CurrentSchemaVersion)
            {
                return false;
            }

            int from = document.schemaVersion;

            while (document.schemaVersion < FyniteGraphDocument.CurrentSchemaVersion)
            {
                switch (document.schemaVersion)
                {
                    case 1:
                        MigrateFrom1To2(document);
                        break;
                    default:
                        // Nothing knows how to move forward from here; stop rather than loop.
                        return document.schemaVersion != from;
                }
            }

            diagnostics?.Add(new FyniteDiagnostic(
                FyniteDiagnosticCodes.SchemaMigrated,
                FyniteDiagnosticSeverity.Warning,
                FyniteDiagnosticCategory.Schema,
                "The graph was migrated from schema version " + from + " to " +
                FyniteGraphDocument.CurrentSchemaVersion + ". Save it to write the new schema."));

            return true;
        }

        /// <summary>
        /// Version 1 had no explicit reaction order and relied on list position, which meant reordering
        /// the list silently changed resolution. Version 2 makes the order a stored field; the migration
        /// freezes the order the version 1 document already had.
        /// </summary>
        private static void MigrateFrom1To2(FyniteGraphDocument document)
        {
            var reactions = document.reactions;
            if (reactions != null)
            {
                for (int i = 0; i < reactions.Count; i++)
                {
                    if (reactions[i] != null)
                    {
                        reactions[i].order = i;
                    }
                }
            }

            document.schemaVersion = 2;
        }
    }
}
