using System;
using System.Collections.Generic;
using Fynite.Authoring;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Fynite.Editor
{
    /// <summary>
    /// The control a graph's context type is chosen with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type is picked as a type, not as a script asset. A script reference was what the graph used to
    /// store, and it leaked an implementation detail into the interface: the field said "Mono Script",
    /// accepted any script at all, and only complained afterwards if the file happened not to be named
    /// after the class it contained. This offers exactly the types that can be a context and nothing
    /// else, so an unusable selection cannot be made in the first place.
    /// </para>
    /// <para>
    /// It is a searchable dropdown rather than a popup because a real project has hundreds of components
    /// and a flat list of them is not a choice, it is a scroll. Types are grouped by namespace, which is
    /// also what tells two same-named classes apart.
    /// </para>
    /// </remarks>
    public static class FyniteContextTypePicker
    {
        /// <summary>Label every surface uses for this, so it reads the same everywhere.</summary>
        public const string Label = "Context Type";

        /// <summary>
        /// Draws the picker.
        /// </summary>
        /// <param name="current">The type currently selected, or null.</param>
        /// <param name="missing">
        /// What was selected when it can no longer be resolved, so the control can say the type is gone
        /// rather than that nothing was chosen.
        /// </param>
        /// <param name="onSelected">Called with the chosen type, or null when the selection was cleared.</param>
        public static void Draw(Type current, FyniteTypeReference missing, Action<Type> onSelected)
        {
            var rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(
                rect,
                new GUIContent(
                    Label,
                    "The type every block in this graph works against. A FyniteRunner resolves a component " +
                    "of this type on its own GameObject."));

            var content = Describe(current, missing);

            if (!EditorGUI.DropdownButton(rect, content, FocusType.Keyboard))
            {
                return;
            }

            var dropdown = new ContextTypeDropdown(new AdvancedDropdownState(), onSelected);
            dropdown.Show(rect);
        }

        private static GUIContent Describe(Type current, FyniteTypeReference missing)
        {
            if (current != null)
            {
                return new GUIContent(
                    current.Name + (string.IsNullOrEmpty(current.Namespace) ? string.Empty : "  (" + current.Namespace + ")"),
                    current.FullName);
            }

            if (missing.IsSet)
            {
                return new GUIContent(
                    "Missing: " + missing.DisplayName,
                    "'" + missing + "' was selected but no longer exists. It was renamed, moved or deleted.");
            }

            return new GUIContent("None", "No context type selected yet.");
        }

        private sealed class ContextTypeItem : AdvancedDropdownItem
        {
            public ContextTypeItem(string name, Type type)
                : base(name)
            {
                Type = type;
            }

            public Type Type { get; }
        }

        private sealed class ContextTypeDropdown : AdvancedDropdown
        {
            private readonly Action<Type> _onSelected;

            public ContextTypeDropdown(AdvancedDropdownState state, Action<Type> onSelected)
                : base(state)
            {
                _onSelected = onSelected;
                minimumSize = new Vector2(300f, 320f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(Label);
                root.AddChild(new ContextTypeItem("None", null));
                root.AddSeparator();

                var groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);
                var catalog = FyniteContextCatalog.All;

                for (int i = 0; i < catalog.Count; i++)
                {
                    var type = catalog[i];
                    var group = string.IsNullOrEmpty(type.Namespace) ? "<global namespace>" : type.Namespace;

                    if (!groups.TryGetValue(group, out var parent))
                    {
                        parent = new AdvancedDropdownItem(group);
                        groups[group] = parent;
                        root.AddChild(parent);
                    }

                    // The interface marker matters: it is the difference between "a component of exactly
                    // this class" and "any component implementing this", which changes what the runner
                    // will and will not accept.
                    parent.AddChild(new ContextTypeItem(type.IsInterface ? type.Name + "  (interface)" : type.Name, type));
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is ContextTypeItem selected)
                {
                    _onSelected(selected.Type);
                }
            }
        }
    }
}
