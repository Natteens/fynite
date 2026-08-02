using System;

namespace Fynite.Authoring
{
    /// <summary>How badly a diagnostic affects the graph.</summary>
    public enum FyniteDiagnosticSeverity
    {
        /// <summary>The graph still compiles, but something deserves attention.</summary>
        Warning = 0,

        /// <summary>The graph cannot be compiled.</summary>
        Error = 1
    }

    /// <summary>Which part of the graph a diagnostic is about.</summary>
    public enum FyniteDiagnosticCategory
    {
        /// <summary>The document format itself.</summary>
        Schema = 0,

        /// <summary>The graph as a whole.</summary>
        Graph = 1,

        /// <summary>The context type.</summary>
        Context = 2,

        /// <summary>The state tree.</summary>
        Hierarchy = 3,

        /// <summary>Signal declarations.</summary>
        Signal = 4,

        /// <summary>Reaction declarations.</summary>
        Reaction = 5,

        /// <summary>Action, guard and effect occurrences.</summary>
        Block = 6
    }

    /// <summary>
    /// One problem found in a graph, addressed to the element that caused it.
    /// </summary>
    /// <remarks>
    /// Every diagnostic carries the GUID of the element it belongs to, which is what lets the editor
    /// select and frame that element instead of only printing a message. The code is stable across
    /// versions so it can be searched for and referred to.
    /// </remarks>
    public readonly struct FyniteDiagnostic
    {
        /// <summary>Creates a diagnostic.</summary>
        public FyniteDiagnostic(
            string code,
            FyniteDiagnosticSeverity severity,
            FyniteDiagnosticCategory category,
            string message,
            string elementGuid = null,
            string elementName = null,
            string elementKind = null)
        {
            Code = code;
            Severity = severity;
            Category = category;
            Message = message;
            ElementGuid = elementGuid;
            ElementName = elementName;
            ElementKind = elementKind;
        }

        /// <summary>Stable identifier, for example <c>FYN0301</c>.</summary>
        public string Code { get; }

        /// <summary>Whether this stops compilation.</summary>
        public FyniteDiagnosticSeverity Severity { get; }

        /// <summary>Which part of the graph it concerns.</summary>
        public FyniteDiagnosticCategory Category { get; }

        /// <summary>Human-readable explanation.</summary>
        public string Message { get; }

        /// <summary>Identity of the offending element, or null when it is about the graph as a whole.</summary>
        public string ElementGuid { get; }

        /// <summary>Display name of the offending element when it has one.</summary>
        public string ElementName { get; }

        /// <summary>Kind of the offending element, for example <c>state</c>.</summary>
        public string ElementKind { get; }

        /// <summary>True when this diagnostic prevents compilation.</summary>
        public bool IsError => Severity == FyniteDiagnosticSeverity.Error;

        /// <summary>Creates a diagnostic addressed to an authoring element.</summary>
        public static FyniteDiagnostic For(
            IFyniteAuthoringElement element,
            string code,
            FyniteDiagnosticSeverity severity,
            FyniteDiagnosticCategory category,
            string message)
        {
            return new FyniteDiagnostic(
                code,
                severity,
                category,
                message,
                element?.Guid,
                element?.DisplayName,
                element?.ElementKind);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var prefix = Severity == FyniteDiagnosticSeverity.Error ? "error" : "warning";
            var where = string.IsNullOrEmpty(ElementName)
                ? (string.IsNullOrEmpty(ElementKind) ? null : ElementKind)
                : ElementKind + " '" + ElementName + "'";

            return where == null
                ? prefix + " " + Code + ": " + Message
                : prefix + " " + Code + " (" + where + "): " + Message;
        }
    }

    /// <summary>
    /// Every diagnostic code Fynite can report.
    /// </summary>
    /// <remarks>
    /// Codes are grouped by category and never reused, so a code that disappears from this list stays
    /// gone rather than coming back meaning something else.
    /// </remarks>
    public static class FyniteDiagnosticCodes
    {
        // Schema — 01xx
        /// <summary>The document declares no schema version, or a version below one.</summary>
        public const string SchemaVersionMissing = "FYN0101";

        /// <summary>The document was written by a newer Fynite than this one.</summary>
        public const string SchemaVersionFromFuture = "FYN0102";

        /// <summary>The document was migrated from an older schema version.</summary>
        public const string SchemaMigrated = "FYN0103";

        /// <summary>The document could not be read at all.</summary>
        public const string SchemaUnreadable = "FYN0104";

        // Graph — 02xx
        /// <summary>The graph declares no states.</summary>
        public const string GraphEmpty = "FYN0201";

        /// <summary>The graph has no persistent identity.</summary>
        public const string GraphGuidMissing = "FYN0202";

        /// <summary>Two elements share one identity.</summary>
        public const string DuplicateGuid = "FYN0203";

        /// <summary>An element has no identity.</summary>
        public const string MissingGuid = "FYN0204";

        // Context — 03xx
        /// <summary>No context type was selected.</summary>
        public const string ContextMissing = "FYN0301";

