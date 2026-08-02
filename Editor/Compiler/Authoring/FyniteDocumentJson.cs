using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fynite.Authoring
{
    /// <summary>
    /// Reads and writes the authoring model as JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>.fyn</c> file on disk is a Graph Toolkit asset, so this is not what is stored there. It
    /// exists because the document is the compiler's real input: tests build documents directly, the
    /// sample ships one, and a graph can be exported to a readable form for diffing and bug reports
    /// without anyone having to read Graph Toolkit's node YAML.
    /// </para>
    /// <para>
    /// Unknown fields are ignored rather than rejected, so a document written by a newer build with
    /// extra data still parses — but the schema version is checked first, because ignoring fields is
    /// only safe when the meaning of the fields that <em>are</em> understood has not changed.
    /// </para>
    /// </remarks>
    public static class FyniteDocumentJson
    {
        [Serializable]
        private struct VersionProbe
        {
            public int schemaVersion;
        }

        /// <summary>Serializes a document. The output is stable for a given document.</summary>
        public static string ToJson(FyniteGraphDocument document, bool prettyPrint = true)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return JsonUtility.ToJson(document, prettyPrint);
        }

        /// <summary>
        /// Parses a document in the current schema version.
        /// </summary>
        /// <param name="json">Serialized document.</param>
        /// <param name="document">The parsed document, or null on failure.</param>
        /// <param name="diagnostics">Everything noticed while reading; may contain warnings on success.</param>
        /// <returns>False when the document cannot be used at all.</returns>
        public static bool TryParse(string json, out FyniteGraphDocument document, out IReadOnlyList<FyniteDiagnostic> diagnostics)
        {
            var found = new List<FyniteDiagnostic>();
            diagnostics = found;
            document = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                found.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.SchemaUnreadable,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Schema,
                    "The graph document is empty."));
                return false;
            }

            // The version is read on its own first: deciding whether the rest of the payload can be
            // trusted has to happen before interpreting it.
            int version;
            try
            {
                version = JsonUtility.FromJson<VersionProbe>(json).schemaVersion;
            }
            catch (Exception exception)
            {
                found.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.SchemaUnreadable,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Schema,
                    "The graph document is not valid JSON: " + exception.Message));
                return false;
            }

            if (!FyniteSchema.CheckCurrent(version, found))
            {
                return false;
            }

            FyniteGraphDocument parsed;
            try
            {
                parsed = JsonUtility.FromJson<FyniteGraphDocument>(json);
            }
            catch (Exception exception)
            {
                found.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.SchemaUnreadable,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Schema,
                    "The graph document could not be read: " + exception.Message));
                return false;
            }

            if (parsed == null)
            {
                found.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.SchemaUnreadable,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Schema,
                    "The graph document parsed to nothing."));
                return false;
            }

            Normalize(parsed);
            document = parsed;
            return true;
        }

        /// <summary>Fills in the collections Unity's serializer leaves null for an absent field.</summary>
        internal static void Normalize(FyniteGraphDocument document)
        {
            document.states = document.states ?? new List<FyniteStateDocument>();
            document.signals = document.signals ?? new List<FyniteSignalDocument>();
            document.reactions = document.reactions ?? new List<FyniteReactionDocument>();

            for (int i = 0; i < document.states.Count; i++)
            {
                var state = document.states[i];
                if (state == null)
                {
                    continue;
                }

                state.enter = state.enter ?? new List<FyniteBlockDocument>();
                state.tick = state.tick ?? new List<FyniteBlockDocument>();
                state.fixedTick = state.fixedTick ?? new List<FyniteBlockDocument>();
                state.exit = state.exit ?? new List<FyniteBlockDocument>();

                // Unity's serializer writes a null string as an empty one, so "no parent" would come
                // back in two different shapes. Collapsing them keeps a round-tripped document equal to
                // the one that was written, and keeps every "is it set" check to one form.
                state.parentGuid = Blank(state.parentGuid);
            }

            for (int i = 0; i < document.reactions.Count; i++)
            {
                var reaction = document.reactions[i];
                if (reaction == null)
                {
                    continue;
                }

                reaction.guards = reaction.guards ?? new List<FyniteBlockDocument>();
                reaction.effects = reaction.effects ?? new List<FyniteBlockDocument>();

                reaction.sourceGuid = Blank(reaction.sourceGuid);
                reaction.signalGuid = Blank(reaction.signalGuid);
                reaction.targetGuid = Blank(reaction.targetGuid);
            }
        }

        private static string Blank(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
