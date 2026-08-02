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
    /// the node data, the schema version, the graph's identity and its context type.
    /// </para>
    /// <para>
    /// The node graph is the single source of truth for authoring. Nothing keeps a parallel copy that
    /// could drift: the compiler reads a document projected from these nodes on demand, and that
    /// projection is the only path from the editor to the runtime asset.
    /// </para>
    /// <para>
    /// The context type is the one thing here that is not a node. It is a property of the whole graph
    /// rather than a step in the flow being drawn, so it lives in a serialized field and is edited in the
    /// <c>.fyn</c> inspector. There is exactly one of it, it cannot be deleted or duplicated, and it
    /// travels in the file like everything else.
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

        [SerializeField]
        [HideInInspector]
        private string m_ContextTypeIdentity;

        /// <summary>Schema version this graph was written with.</summary>
        public int SchemaVersion => m_SchemaVersion;

        /// <summary>Persistent identity of this graph, independent of its file name or path.</summary>
        public string GraphGuid => m_GraphGuid;

        /// <summary>
        /// The type every block in this graph works against, as the authoring model stores it.
        /// </summary>
        /// <remarks>
        /// A reference rather than a <see cref="Type"/>, because the type may not exist any more and
        /// saying so precisely is more useful than silently reading as "none selected".
        /// </remarks>
        public FyniteTypeReference ContextTypeReference => new FyniteTypeReference(m_ContextTypeIdentity);

        /// <summary>Records the context type. Pass null to clear it.</summary>
        public void SetContextType(Type contextType) =>
            m_ContextTypeIdentity = FyniteTypeReference.Format(contextType);

        /// <summary>Records the context type from a stored identity for the inspector.</summary>
        public void SetContextTypeIdentity(string identity) =>
            m_ContextTypeIdentity = string.IsNullOrEmpty(identity) ? null : identity;

        /// <summary>Brings a newly created graph up to a valid starting point.</summary>
        /// <remarks>
        /// A new file gets an identity, the current schema version and a root, and nothing else. The
        /// root is there because every machine has exactly one and it is not something the user should
        /// have to know to add; no states, blocks or settings nodes are invented on their behalf.
        /// </remarks>
        public void InitializeNewGraph()
        {
            m_GraphGuid = FyniteGuids.New();
            m_SchemaVersion = FyniteGraphDocument.CurrentSchemaVersion;
            m_ContextTypeIdentity = null;

            var root = new FyniteRootStateNode();
            AddNode(root);
            root.Position = new Vector2(0f, 0f);
        }

        /// <summary>The graph's root node, or null when it has none.</summary>
        public FyniteRootStateNode FindRoot()
        {
            foreach (var node in GetNodes())
            {
                if (node is FyniteRootStateNode root)
                {
                    return root;
                }
            }

            return null;
        }

        /// <summary>Every root node in the graph. More than one is an error the compiler reports.</summary>
        public List<FyniteRootStateNode> FindRoots()
        {
            var roots = new List<FyniteRootStateNode>();

            foreach (var node in GetNodes())
            {
                if (node is FyniteRootStateNode root)
                {
                    roots.Add(root);
                }
            }

            return roots;
        }

        /// <summary>The context type the graph compiles against, or null when it is unset or gone.</summary>
        public Type ResolveContextType()
        {
            var reference = ContextTypeReference;
            return reference.IsSet ? FyniteTypeResolver.Resolve(reference) : null;
        }

        /// <summary>
        /// Reports a change Graph Toolkit's own change model does not see.
        /// </summary>
        /// <remarks>
        /// Graph Toolkit notices structural edits — nodes added, wires connected — because those go
        /// through its API. A serialized field owned by a Fynite node or by this graph is deliberately
        /// outside that model, so an edit that only writes one can be left out of the save. Touching the
        /// public model without leaving anything behind puts the change back on its radar. No internal
        /// API and no reflection is involved: this is <see cref="AddNode"/> and <see cref="RemoveNode"/>,
        /// and the graph it returns to the caller is byte-for-byte the one it received.
        /// </remarks>
        public void TouchForFieldChange()
        {
            var marker = new FyniteRootStateNode();
            AddNode(marker);
            RemoveNode(marker);
        }

        /// <inheritdoc />
        public override void OnGraphChanged(GraphLogger graphLogger)
        {
            base.OnGraphChanged(graphLogger);

            if (m_SchemaVersion != FyniteGraphDocument.CurrentSchemaVersion)
            {
                graphLogger.LogError(
                    "Unsupported Fynite schema version " + m_SchemaVersion + ". This build accepts only " +
                    FyniteGraphDocument.CurrentSchemaVersion + ". Convert the file with the Fynite version that owns it.");
                return;
            }

            EnsureUniqueIdentities();
            RefreshLabels();

            FyniteGraphValidation.Report(this, graphLogger);
        }

        /// <summary>Records the schema version this graph is now written in.</summary>
        internal void SetSchemaVersion(int version) => m_SchemaVersion = version;

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
                    case FyniteRootStateNode root:
                        root.RefreshLabels();
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
            var marks = new List<string>(2);

            if (state.IsInitial)
            {
                marks.Add("initial");
            }

            marks.Add(state.ResolveChildren().Count > 0 ? "composite" : "leaf");

            state.Subtitle = string.Join(" · ", marks);
        }
    }
}
