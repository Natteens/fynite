using System;
using System.Collections.Generic;
using Fynite.Authoring;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// The Graph Toolkit graph behind a <c>.fyn</c> file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Declaring the extension here is what makes <c>.fyn</c> a real graph asset: Graph Toolkit owns
    /// the file's container format, opens the window on double-click, and gives the canvas its pan,
    /// zoom, selection, undo and clipboard behaviour for free. Fynite owns what is stored inside —
    /// the node data, the schema version and the graph's identity.
    /// </para>
    /// <para>
    /// The node graph is the single source of truth for authoring. Nothing keeps a parallel copy that
    /// could drift: the compiler reads a document projected from these nodes on demand, and that
    /// projection is the only path from the editor to the runtime asset.
    /// </para>
    /// </remarks>
    [Serializable]
    [Graph(Extension)]
    public sealed class FyniteGraph : Graph
    {
        /// <summary>Extension of a Fynite graph file, without the dot.</summary>
        public const string Extension = "fyn";

        /// <summary>Extension with the leading dot, for path work.</summary>
        public const string DottedExtension = "." + Extension;

        [SerializeField]
        [HideInInspector]
        private int m_SchemaVersion;

        [SerializeField]
        [HideInInspector]
        private string m_GraphGuid;

        /// <summary>Schema version this graph was written with.</summary>
        public int SchemaVersion => m_SchemaVersion;

        /// <summary>Persistent identity of this graph, independent of its file name or path.</summary>
        public string GraphGuid => m_GraphGuid;

        /// <summary>Brings a newly created graph up to a valid, compilable starting point.</summary>
        /// <remarks>
        /// A new file gets an identity, the current schema version, a settings node and a root state, so
        /// the very first thing the user sees is a graph that only needs a context type rather than an
        /// empty canvas with no clue what to do.
        /// </remarks>
        public void InitializeNewGraph()
        {
            m_GraphGuid = FyniteGuids.New();
            m_SchemaVersion = FyniteGraphDocument.CurrentSchemaVersion;

            var settings = new FyniteConfigNode();
            AddNode(settings);
            settings.Position = new Vector2(-320f, -160f);

            var root = new FyniteStateNode();
            AddNode(root);
            root.Position = new Vector2(0f, 0f);
            root.SetDisplayName("Root");
        }

        /// <summary>The settings node, or null when the graph has none.</summary>
        public FyniteConfigNode FindSettings()
        {
            foreach (var node in GetNodes())
            {
                if (node is FyniteConfigNode settings)
                {
                    return settings;
                }
            }

            return null;
        }

        /// <summary>The context type the graph compiles against, or null.</summary>
        public Type ResolveContextType() => FindSettings()?.ResolveContextType();

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();

            // A graph opened from a file written before identity or versioning existed still needs both,
            // and there is no earlier moment than this to give them.
            if (string.IsNullOrEmpty(m_GraphGuid))
            {
                m_GraphGuid = FyniteGuids.New();
            }

            if (m_SchemaVersion <= 0)
            {
                m_SchemaVersion = FyniteGraphDocument.MinimumSupportedSchemaVersion;
            }
        }

        /// <inheritdoc />
        public override void OnGraphChanged(GraphLogger graphLogger)
        {
            base.OnGraphChanged(graphLogger);

            EnsureUniqueIdentities();
            RefreshLabels();

            if (m_SchemaVersion < FyniteGraphDocument.CurrentSchemaVersion)
            {
                // The projection already reads the current shape, so recording the new version here is
                // what makes the migration stick the next time the file is written.
                m_SchemaVersion = FyniteGraphDocument.CurrentSchemaVersion;
            }

            if (m_SchemaVersion > FyniteGraphDocument.CurrentSchemaVersion)
            {
                graphLogger.LogError(
                    "This graph was written with schema version " + m_SchemaVersion + " but this build of " +
                    "Fynite only understands up to version " + FyniteGraphDocument.CurrentSchemaVersion +
                    ". Editing it here would reinterpret it; upgrade Fynite instead.");
                return;
            }

            FyniteGraphValidation.Report(this, graphLogger);
        }

        /// <summary>
        /// Re-issues identities that appear more than once.
        /// </summary>
        /// <remarks>
        /// Copy and paste clones a node's serialized fields, identity included, so pasted nodes arrive
        /// carrying the identity of the node they came from. Graph Toolkit already remaps the wires
        /// between pasted nodes, and every cross-reference in this model is a wire — so nothing needs
        /// remapping here. Giving the duplicates fresh identities is enough, and the first occurrence
        /// keeps its own so external references to it stay valid.
        /// </remarks>
        private void EnsureUniqueIdentities()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var node in GetNodes())
            {
                Deduplicate(node as IFyniteIdentifiedNode, seen);

                if (!(node is ContextNode container))
                {
                    continue;
                }

                foreach (var block in container.BlockNodes)
                {
                    Deduplicate(block as IFyniteIdentifiedNode, seen);
                }
            }
        }

        private static void Deduplicate(IFyniteIdentifiedNode node, HashSet<string> seen)
        {
            if (node == null)
            {
                return;
            }

            node.EnsureFyniteGuid();

            while (!seen.Add(node.FyniteGuid))
            {
                node.AssignNewFyniteGuid();
            }
        }

        private void RefreshLabels()
        {
            foreach (var node in GetNodes())
            {
                switch (node)
                {
                    case FyniteConfigNode settings:
                        settings.RefreshLabels();
                        break;
                    case FyniteSignalNode signal:
                        signal.SyncDisplayName();
                        signal.RefreshLabels();
                        break;
                    case FyniteReactionNode reaction:
                        reaction.RefreshLabels();
                        break;
                    case FyniteStateNode state:
                        state.SyncDisplayName();
                        RefreshStateLabel(state);
                        break;
                }
            }
        }

        private static void RefreshStateLabel(FyniteStateNode state)
        {
            var marks = new List<string>(3);

            if (state.IsRoot)
            {
                marks.Add("ROOT");
            }

            if (state.IsInitial)
            {
                marks.Add("initial");
            }

            marks.Add(HasChildren(state) ? "composite" : "leaf");

            state.Subtitle = string.Join(" · ", marks);
        }

        private static bool HasChildren(FyniteStateNode state)
        {
            var port = state.GetOutputPortByName(FyniteStateNode.ChildrenPort);
            if (port == null)
            {
                return false;
            }

            var connected = new List<IPort>();
            port.GetConnectedPorts(connected);
            return connected.Count > 0;
        }
    }
}
