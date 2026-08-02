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

        /// <summary>Option marking this state as the one its parent enters by default.</summary>
        public const string InitialOption = "isInitial";

        public const string DefaultName = "State";

        [SerializeField]
        [HideInInspector]
        private string m_FyniteGuid;

        [SerializeField]
        [HideInInspector]
        private string m_DisplayName;

        /// <inheritdoc />
        public string FyniteGuid => m_FyniteGuid;

        /// <summary>True when this state is marked as its parent's initial child.</summary>
        public bool IsInitial => ReadFlag(InitialOption);

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

        /// <summary>Reconciles the canvas title and the persisted name.</summary>
        public void SyncDisplayName() => FyniteDisplayNames.Sync(this, ref m_DisplayName, DefaultName);

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

        /// <summary>
        /// True when this state carries anything a structural root is not allowed to carry.
        /// </summary>
        /// <remarks>
        /// Only migration asks this. A legacy root that answers false can simply become a root node;
        /// one that answers true has behaviour that must survive, so it stays an ordinary state and a
        /// root is created above it instead.
        /// </remarks>
        public bool HasExecutableContent() =>
            BlockCount > 0 || ResolveReactions().Count > 0 || ResolveIncoming().Count > 0;

        /// <summary>
        /// Reads the <c>Is Root</c> flag of a graph written before the root became its own node kind.
        /// </summary>
        /// <remarks>
        /// The option is no longer declared, so Graph Toolkit drops it the next time it defines the
        /// node. Whether it is still readable in between depends on when that reconciliation happens,
        /// which is why migration treats this as a hint and can identify a legacy root without it.
        /// </remarks>
        /// <returns>False when the flag is not readable, which is the normal case for a current graph.</returns>
        public bool TryReadLegacyRootFlag(out bool isRoot)
        {
            isRoot = false;

            var option = GetNodeOptionByName(LegacyRootOption);
            if (option == null)
            {
                return false;
            }

            return option.TryGetValue(out isRoot);
        }

        /// <summary>Name the removed root flag was declared under, kept only so migration can look for it.</summary>
        internal const string LegacyRootOption = "isRoot";

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

        /// <inheritdoc />
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            // There is deliberately no "Is Root" here. The root is its own node kind, so a state cannot
            // become one by ticking a box and a graph cannot acquire a second root by ticking two.
            context.AddOption<bool>(InitialOption)
                .WithDisplayName("Initial")
                .WithTooltip("The Initial state is the first direct child entered when its Parent becomes active. Use Set as Initial to change it.")
                .Build();
        }

        private bool ReadFlag(string option)
        {
            var value = GetNodeOptionByName(option);
            if (value == null)
            {
                return false;
            }

            bool flag = false;
            value.TryGetValue(out flag);
            return flag;
        }
    }
}
