using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fynite.Authoring
{
    /// <summary>
    /// The authoring model: everything a Fynite graph means, independent of how it is drawn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is deliberately not a Graph Toolkit type. The visual editor projects its nodes into a
    /// document, and the compiler consumes documents — so validation and compilation can be exercised in
    /// tests without ever opening a window, and the visual layer can be replaced without touching the
    /// pipeline behind it.
    /// </para>
    /// <para>
    /// Identity is by GUID throughout. Names are labels the user can change at any time, and positions
    /// are layout only: neither is ever allowed to decide execution order.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class FyniteGraphDocument
    {
        /// <summary>Schema version this build of Fynite writes and fully understands.</summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>Oldest schema version that can still be migrated forward.</summary>
        public const int MinimumSupportedSchemaVersion = 1;

        /// <summary>Version of the schema the document was written with.</summary>
        public int schemaVersion = CurrentSchemaVersion;

        /// <summary>Persistent identity of the graph itself.</summary>
        public string graphGuid;

        /// <summary>Context type every block in this graph must be compatible with.</summary>
        public FyniteTypeReference contextType;

        /// <summary>Declared states, in authoring order.</summary>
        public List<FyniteStateDocument> states = new List<FyniteStateDocument>();

        /// <summary>Declared signals, in authoring order.</summary>
        public List<FyniteSignalDocument> signals = new List<FyniteSignalDocument>();

        /// <summary>Declared reactions, in authoring order.</summary>
        public List<FyniteReactionDocument> reactions = new List<FyniteReactionDocument>();

        /// <summary>Finds a state by identity, or null.</summary>
        public FyniteStateDocument FindState(string guid) => Find(states, guid);

        /// <summary>Finds a signal by identity, or null.</summary>
        public FyniteSignalDocument FindSignal(string guid) => Find(signals, guid);

        /// <summary>Finds a reaction by identity, or null.</summary>
        public FyniteReactionDocument FindReaction(string guid) => Find(reactions, guid);

        private static T Find<T>(List<T> items, string guid) where T : class, IFyniteAuthoringElement
        {
            if (items == null || string.IsNullOrEmpty(guid))
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && string.Equals(items[i].Guid, guid, StringComparison.Ordinal))
                {
                    return items[i];
                }
            }

            return null;
        }
    }

    /// <summary>Anything in the authoring model that carries a persistent identity.</summary>
    public interface IFyniteAuthoringElement
    {
        /// <summary>Persistent identity; never a name, never an index.</summary>
        string Guid { get; }

        /// <summary>Label shown to the user, when the element has one.</summary>
        string DisplayName { get; }

        /// <summary>What kind of element this is, for diagnostic messages.</summary>
        string ElementKind { get; }
    }

    /// <summary>One authored state.</summary>
    [Serializable]
    public sealed class FyniteStateDocument : IFyniteAuthoringElement
    {
        /// <summary>Persistent identity.</summary>
        public string guid;

        /// <summary>Label shown in the editor and used as the runtime diagnostic name.</summary>
        public string name;

        /// <summary>Layout only. Never affects ordering or semantics.</summary>
        public Vector2 position;

        /// <summary>Identity of the parent state, or null/empty when the state has no parent.</summary>
        public string parentGuid;

        /// <summary>True for the single state that owns the whole hierarchy.</summary>
        public bool isRoot;

        /// <summary>True when this state is the one its parent enters by default.</summary>
        public bool isInitial;

        /// <summary>Blocks that run when the state is entered, in execution order.</summary>
        public List<FyniteBlockDocument> enter = new List<FyniteBlockDocument>();

        /// <summary>Blocks that run every tick while the state is active, in execution order.</summary>
        public List<FyniteBlockDocument> tick = new List<FyniteBlockDocument>();

        /// <summary>Blocks that run every fixed tick while the state is active, in execution order.</summary>
        public List<FyniteBlockDocument> fixedTick = new List<FyniteBlockDocument>();

        /// <summary>Blocks that run when the state is exited, in execution order.</summary>
        public List<FyniteBlockDocument> exit = new List<FyniteBlockDocument>();

        /// <inheritdoc />
        public string Guid => guid;

        /// <inheritdoc />
        public string DisplayName => name;

        /// <inheritdoc />
        public string ElementKind => "state";

        /// <summary>The four block lists, in phase order.</summary>
        public IEnumerable<KeyValuePair<FynitePhase, List<FyniteBlockDocument>>> Phases
        {
            get
            {
                yield return new KeyValuePair<FynitePhase, List<FyniteBlockDocument>>(FynitePhase.Enter, enter);
                yield return new KeyValuePair<FynitePhase, List<FyniteBlockDocument>>(FynitePhase.Tick, tick);
                yield return new KeyValuePair<FynitePhase, List<FyniteBlockDocument>>(FynitePhase.FixedTick, fixedTick);
                yield return new KeyValuePair<FynitePhase, List<FyniteBlockDocument>>(FynitePhase.Exit, exit);
            }
        }
    }

    /// <summary>One authored signal.</summary>
    [Serializable]
    public sealed class FyniteSignalDocument : IFyniteAuthoringElement
    {
        /// <summary>Persistent identity. Reactions and external references point at this, never at the name.</summary>
        public string guid;

        /// <summary>Label shown in the editor and used as the runtime diagnostic name.</summary>
        public string name;

        /// <summary>Layout only.</summary>
        public Vector2 position;

        /// <summary>Declared payload type, or an unset reference for a payload-less signal.</summary>
        public FyniteTypeReference payloadType;

        /// <inheritdoc />
        public string Guid => guid;

        /// <inheritdoc />
        public string DisplayName => name;

        /// <inheritdoc />
        public string ElementKind => "signal";
    }

    /// <summary>One authored reaction. A reaction without a target only runs its effects.</summary>
    [Serializable]
    public sealed class FyniteReactionDocument : IFyniteAuthoringElement
    {
        /// <summary>Persistent identity.</summary>
        public string guid;

        /// <summary>Identity of the state the reaction is declared on.</summary>
        public string sourceGuid;

        /// <summary>Identity of the signal that triggers it.</summary>
        public string signalGuid;

        /// <summary>Identity of the state to transition to, or null/empty for an effect-only reaction.</summary>
        public string targetGuid;

        /// <summary>Layout only.</summary>
        public Vector2 position;

        /// <summary>Resolution priority; higher wins.</summary>
        public int priority;

        /// <summary>
        /// Explicit registration order, used as the last tie-break between reactions of equal priority
        /// and depth. Authored rather than derived, so moving a node never changes resolution.
        /// </summary>
        public int order;

        /// <summary>Guards, evaluated in order, short-circuiting on the first false.</summary>
        public List<FyniteBlockDocument> guards = new List<FyniteBlockDocument>();

        /// <summary>Effects, run in order between the exits and the entries of a transition.</summary>
        public List<FyniteBlockDocument> effects = new List<FyniteBlockDocument>();

        /// <summary>True when this reaction changes the active path.</summary>
        public bool HasTarget => !string.IsNullOrEmpty(targetGuid);

        /// <inheritdoc />
        public string Guid => guid;

        /// <inheritdoc />
        public string DisplayName => null;

        /// <inheritdoc />
        public string ElementKind => "reaction";
    }

    /// <summary>One authored use of a block type, with its own configuration and identity.</summary>
    /// <remarks>
    /// The same block type can appear many times with different configuration, which is why an
    /// occurrence has its own GUID and its own serialized data rather than being identified by its type.
    /// </remarks>
    [Serializable]
    public sealed class FyniteBlockDocument : IFyniteAuthoringElement
    {
        /// <summary>Persistent identity of this occurrence.</summary>
        public string guid;

        /// <summary>The block type this occurrence instantiates.</summary>
        public FyniteTypeReference blockType;

        /// <summary>
        /// The occurrence's configuration, as JSON of the block object itself.
        /// </summary>
        /// <remarks>
        /// Storing the configuration separately from the type keeps stale data detectable: when the type
        /// changes shape, fields that no longer exist are simply dropped and missing ones take their
        /// defaults, and the compiler reports what it could not carry over.
        /// </remarks>
        public string configJson;

        /// <inheritdoc />
        public string Guid => guid;

        /// <inheritdoc />
        public string DisplayName => blockType.DisplayName;

        /// <inheritdoc />
        public string ElementKind => "block";
    }
}
