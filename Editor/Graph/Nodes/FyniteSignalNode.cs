using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// A declared signal, drawn as a node reactions wire themselves to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Signals are nodes rather than blackboard variables. Graph Toolkit's <c>IVariable</c> exposes a
    /// name, a data type and a default value and nothing else — there is no field on it to hang a
    /// persistent identity, so renaming a variable would be the only thing reactions could key off. That
    /// contradicts the rule that names are never identity, so signals became nodes, which do carry a
    /// serialized GUID.
    /// </para>
    /// <para>
    /// Doing it this way also makes usage visible: every reaction listening to a signal has a wire to
    /// it, deleting a signal breaks those wires where the user can see them rather than silently
    /// repointing them, and "select the reactions that use this signal" is just following the edges.
    /// </para>
    /// </remarks>
    [Serializable]
    [Node("Fynite/Signal")]
    [UseWithGraph(typeof(FyniteGraph))]
    public sealed class FyniteSignalNode : FyniteNamedNode
    {
        /// <summary>Port every listening reaction connects to.</summary>
        public const string SignalPort = "Signal";

        /// <summary>Option holding the script that declares the payload type, if any.</summary>
        public const string PayloadScriptOption = "payloadScript";

        /// <summary>Option choosing between the built-in payload kinds and a scripted one.</summary>
        public const string PayloadKindOption = "payload";

        /// <summary>Payload types offered without needing a script reference.</summary>
        public enum PayloadKind
        {
            /// <summary>The signal carries nothing.</summary>
            None = 0,

            /// <summary>The signal carries an <see cref="int"/>.</summary>
            Int = 1,

            /// <summary>The signal carries a <see cref="float"/>.</summary>
            Float = 2,

            /// <summary>The signal carries a <see cref="bool"/>.</summary>
            Bool = 3,

            /// <summary>The signal carries a <see cref="string"/>.</summary>
            String = 4,

            /// <summary>The signal carries a <see cref="Vector2"/>.</summary>
            Vector2 = 5,

            /// <summary>The signal carries a <see cref="Vector3"/>.</summary>
            Vector3 = 6,

            /// <summary>The signal carries the type declared by the selected script.</summary>
            FromScript = 7
        }

        /// <summary>
        /// Display name. Renaming a signal is safe: reactions point at it through a wire and external
        /// references through its GUID, so neither notices the name changing.
        /// </summary>
        public string SignalName => DisplayName;

        /// <inheritdoc />
        protected override string DefaultDisplayName => "Signal";

        /// <summary>The payload type this signal declares, or null when it carries none.</summary>
        public Type ResolvePayloadType()
        {
            switch (ReadKind())
            {
                case PayloadKind.Int: return typeof(int);
                case PayloadKind.Float: return typeof(float);
                case PayloadKind.Bool: return typeof(bool);
                case PayloadKind.String: return typeof(string);
                case PayloadKind.Vector2: return typeof(Vector2);
                case PayloadKind.Vector3: return typeof(Vector3);
                case PayloadKind.FromScript:
                    var option = GetNodeOptionByName(PayloadScriptOption);
                    MonoScript script = null;
                    option?.TryGetValue(out script);
                    return script != null ? script.GetClass() : null;
                default:
                    return null;
            }
        }

        /// <summary>True when the author asked for a scripted payload but no usable script is selected.</summary>
        public bool HasUnresolvedScriptPayload() =>
            ReadKind() == PayloadKind.FromScript && ResolvePayloadType() == null;

        /// <summary>Reactions wired to this signal.</summary>
        public List<FyniteReactionNode> ResolveListeners()
        {
            var listeners = new List<FyniteReactionNode>();
            var port = GetOutputPortByName(SignalPort);
            if (port == null)
            {
                return listeners;
            }

            var connected = new List<IPort>();
            port.GetConnectedPorts(connected);

            for (int i = 0; i < connected.Count; i++)
            {
                if (connected[i].GetNode() is FyniteReactionNode reaction)
                {
                    listeners.Add(reaction);
                }
            }

            return listeners;
        }

        /// <summary>Refreshes the subtitle so the payload is readable straight off the canvas.</summary>
        public void RefreshLabels()
        {
            var payload = ResolvePayloadType();
            int listeners = ResolveListeners().Count;

            var payloadLabel = payload == null
                ? (ReadKind() == PayloadKind.FromScript ? "payload: <unresolved>" : "no payload")
                : "payload: " + payload.Name;

            Subtitle = payloadLabel + "  ·  " + listeners + (listeners == 1 ? " listener" : " listeners");
        }

        /// <inheritdoc />
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort<FynitePortTypes.Signal>(SignalPort)
                .WithDisplayName("Signal")
                .WithTooltip("Connect to the Signal port of every reaction that responds to this signal.")
                .Build();
        }

        /// <inheritdoc />
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<PayloadKind>(PayloadKindOption)
                .WithDisplayName("Payload")
                .WithTooltip("What this signal carries when it is raised. Prefer no payload for frequent events.")
                .Build();

            if (ReadKind() == PayloadKind.FromScript)
            {
                context.AddOption<MonoScript>(PayloadScriptOption)
                    .WithDisplayName("Payload Script")
                    .WithTooltip("Script declaring the payload type this signal carries.")
                    .Build();
            }
        }

        private PayloadKind ReadKind()
        {
            var option = GetNodeOptionByName(PayloadKindOption);
            if (option == null)
            {
                return PayloadKind.None;
            }

            PayloadKind kind = PayloadKind.None;
            option.TryGetValue(out kind);
            return kind;
        }
    }
}
