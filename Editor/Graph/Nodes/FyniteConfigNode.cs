using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// The settings node graphs used to carry. Kept only so that graphs written with one can still be
    /// read and migrated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The context type is a property of the graph, not a step in the flow drawn on the canvas, so a node
    /// was the wrong place for it: it took up space in the diagram, it could be deleted or duplicated,
    /// and it made "which node holds the truth" a question at all. It now lives on the graph itself and
    /// is edited in the <c>.fyn</c> inspector.
    /// </para>
    /// <para>
    /// The class survives because deleting it would make an existing <c>.fyn</c> unreadable — Graph
    /// Toolkit stores the node's type name in the file, and a node whose class is gone takes its data
    /// with it, including the context type this is here to recover. It deliberately has no
    /// <c>[Node]</c> attribute, so it is absent from the create menu and no new one can appear;
    /// <see cref="FyniteGraphMigration"/> reads the context out of any that are found and then removes
    /// them.
    /// </para>
    /// </remarks>
    [Serializable]
    [UseWithGraph(typeof(FyniteGraph))]
    public sealed class FyniteConfigNode : FyniteNodeBase
    {
        /// <summary>Option the legacy node held the context script in.</summary>
        public const string ContextScriptOption = "contextScript";

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            Title = "Fynite Graph (legacy settings)";
            Subtitle = "migrated to the graph's Context Type";
        }

        /// <summary>The context type this legacy node selects, or null when it selects none.</summary>
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

        /// <inheritdoc />
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<MonoScript>(ContextScriptOption)
                .WithDisplayName("Context Script")
                .WithTooltip("Where this graph's context type used to be chosen. Set it in the .fyn inspector now.")
                .Build();
        }
    }
}
