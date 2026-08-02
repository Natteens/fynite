using System.Collections.Generic;

namespace Fynite.Authoring
{
    /// <summary>Checks the single authoring schema understood by this build.</summary>
    public static class FyniteSchema
    {
        public static bool CheckCurrent(int version, List<FyniteDiagnostic> diagnostics)
        {
            if (version == FyniteGraphDocument.CurrentSchemaVersion)
            {
                return true;
            }

            diagnostics?.Add(new FyniteDiagnostic(
                version <= 0
                    ? FyniteDiagnosticCodes.SchemaVersionMissing
                    : version > FyniteGraphDocument.CurrentSchemaVersion
                        ? FyniteDiagnosticCodes.SchemaVersionFromFuture
                        : FyniteDiagnosticCodes.SchemaVersionUnsupported,
                FyniteDiagnosticSeverity.Error,
                FyniteDiagnosticCategory.Schema,
                "Unsupported Fynite schema version " + version + ". This build accepts only version " +
                FyniteGraphDocument.CurrentSchemaVersion + "."));
            return false;
        }
    }
}
