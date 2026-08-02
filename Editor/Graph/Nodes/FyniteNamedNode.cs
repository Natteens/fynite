using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Keeps a node's user-chosen name alive across saves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Graph Toolkit's <c>Node.Title</c> is not persisted for user nodes: it round-trips within a
    /// session but comes back as the node's type name once the file is reloaded. States and signals
    /// cannot rely on that, because their names are what appears in runtime diagnostics and in every
    /// message the compiler produces.
    /// </para>
    /// <para>
    /// So the name lives in a serialized field of the node, and this reconciles that field with the
    /// canvas title in whichever direction has news: a rename on the canvas is adopted into the field,
    /// and the field is pushed back into the title after a reload has blanked it. The name stays a label
    /// either way — identity remains the GUID — so renaming never breaks a wire or an external reference.
    /// </para>
    /// </remarks>
    public static class FyniteDisplayNames
    {
        /// <summary>Reconciles a node's title with the name stored on it.</summary>
        /// <param name="node">The node being reconciled.</param>
        /// <param name="stored">The node's serialized name field.</param>
        /// <param name="fallback">Name to use when there has never been one.</param>
        public static void Sync(INode node, ref string stored, string fallback)
        {
            var title = node.Title;

            // Graph Toolkit hands back the type name when a user node has no title of its own, which is
            // both the initial state and what a reload leaves behind. Either way it is not a name the
            // user chose, so it must not overwrite one they did.
            bool titleIsPlaceholder = string.IsNullOrWhiteSpace(title) ||
                                      title == node.GetType().Name ||
                                      (!string.IsNullOrWhiteSpace(stored) && title == fallback);

            if (!titleIsPlaceholder && !string.Equals(title, stored, StringComparison.Ordinal))
            {
                stored = title;
                return;
            }

            if (string.IsNullOrWhiteSpace(stored))
            {
                stored = fallback;
            }

            if (!string.Equals(title, stored, StringComparison.Ordinal))
            {
                node.Title = stored;
            }
        }
    }

    /// <summary>A plain node whose display name the user owns.</summary>
    [Serializable]
    public abstract class FyniteNamedNode : FyniteNodeBase
    {
        [SerializeField]
        [HideInInspector]
        private string m_DisplayName;

        /// <summary>Name used when the node has never been given one.</summary>
        protected abstract string DefaultDisplayName { get; }

        /// <summary>The persisted display name.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(m_DisplayName) ? DefaultDisplayName : m_DisplayName;

        /// <summary>Sets the display name from code.</summary>
        public void SetDisplayName(string value)
        {
            m_DisplayName = string.IsNullOrWhiteSpace(value) ? DefaultDisplayName : value;
            Title = m_DisplayName;
        }

        /// <summary>Reconciles the canvas title and the persisted name.</summary>
        public void SyncDisplayName() => FyniteDisplayNames.Sync(this, ref m_DisplayName, DefaultDisplayName);

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            SyncDisplayName();
        }
    }
}
