using System;
using Fynite.Authoring;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// The graph's settings, as a node so they get a real inspector.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Graph Toolkit builds inspectors from node options, and offers no hook for adding a panel of the
    /// graph's own. Putting the settings on a node is what makes them editable at all — and it has the
    /// side benefit of being visible on the canvas, so a graph missing its context type looks wrong
    /// rather than merely behaving wrong.
    /// </para>
    /// <para>
    /// Exactly one of these belongs in a graph; the graph creates one when a new file is made and
    /// reports it when a graph ends up with none or several.
    /// </para>
    /// </remarks>
    [Serializable]
    [Node("Fynite/Graph Settings")]
    [UseWithGraph(typeof(FyniteGraph))]
    public sealed class FyniteConfigNode : FyniteNodeBase
    {
        /// <summary>Option holding the script that declares the context type.</summary>
        public const string ContextScriptOption = "contextScript";

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            Title = "Fynite Graph";
        }

        /// <summary>The context type this graph compiles against, or null when none is selected.</summary>
        public Type ResolveContextType()
        {
            var option = GetNodeOptionByName(ContextScriptOption);
            if (option == null)
            {
                return null;
            }

            MonoScript script = null;
            option.TryGetValue(out script);
            return script != null ? script.GetClass() : null;
        }

        /// <summary>True when a script is selected but no usable type could be taken from it.</summary>
        public bool HasUnresolvedContextScript()
        {
            var option = GetNodeOptionByName(ContextScriptOption);
            if (option == null)
            {
                return false;
            }

            MonoScript script = null;
            option.TryGetValue(out script);
            return script != null && script.GetClass() == null;
        }

        /// <summary>Refreshes the subtitle so the current context reads off the canvas.</summary>
        public void RefreshLabels()
        {
            var context = ResolveContextType();
            Subtitle = context != null
                ? "context: " + context.Name
                : HasUnresolvedContextScript()
                    ? "context: <script declares no usable type>"
                    : "context: <none selected>";
        }

        /// <inheritdoc />
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<MonoScript>(ContextScriptOption)
                .WithDisplayName("Context Script")
                .WithTooltip(
                    "Script declaring the type every block in this graph works against. A FyniteRunner is " +
                    "given a component of this type, so it must be a MonoBehaviour or an interface a " +
                    "component implements.")
                .Build();
        }
    }
}
