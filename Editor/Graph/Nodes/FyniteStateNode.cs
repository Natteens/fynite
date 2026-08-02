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

        /// <summary>Option marking this state as the root of the machine.</summary>
        public const string RootOption = "isRoot";

        /// <summary>Option marking this state as the one its parent enters by default.</summary>
        public const string InitialOption = "isInitial";

        private const string DefaultName = "State";

        [SerializeField]
        [HideInInspector]
        private string m_FyniteGuid;

        [SerializeField]
        [HideInInspector]
        private string m_DisplayName;

        /// <inheritdoc />
        public string FyniteGuid => m_FyniteGuid;

        /// <summary>True when this state is marked as the machine's root.</summary>
        public bool IsRoot => ReadFlag(RootOption);

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
            m_DisplayName = string.IsNullOrWhiteSpace(value) ? DefaultName : value;
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

        /// <summary>The state this one is parented to, or null when it has no parent.</summary>
        public FyniteStateNode ResolveParent()
        {
            var port = GetInputPortByName(ParentPort);
            var connected = port?.FirstConnectedPort;
            return connected?.GetNode() as FyniteStateNode;
        }

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
                .WithTooltip("The state this one lives inside. Only the root has none.")
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
            context.AddOption<bool>(RootOption)
                .WithDisplayName("Is Root")
                .WithTooltip("The single state that owns the whole hierarchy. Exactly one state must be the root.")
                .Build();

            context.AddOption<bool>(InitialOption)
                .WithDisplayName("Is Initial Child")
                .WithTooltip("Entering the parent enters this state by default. Exactly one child of every composite state must be marked.")
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
