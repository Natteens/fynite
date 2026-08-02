using System;
using System.Collections.Generic;
using Fynite.Authoring;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// A state, drawn as a container its enter, tick, fixed-tick and exit blocks live inside.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A state is a <see cref="ContextNode"/> so that its blocks are real nodes stacked in it rather
    /// than rows in a list widget. The stack order is the execution order, which makes reordering a
    /// drag and keeps the ordering visible instead of hidden in an inspector.
    /// </para>
    /// <para>
    /// The four connectors carry different port data types on purpose. Graph Toolkit will not connect
    /// mismatched types, so a hierarchy wire cannot be dropped on a reaction port and the classes of
    /// invalid graph that would produce simply cannot be drawn.
    /// </para>
    /// </remarks>
    [Serializable]
    [Node("Fynite/State")]
    [UseWithGraph(typeof(FyniteGraph))]
    public sealed class FyniteStateNode : ContextNode, IFyniteIdentifiedNode
    {
        /// <summary>Port a child connects its parent to.</summary>
        public const string ParentPort = "Parent";

        /// <summary>Port a parent hands out to its children.</summary>
        public const string ChildrenPort = "Children";

        /// <summary>Port the reactions declared on this state connect to.</summary>
        public const string ReactionsPort = "Reactions";

        /// <summary>Port reactions targeting this state connect to.</summary>
        public const string IncomingPort = "Targeted By";

        public const string DefaultName = "State";

        /// <summary>Identifier of the node option the Graph Inspector edits the name through.</summary>
        public const string NameOption = "Name";

        [SerializeField]
        [HideInInspector]
        private string m_FyniteGuid;

        [SerializeField]
        [HideInInspector]
        private string m_DisplayName;

        /// <summary>
        /// The last value read out of the <see cref="NameOption"/> option.
        /// </summary>
        /// <remarks>
        /// Graph Toolkit lets a package read a node option and never write one: <c>INodeOption</c>
        /// exposes <c>TryGetValue</c> and no setter. So the option cannot mirror the stored name, and
        /// "which of the two is newer" cannot be answered by comparing them — after a rename from code
        /// they simply differ, with no way to tell which side moved. Remembering what the option said
        /// last time answers it exactly: if it changed, the user typed in the inspector and that wins;
        /// if it did not, the stored name moved and the option is merely stale.
        /// </remarks>
        [SerializeField]
        [HideInInspector]
        private string m_ObservedNameOption;

        [SerializeField]
        [HideInInspector]
        private bool m_IsInitial;

        /// <inheritdoc />
        public string FyniteGuid => m_FyniteGuid;

        /// <summary>True when this state is marked as its parent's initial child.</summary>
        public bool IsInitial => m_IsInitial;

        /// <summary>Sets whether this state is its parent's Initial child.</summary>
        internal void SetInitial(bool value) => m_IsInitial = value;

        /// <summary>
        /// Display name. The canvas rename edits it and <see cref="SyncDisplayName"/> persists it; the
        /// name is only a label, so changing it never disturbs a wire or an external reference.
        /// </summary>
        public string StateName => string.IsNullOrWhiteSpace(m_DisplayName) ? DefaultName : m_DisplayName;

        /// <inheritdoc />
        public void AssignNewFyniteGuid() => m_FyniteGuid = FyniteGuids.New();

        /// <inheritdoc />
        public void AdoptFyniteGuid(string guid) => m_FyniteGuid = guid;

        /// <inheritdoc />
        public void EnsureFyniteGuid()
        {
            if (string.IsNullOrEmpty(m_FyniteGuid))
            {
                m_FyniteGuid = FyniteGuids.New();
            }
        }

        /// <summary>Sets the display name from code.</summary>
        public void SetDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            m_DisplayName = value.Trim();
            Title = m_DisplayName;
        }

        /// <summary>
        /// Reconciles the name the user typed, the stored name and the canvas title.
        /// </summary>
        /// <remarks>
        /// Runs from <see cref="FyniteGraph.OnGraphChanged"/>, which Graph Toolkit raises after any edit
        /// it owns — including a node option changed in the Graph Inspector. That is what makes the
        /// header follow the field as it is typed, with no reimport and no Asset Database call.
        /// </remarks>
        public void SyncDisplayName()
        {
            if (TryReadNameOption(out var typed))
            {
                if (m_ObservedNameOption == null)
                {
                    // First sight of the option on this node. Whatever it holds came from the default,
                    // not from anyone typing, and a graph saved before this option existed carries its
                    // name in the stored field alone — so record and adopt nothing.
                    m_ObservedNameOption = typed ?? string.Empty;
                }
                else if (!string.Equals(typed, m_ObservedNameOption, StringComparison.Ordinal))
                {
                    m_ObservedNameOption = typed;

                    // An emptied field means "no name", not a state literally called nothing.
                    m_DisplayName = string.IsNullOrWhiteSpace(typed) ? DefaultName : typed.Trim();
                    Title = m_DisplayName;
                    return;
                }
            }

            FyniteDisplayNames.Sync(this, ref m_DisplayName, DefaultName);
        }

        /// <summary>Reads the Graph Inspector's Name field, or false when the option is not defined yet.</summary>
        private bool TryReadNameOption(out string value)
        {
            value = null;
            var option = GetNodeOptionByName(NameOption);
            return option != null && option.TryGetValue(out value);
        }

        /// <inheritdoc />
        /// <remarks>
        /// One string option, shown only in the Graph Inspector. It is deliberately not in the node
        /// header: the header already shows the name as the title, and a second editable copy of it
        /// sitting on the node is the kind of duplicate surface this replaced.
        /// </remarks>
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            context.AddOption<string>(NameOption)
                .WithDisplayName("Name")
                .WithTooltip("The label this state is known by. Renaming never changes its identity, " +
                             "its parent, its reactions or anything wired to it.")
                .WithDefaultValue(StateName)
                .ShowInInspectorOnly()
                .Delayed()
                .Build();
        }

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            EnsureFyniteGuid();
            SyncDisplayName();
        }

        /// <summary>
        /// The node this state is parented to — an ordinary state or the graph's root — or null when it
        /// is not connected to anything.
        /// </summary>
        /// <remarks>
        /// The return type is the identity interface rather than a state, because the root is a
        /// different node kind and is a perfectly ordinary thing to be parented to. What the projection
        /// needs from a parent is its identity, and that is what both kinds have.
        /// </remarks>
        public IFyniteIdentifiedNode ResolveParent()
        {
            var port = GetInputPortByName(ParentPort);
            var connected = port?.FirstConnectedPort;
            return connected?.GetNode() as IFyniteIdentifiedNode;
        }

        /// <summary>The ordinary state this one lives inside, or null when its parent is the root.</summary>
        public FyniteStateNode ResolveParentState() => ResolveParent() as FyniteStateNode;

        /// <summary>Reactions declared on this state, in the order they are wired.</summary>
        public List<FyniteReactionNode> ResolveReactions()
        {
            var reactions = new List<FyniteReactionNode>();
            var port = GetOutputPortByName(ReactionsPort);
            if (port == null)
            {
                return reactions;
            }

            var connected = new List<IPort>();
            port.GetConnectedPorts(connected);

            for (int i = 0; i < connected.Count; i++)
            {
                if (connected[i].GetNode() is FyniteReactionNode reaction)
                {
                    reactions.Add(reaction);
                }
            }

            return reactions;
        }

        /// <summary>Reactions that transition into this state, in the order they are wired.</summary>
        public List<FyniteReactionNode> ResolveIncoming()
        {
            var reactions = new List<FyniteReactionNode>();
            var port = GetOutputPortByName(IncomingPort);
            if (port == null)
            {
                return reactions;
            }

            var connected = new List<IPort>();
            port.GetConnectedPorts(connected);

            for (int i = 0; i < connected.Count; i++)
            {
                if (connected[i].GetNode() is FyniteReactionNode reaction)
                {
                    reactions.Add(reaction);
                }
            }

            return reactions;
        }

        /// <summary>The states directly inside this one, in the order they are wired.</summary>
        public List<FyniteStateNode> ResolveChildren()
        {
            var children = new List<FyniteStateNode>();
            var port = GetOutputPortByName(ChildrenPort);
            if (port == null)
            {
                return children;
            }

            var connected = new List<IPort>();
            port.GetConnectedPorts(connected);

            for (int i = 0; i < connected.Count; i++)
            {
                if (connected[i].GetNode() is FyniteStateNode child)
                {
                    children.Add(child);
                }
            }

            return children;
        }

        /// <summary>Blocks of this state grouped by the phase they run in, in stack order.</summary>
        public List<FyniteActionBlockNode> BlocksOfPhase(FynitePhase phase)
        {
            var blocks = new List<FyniteActionBlockNode>();

            foreach (var block in BlockNodes)
            {
                if (block is FyniteActionBlockNode action && action.Phase == phase)
                {
                    blocks.Add(action);
                }
            }

            return blocks;
        }

        /// <inheritdoc />
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            // Inputs accept a single wire in Graph Toolkit, outputs accept many. That is exactly the
            // cardinality the model needs: one parent per state, many children; one source and one
            // target per reaction, many reactions per state.
            context.AddInputPort<FynitePortTypes.Hierarchy>(ParentPort)
                .WithDisplayName("Parent")
                .WithTooltip("Parent is the state that directly contains this state. Root has no parent.")
                .Build();

            context.AddOutputPort<FynitePortTypes.Hierarchy>(ChildrenPort)
                .WithDisplayName("Children")
                .WithTooltip("Connect to the Parent port of every state nested in this one.")
                .Build();

            context.AddOutputPort<FynitePortTypes.ReactionSource>(ReactionsPort)
                .WithDisplayName("Reactions")
                .WithTooltip("Connect to the Source port of every reaction declared on this state.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddOutputPort<FynitePortTypes.TransitionTarget>(IncomingPort)
                .WithDisplayName("Targeted By")
                .WithTooltip("Connect to the Target port of every reaction that transitions into this state.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }

    }
}
