using System;
using System.Collections.Generic;
using Fynite.Diagnostics;
using UnityEngine;

namespace Fynite
{
    /// <summary>
    /// The runtime artefact the <c>.fyn</c> importer produces: a compiled graph that a
    /// <see cref="FyniteRunner"/> can execute, with no trace of the authoring tool left in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing here knows about Graph Toolkit, GUID-keyed authoring elements, node positions or the
    /// editor at all. The asset stores three things: the dense structure, one prototype per authored
    /// block occurrence, and the <see cref="FyniteContextBinder"/> that carries the static context type.
    /// </para>
    /// <para>
    /// The asset is deliberately non-generic even though <see cref="FyniteDefinition{TContext}"/> is
    /// generic. A closed generic <c>ScriptableObject</c> cannot be referenced from a scene, so the
    /// context type travels in the <c>[SerializeReference]</c> binder instead — see
    /// <see cref="FyniteContextBinder"/> for why that is the piece that makes reload and player builds
    /// work without reflection.
    /// </para>
    /// </remarks>
    public sealed class FyniteGraphAsset : FyniteDefinitionAsset, ISerializationCallbackReceiver
    {
        [Tooltip("Compiled structure: states, signals and reactions as dense indices.")]
        [SerializeField]
        private FyniteRuntimeGraph graph = new FyniteRuntimeGraph();

        [Tooltip("Carries the static context type across serialization.")]
        [SerializeReference]
        private FyniteContextBinder binder;

        [Tooltip("One configured prototype per authored block occurrence.")]
        [SerializeReference]
        private FyniteBlock[] blocks = Array.Empty<FyniteBlock>();

        [Tooltip("Declared payload type per signal, index-aligned with the compiled signals.")]
        [SerializeReference]
        private FynitePayloadType[] payloads = Array.Empty<FynitePayloadType>();

        [Tooltip("Schema version of the authoring graph this was compiled from.")]
        [SerializeField]
        private int schemaVersion;

        [Tooltip("Set when the importer could not compile the graph; the asset then refuses to run.")]
        [SerializeField]
        private string compilationError;

        [NonSerialized]
        private IFyniteDefinition _definition;

        [NonSerialized]
        private Exception _buildFailure;

        [NonSerialized]
        private Dictionary<string, int> _signalsByGuid;

        /// <summary>Persistent identity of the authoring graph this was compiled from.</summary>
        public string GraphGuid => graph != null ? graph.graphGuid : null;

        /// <summary>Schema version of the authoring graph this was compiled from.</summary>
        public int SchemaVersion => schemaVersion;

        /// <summary>
        /// Why the last import failed to compile, or null when the asset holds a usable graph.
        /// </summary>
        /// <remarks>
        /// A failed import deliberately leaves an <em>empty</em> asset carrying this message rather than
        /// the previously compiled graph, so nothing can keep silently executing a stale definition
        /// after an edit broke the graph.
        /// </remarks>
        public string CompilationError => string.IsNullOrEmpty(compilationError) ? null : compilationError;

        /// <summary>True when the asset holds a graph that can be built.</summary>
        public bool IsExecutable => CompilationError == null && binder != null && graph != null && graph.states != null && graph.states.Length > 0;

        /// <inheritdoc />
        public override Type ContextType => binder?.ContextType;

        /// <inheritdoc />
        public override IFyniteDefinition Definition => GetOrBuildDefinition();

        /// <summary>
        /// Fills the asset with a compilation result. Called by the importer; not part of the runtime API.
        /// </summary>
        public void SetCompiled(
            FyniteRuntimeGraph compiledGraph,
            FyniteContextBinder contextBinder,
            FyniteBlock[] blockPrototypes,
            FynitePayloadType[] payloadTypes,
            int authoringSchemaVersion)
        {
            graph = compiledGraph ?? new FyniteRuntimeGraph();
            binder = contextBinder;
            blocks = blockPrototypes ?? Array.Empty<FyniteBlock>();
            payloads = payloadTypes ?? Array.Empty<FynitePayloadType>();
            schemaVersion = authoringSchemaVersion;
            compilationError = null;
            InvalidateCache();
        }

