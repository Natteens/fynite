using System;
using UnityEngine;

namespace Fynite
{
    /// <summary>
    /// A serializable pointer to one signal of one compiled graph, safe to put on a component.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gameplay code needs to raise <c>Move</c> without hardcoding the string <c>"Move"</c>, without a
    /// dense index that shifts when the author adds a signal, and without serializing a
    /// <see cref="SignalHandle"/> (whose owner tag is only meaningful inside one process).
    /// </para>
    /// <para>
    /// This struct stores the graph asset as an object reference and the signal's persistent authoring
    /// GUID. Renaming or moving either survives; deleting the signal is detected and reported instead of
    /// silently pointing at whatever now sits at that index.
    /// </para>
    /// <para>
    /// <see cref="Resolve"/> turns the GUID into a dense handle exactly once and caches it. Every
    /// subsequent raise goes through the handle, so no string ever reaches the dispatch path.
    /// </para>
    /// </remarks>
    [Serializable]
    public struct FyniteSignalReference : IEquatable<FyniteSignalReference>
    {
        [Tooltip("Compiled graph that declares the signal.")]
        [SerializeField]
        private FyniteGraphAsset graph;

        [Tooltip("Persistent identity of the signal inside that graph.")]
        [SerializeField]
        private string signalGuid;

        [NonSerialized]
        private SignalHandle _resolved;

        [NonSerialized]
        private FyniteGraphAsset _resolvedAgainst;

        /// <summary>Creates a reference to a signal of a graph.</summary>
        public FyniteSignalReference(FyniteGraphAsset graphAsset, string guid)
        {
            graph = graphAsset;
            signalGuid = guid;
            _resolved = SignalHandle.Invalid;
            _resolvedAgainst = null;
        }

        /// <summary>The graph this reference points into, or null when nothing was assigned.</summary>
        public FyniteGraphAsset Graph => graph;

        /// <summary>Persistent identity of the referenced signal.</summary>
        public string SignalGuid => signalGuid;

        /// <summary>True when both a graph and a signal identity were assigned.</summary>
        public bool IsAssigned => graph != null && !string.IsNullOrEmpty(signalGuid);

        /// <summary>Diagnostic label of the referenced signal, or null when it cannot be found.</summary>
        public string SignalName => graph != null ? graph.GetSignalName(signalGuid) : null;

        /// <summary>
        /// Resolves the dense handle, caching it for subsequent calls.
        /// </summary>
        /// <param name="signal">The resolved handle, or <see cref="SignalHandle.Invalid"/> on failure.</param>
        /// <param name="error">Why resolution failed, or null on success.</param>
        /// <returns>True when the handle can be used.</returns>
        public bool TryResolve(out SignalHandle signal, out string error)
        {
            if (_resolved.IsValid && ReferenceEquals(_resolvedAgainst, graph))
            {
                signal = _resolved;
                error = null;
                return true;
            }

            signal = SignalHandle.Invalid;

            if (graph == null)
            {
                error = "No Fynite graph is assigned to this signal reference.";
                return false;
            }

            if (string.IsNullOrEmpty(signalGuid))
            {
                error = "No signal is selected on this reference to graph '" + graph.name + "'.";
                return false;
            }

            if (graph.CompilationError != null)
            {
                error = "Graph '" + graph.name + "' did not compile: " + graph.CompilationError;
                return false;
            }

            if (!graph.TryResolveSignal(signalGuid, out signal))
            {
                error = "Graph '" + graph.name + "' declares no signal with identity '" + signalGuid +
                        "'. It was most likely deleted after this reference was made.";
                return false;
            }

            _resolved = signal;
            _resolvedAgainst = graph;
            error = null;
            return true;
        }

        /// <summary>Resolves the dense handle, throwing a descriptive error when that is not possible.</summary>
        /// <exception cref="InvalidOperationException">The reference does not point at a usable signal.</exception>
        public SignalHandle Resolve()
        {
            if (!TryResolve(out var signal, out string error))
            {
                throw new InvalidOperationException(error);
            }

            return signal;
        }

        /// <summary>
        /// Raises the referenced payload-less signal on a machine.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="machine"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The reference cannot be resolved, or the machine runs a different graph.</exception>
        public void Raise(IFyniteMachine machine)
        {
            RequireMachine(machine).Raise(Resolve());
        }

        /// <summary>
        /// Raises the referenced signal with a payload on a machine.
        /// </summary>
        /// <remarks>
        /// The payload type is validated by the machine against the type the signal was declared with,
        /// so an incompatible payload is rejected with a message naming both types.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="machine"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The reference cannot be resolved, or the machine runs a different graph.</exception>
        public void Raise(IFyniteMachine machine, object payload)
        {
            RequireMachine(machine).Raise(Resolve(), payload);
        }

        /// <summary>Declared payload type of the referenced signal, or null when it carries none.</summary>
        public Type GetPayloadType()
        {
            return TryResolve(out var signal, out _) ? graph.Definition.GetPayloadType(signal) : null;
        }

        /// <inheritdoc />
        public bool Equals(FyniteSignalReference other) =>
            ReferenceEquals(graph, other.graph) && string.Equals(signalGuid, other.signalGuid, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is FyniteSignalReference other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            unchecked(((graph != null ? graph.GetHashCode() : 0) * 397) ^ (signalGuid != null ? signalGuid.GetHashCode() : 0));

        /// <inheritdoc />
        public override string ToString() =>
            IsAssigned ? (SignalName ?? "<missing>") + " @ " + graph.name : "<unassigned signal>";

        private IFyniteMachine RequireMachine(IFyniteMachine machine)
        {
            if (machine == null)
            {
                throw new ArgumentNullException(nameof(machine));
            }

            // A handle only means anything inside the definition that minted it, so a reference pointing
            // at another graph must be refused rather than raised against a same-index stranger.
            if (graph != null && !ReferenceEquals(machine.Definition, graph.Definition))
            {
                throw new InvalidOperationException(
                    "Signal reference '" + this + "' belongs to graph '" + graph.name +
                    "' but was raised on a machine running a different definition.");
            }

            return machine;
        }
    }
}