        /// <summary>The selected context type no longer exists.</summary>
        public const string ContextTypeNotFound = "FYN0302";

        /// <summary>The selected context type cannot be a context.</summary>
        public const string ContextTypeInvalid = "FYN0303";

        /// <summary>The context type cannot be assigned to a runner's context field.</summary>
        public const string ContextNotRunnable = "FYN0304";

        /// <summary>
        /// A graph written before the context became graph metadata carries more than one settings node,
        /// and they disagree about the context type.
        /// </summary>
        public const string LegacyContextConflict = "FYN0305";

        /// <summary>The context type was lifted out of a legacy settings node into the graph itself.</summary>
        public const string LegacyContextMigrated = "FYN0306";

        // Hierarchy — 04xx
        /// <summary>No state is marked as the root.</summary>
        public const string RootMissing = "FYN0401";

        /// <summary>More than one state is marked as the root.</summary>
        public const string RootDuplicate = "FYN0402";

        /// <summary>A state has an empty name.</summary>
        public const string StateNameEmpty = "FYN0403";

        /// <summary>A non-root state has no parent.</summary>
        public const string StateOrphan = "FYN0404";

        /// <summary>A state points at a parent that does not exist.</summary>
        public const string ParentNotFound = "FYN0405";

        /// <summary>A state is its own parent.</summary>
        public const string ParentSelf = "FYN0406";

        /// <summary>A chain of parents loops back on itself.</summary>
        public const string HierarchyCycle = "FYN0407";

        /// <summary>A state cannot be reached from the root.</summary>
        public const string StateUnreachable = "FYN0408";

        /// <summary>A state with children declares no initial child.</summary>
        public const string InitialMissing = "FYN0409";

        /// <summary>A state with children declares more than one initial child.</summary>
        public const string InitialDuplicate = "FYN0410";

        /// <summary>A leaf state is marked as an initial child of nothing, or declares one.</summary>
        public const string InitialOnLeaf = "FYN0411";

        /// <summary>The root is marked as an initial child.</summary>
        public const string RootMarkedInitial = "FYN0412";

        /// <summary>
        /// A graph written before the root became its own node kind declares more than one root, so
        /// which one to turn into the structural root cannot be decided without guessing.
        /// </summary>
        public const string LegacyRootAmbiguous = "FYN0413";

        /// <summary>
        /// A legacy root carried behaviour a structural root cannot, so it was kept as an ordinary state
        /// underneath a newly created structural root.
        /// </summary>
        public const string LegacyRootPreserved = "FYN0414";

        /// <summary>A legacy root was converted straight into a structural root node.</summary>
        public const string LegacyRootConverted = "FYN0415";

        // Signals — 05xx
        /// <summary>A signal has an empty name.</summary>
        public const string SignalNameEmpty = "FYN0501";

        /// <summary>Two signals share one name, which makes diagnostics ambiguous.</summary>
        public const string SignalNameDuplicate = "FYN0502";

        /// <summary>A signal's payload type no longer exists.</summary>
        public const string PayloadTypeNotFound = "FYN0503";

        /// <summary>A signal's payload type cannot be used as a payload.</summary>
        public const string PayloadTypeInvalid = "FYN0504";

        // Reactions — 06xx
        /// <summary>A reaction declares no source state.</summary>
        public const string ReactionSourceMissing = "FYN0601";

        /// <summary>A reaction's source state does not exist.</summary>
        public const string ReactionSourceNotFound = "FYN0602";

        /// <summary>A reaction declares no signal.</summary>
        public const string ReactionSignalMissing = "FYN0603";

        /// <summary>A reaction's signal does not exist.</summary>
        public const string ReactionSignalNotFound = "FYN0604";

        /// <summary>A reaction's target state does not exist.</summary>
        public const string ReactionTargetNotFound = "FYN0605";

        /// <summary>Two reactions declare the same explicit order, so their tie-break is arbitrary.</summary>
        public const string ReactionOrderAmbiguous = "FYN0606";

        // Blocks — 07xx
        /// <summary>A block occurrence selects no type.</summary>
        public const string BlockTypeMissing = "FYN0701";

        /// <summary>A block occurrence's type no longer exists.</summary>
        public const string BlockTypeNotFound = "FYN0702";

        /// <summary>A block type cannot be instantiated.</summary>
        public const string BlockTypeNotConstructible = "FYN0703";

        /// <summary>A block type is not compatible with the graph's context type.</summary>
        public const string BlockContextIncompatible = "FYN0704";

        /// <summary>A guard was used where an action is required, or the other way round.</summary>
        public const string BlockWrongKind = "FYN0705";

        /// <summary>A block's stored configuration could not be applied to its current type.</summary>
        public const string BlockConfigInvalid = "FYN0706";

        // Core — 08xx
        /// <summary>The core builder rejected the compiled graph.</summary>
        public const string BuilderRejected = "FYN0801";

        /// <summary>The compiler hit an unexpected error.</summary>
        public const string InternalError = "FYN0802";
    }
}