        /// <summary>
        /// Marks the asset as not executable. Called by the importer when compilation failed; not part
        /// of the runtime API.
        /// </summary>
        public void SetCompilationFailed(string message, string graphGuid, int authoringSchemaVersion)
        {
            graph = new FyniteRuntimeGraph { graphGuid = graphGuid };
            binder = null;
            blocks = Array.Empty<FyniteBlock>();
            payloads = Array.Empty<FynitePayloadType>();
            schemaVersion = authoringSchemaVersion;
            compilationError = string.IsNullOrEmpty(message) ? "The graph could not be compiled." : message;
            InvalidateCache();
        }

        /// <inheritdoc />
        public override IFyniteMachine CreateMachine(object context, IFyniteDiagnostics diagnostics = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var definition = GetOrBuildDefinition();
            return binder.CreateMachine(definition, context, diagnostics);
        }

        /// <summary>
        /// Resolves a persistent signal identity to the dense handle the machine dispatches on.
        /// </summary>
        /// <remarks>
        /// This is the only place a string reaches signal identity, and callers are expected to do it
        /// once when they bind — never per frame. Everything afterwards uses the returned handle.
        /// </remarks>
        /// <returns>False when this graph declares no signal with that identity.</returns>
        public bool TryResolveSignal(string signalGuid, out SignalHandle signal)
        {
            signal = SignalHandle.Invalid;
            if (string.IsNullOrEmpty(signalGuid) || !IsExecutable)
            {
                return false;
            }

            if (_signalsByGuid == null)
            {
                var map = new Dictionary<string, int>(graph.signals.Length, StringComparer.Ordinal);
                for (int i = 0; i < graph.signals.Length; i++)
                {
                    var guid = graph.signals[i].guid;
                    if (!string.IsNullOrEmpty(guid))
                    {
                        map[guid] = i;
                    }
                }

                _signalsByGuid = map;
            }

            if (!_signalsByGuid.TryGetValue(signalGuid, out int index))
            {
                return false;
            }

            var definition = GetOrBuildDefinition();
            if (index >= definition.SignalCount)
            {
                return false;
            }

            // Handles are dense and allocated in compilation order, so the compiled index is the handle
            // index; going through the definition keeps the owner tag correct.
            signal = definition.GetSignal(index);
            return signal.IsValid;
        }

        /// <summary>Diagnostic label of a signal, by its persistent identity.</summary>
        public string GetSignalName(string signalGuid)
        {
            if (graph?.signals == null)
            {
                return null;
            }

            for (int i = 0; i < graph.signals.Length; i++)
            {
                if (string.Equals(graph.signals[i].guid, signalGuid, StringComparison.Ordinal))
                {
                    return graph.signals[i].name;
                }
            }

            return null;
        }

        /// <summary>Persistent identities of every signal this graph declares, in compilation order.</summary>
        public IReadOnlyList<FyniteRuntimeSignal> Signals =>
            graph?.signals ?? Array.Empty<FyniteRuntimeSignal>();

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // A reimport or a domain reload replaces the serialized data; anything derived from it has
            // to be rebuilt rather than reused.
            InvalidateCache();
        }

        private void InvalidateCache()
        {
            _definition = null;
            _buildFailure = null;
            _signalsByGuid = null;
        }

        private IFyniteDefinition GetOrBuildDefinition()
        {
            if (_buildFailure != null)
            {
                throw new InvalidOperationException(
                    "Fynite graph asset '" + name + "' failed to build: " + _buildFailure.Message, _buildFailure);
            }

            if (_definition != null)
            {
                return _definition;
            }

            if (CompilationError != null)
            {
                throw new InvalidOperationException(
                    "Fynite graph asset '" + name + "' did not compile: " + CompilationError +
                    " Fix the graph and let it reimport before running it.");
            }

            if (binder == null)
            {
                throw new InvalidOperationException(
                    "Fynite graph asset '" + name + "' has no context binder. It was never compiled, or the " +
                    "context type it was compiled against no longer exists.");
            }

            try
            {
                _definition = binder.Build(graph, blocks, payloads);
            }
            catch (Exception exception)
            {
                _buildFailure = exception;
                throw;
            }

            return _definition;
        }
    }
}
