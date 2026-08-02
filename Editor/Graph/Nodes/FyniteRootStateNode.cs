using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// The top of the hierarchy, and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The root used to be an ordinary state with a tick box, which made two things possible that should
    /// never have been: a second root, by ticking the box twice, and a root that ran behaviour. This is a
    /// different node kind instead, so both are structurally impossible rather than merely reported.
    /// </para>
    /// <para>
    /// It is a plain <see cref="Node"/> rather than a <see cref="ContextNode"/> precisely because a
    /// <c>ContextNode</c> is what allows block nodes to be dropped into it. Having no block stack is not
    /// a rule enforced somewhere — there is simply nowhere to put one. For the same reason it declares a
    /// single port: a root has no parent, runs nothing, and cannot be the source or the target of a
    /// reaction, so the ports that would express any of that do not exist.
    /// </para>
    /// <para>
    /// The root is a container, not a state that is always active. It carries the identity of the
    /// machine's top level so that reparenting, renaming and reimporting never disturb it.
    /// </para>
    /// </remarks>
    [Serializable]
    [Node("Fynite/Root")]
    [UseWithGraph(typeof(FyniteGraph))]
    public sealed class FyniteRootStateNode : FyniteNodeBase
    {
        /// <summary>Port the root hands out to the states directly beneath it.</summary>
        public const string ChildrenPort = "Children";

        /// <summary>
        /// Name the compiled machine knows the root by.
        /// </summary>
        /// <remarks>
        /// Fixed rather than user-chosen: the root is structural, so its name is only ever a label in a
        /// diagnostic, and one label that always means the same thing is worth more than a name someone
        /// has to invent for a node that does nothing.
        /// </remarks>
        public const string RootName = "Root";

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            Title = RootName;
        }

        /// <summary>Reasserts the fixed title, in case the canvas renamed it.</summary>
        public void RefreshLabels()
        {
            if (!string.Equals(Title, RootName, StringComparison.Ordinal))
            {
                Title = RootName;
            }

            Subtitle = HasChildren() ? "machine root" : "machine root · no children yet";
        }

        /// <summary>The states directly beneath the root, in the order they are wired.</summary>
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
                if (connected[i].GetNode() is FyniteStateNode state)
                {
                    children.Add(state);
                }
            }

            return children;
        }

        /// <summary>True when at least one state is parented to the root.</summary>
        public bool HasChildren()
        {
            var port = GetOutputPortByName(ChildrenPort);
            if (port == null)
            {
                return false;
            }

            var connected = new List<IPort>();
            port.GetConnectedPorts(connected);
            return connected.Count > 0;
        }

        /// <inheritdoc />
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort<FynitePortTypes.Hierarchy>(ChildrenPort)
                .WithDisplayName("Children")
                .WithTooltip("Connect to the Parent port of every state at the top level of this machine.")
                .Build();
        }
    }
}
