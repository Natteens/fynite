using System;
using System.Collections.Generic;
using Fynite.Authoring;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// A reaction, drawn as its own node between the state that declares it and the state it enters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reaction is a node rather than a decorated wire because it owns things a wire cannot hold: an
    /// ordered list of guards, an ordered list of effects, a priority and an explicit registration
    /// order. Being a <see cref="ContextNode"/> lets the guards and effects be stacked blocks, so their
    /// order is visible and draggable, exactly like a state's actions.
    /// </para>
    /// <para>
    /// Leaving the target port unconnected is what makes a reaction effect-only: it runs its effects and
    /// the active path is untouched. That reads correctly on the canvas — a reaction with nothing coming
    /// out of it goes nowhere.
    /// </para>
    /// </remarks>
    [Serializable]
    [Node("Fynite/Reaction")]
    [UseWithGraph(typeof(FyniteGraph))]
    public sealed class FyniteReactionNode : ContextNode, IFyniteIdentifiedNode
    {
        /// <summary>Port connected to the declaring state's Reactions port.</summary>
        public const string SourcePort = "Source";

        /// <summary>Port connected to the signal that triggers this reaction.</summary>
        public const string SignalPort = "Signal";

        /// <summary>Port connected to the target state's Targeted By port; unconnected means no transition.</summary>
        public const string TargetPort = "Target";

        /// <summary>Option holding the resolution priority.</summary>
        public const string PriorityOption = "priority";

        /// <summary>Option holding the explicit registration order.</summary>
        public const string OrderOption = "order";

        [SerializeField]
        [HideInInspector]
        private string m_FyniteGuid;

        /// <inheritdoc />
        public string FyniteGuid => m_FyniteGuid;

        /// <summary>Resolution priority; higher wins.</summary>
        public int Priority => ReadInt(PriorityOption);

        /// <summary>Explicit registration order, the last tie-break between equal priorities.</summary>
        public int Order => ReadInt(OrderOption);

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

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            EnsureFyniteGuid();
        }

        /// <summary>The state this reaction is declared on, or null when it is not wired to one.</summary>
        public FyniteStateNode ResolveSource() =>
            GetInputPortByName(SourcePort)?.FirstConnectedPort?.GetNode() as FyniteStateNode;

        /// <summary>The state this reaction transitions to, or null for an effect-only reaction.</summary>
        public FyniteStateNode ResolveTarget() =>
            GetInputPortByName(TargetPort)?.FirstConnectedPort?.GetNode() as FyniteStateNode;

        /// <summary>The signal that triggers this reaction, or null when it is not wired to one.</summary>
        public FyniteSignalNode ResolveSignal() =>
            GetInputPortByName(SignalPort)?.FirstConnectedPort?.GetNode() as FyniteSignalNode;

        /// <summary>Guards of this reaction, in stack order.</summary>
        public List<FyniteGuardBlockNode> ResolveGuards()
        {
            var guards = new List<FyniteGuardBlockNode>();
            foreach (var block in BlockNodes)
            {
                if (block is FyniteGuardBlockNode guard)
                {
                    guards.Add(guard);
                }
            }

            return guards;
        }

        /// <summary>Effects of this reaction, in stack order.</summary>
        public List<FyniteEffectBlockNode> ResolveEffects()
        {
            var effects = new List<FyniteEffectBlockNode>();
            foreach (var block in BlockNodes)
            {
                if (block is FyniteEffectBlockNode effect)
                {
                    effects.Add(effect);
                }
            }

            return effects;
        }

        /// <summary>Refreshes the node's labels so the canvas reads without opening the inspector.</summary>
        public void RefreshLabels()
        {
            var signal = ResolveSignal();
            var target = ResolveTarget();

            Title = signal != null ? "on " + signal.SignalName : "on <no signal>";
            Subtitle = target != null ? "→ " + target.StateName : "no transition";
        }

        /// <inheritdoc />
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<FynitePortTypes.ReactionSource>(SourcePort)
                .WithDisplayName("Source")
                .WithTooltip("The state that declares this reaction.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddInputPort<FynitePortTypes.Signal>(SignalPort)
                .WithDisplayName("Signal")
                .WithTooltip("The signal this reaction responds to.")
                .Build();

            context.AddInputPort<FynitePortTypes.TransitionTarget>(TargetPort)
                .WithDisplayName("Target")
                .WithTooltip("The state to transition to. Leave unconnected to run only the effects.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }

        /// <inheritdoc />
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<int>(PriorityOption)
                .WithDisplayName("Priority")
                .WithTooltip("Higher wins when several reactions on the active path answer the same signal.")
                .Build();

            context.AddOption<int>(OrderOption)
                .WithDisplayName("Order")
                .WithTooltip(
                    "Explicit registration order, used as the last tie-break between reactions of equal " +
                    "priority and depth. Give every reaction a distinct value to make resolution explicit.")
                .Build();
        }

        private int ReadInt(string option)
        {
            var value = GetNodeOptionByName(option);
            if (value == null)
            {
                return 0;
            }

            int number = 0;
            value.TryGetValue(out number);
            return number;
        }
    }
}
