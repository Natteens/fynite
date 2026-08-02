using System;
using System.Collections.Generic;

namespace Fynite.Authoring
{
    /// <summary>
    /// Works out the state tree and reports everything wrong with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The analysis produces the parent-first ordering the runtime data needs. That ordering is derived
    /// by walking the tree from the root and visiting each state's children in authoring order — never
    /// in position order, so moving a node on the canvas cannot change what the graph compiles to.
    /// </para>
    /// <para>
    /// Problems are reported per element rather than thrown, so one broken state does not hide the other
    /// nine. When the tree is too broken to order at all, the analysis still returns, and the compiler
    /// stops because errors were recorded.
    /// </para>
    /// </remarks>
    public sealed class FyniteHierarchyAnalysis
    {
        private readonly List<int> _parentIndices = new List<int>();
        private readonly List<int> _initialIndices = new List<int>();

        private FyniteHierarchyAnalysis()
        {
        }

        /// <summary>States in parent-first order; empty when the tree could not be ordered.</summary>
        public List<FyniteStateDocument> Ordered { get; } = new List<FyniteStateDocument>();

        /// <summary>Index into <see cref="Ordered"/> by state identity.</summary>
        public Dictionary<string, int> IndexByGuid { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Index of the root in <see cref="Ordered"/>, or -1.</summary>
        public int RootIndex { get; private set; } = -1;

        /// <summary>Parent index of an ordered state, or -1 for the root.</summary>
        public int ParentIndex(int orderedIndex) => _parentIndices[orderedIndex];

        /// <summary>Initial child index of an ordered state, or -1 for a leaf.</summary>
        public int InitialChildIndex(int orderedIndex) => _initialIndices[orderedIndex];

        /// <summary>Runs the analysis, appending diagnostics for everything it finds wrong.</summary>
        public static FyniteHierarchyAnalysis Analyse(FyniteGraphDocument document, List<FyniteDiagnostic> diagnostics)
        {
            var analysis = new FyniteHierarchyAnalysis();

            var states = new List<FyniteStateDocument>();
            var byGuid = new Dictionary<string, FyniteStateDocument>(StringComparer.Ordinal);

            for (int i = 0; i < document.states.Count; i++)
            {
                var state = document.states[i];
                if (state == null || string.IsNullOrEmpty(state.guid))
                {
                    continue;
                }

                if (byGuid.ContainsKey(state.guid))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(state.name))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        state,
                        FyniteDiagnosticCodes.StateNameEmpty,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Hierarchy,
                        "A state has no name. The name is only a label, but every state needs one so it can " +
                        "be identified in diagnostics and traces."));
                }

                byGuid[state.guid] = state;
                states.Add(state);
            }

            if (states.Count == 0)
            {
                return analysis;
            }

            var root = FindRoot(states, diagnostics);
            var children = BuildChildren(states, byGuid, root, diagnostics);

            if (root == null)
            {
                return analysis;
            }

            if (!ValidateParentChains(states, byGuid, diagnostics))
            {
                return analysis;
            }

            var initialByState = ValidateInitialChildren(states, children, root, diagnostics);

            analysis.Order(root, children, byGuid, states, initialByState, diagnostics);
            return analysis;
        }

        private static FyniteStateDocument FindRoot(List<FyniteStateDocument> states, List<FyniteDiagnostic> diagnostics)
        {
            FyniteStateDocument root = null;

            for (int i = 0; i < states.Count; i++)
            {
                if (!states[i].isRoot)
                {
                    continue;
                }

                if (root == null)
                {
                    root = states[i];
                    continue;
                }

                diagnostics.Add(FyniteDiagnostic.For(
                    states[i],
                    FyniteDiagnosticCodes.RootDuplicate,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Hierarchy,
                    "More than one state is marked as the root. '" + root.name + "' is already the root; a " +
                    "machine has exactly one."));
            }

            if (root == null)
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.RootMissing,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Hierarchy,
                    "No state is marked as the root. Mark the state that owns the whole hierarchy."));
                return null;
            }

            if (!string.IsNullOrEmpty(root.parentGuid))
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    root,
                    FyniteDiagnosticCodes.StateOrphan,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Hierarchy,
                    "The root state has a parent. Disconnect it: only the root may be parentless, and it must be."));
                return null;
            }

            if (root.isInitial)
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    root,
                    FyniteDiagnosticCodes.RootMarkedInitial,
                    FyniteDiagnosticSeverity.Warning,
                    FyniteDiagnosticCategory.Hierarchy,
                    "The root state is marked as an initial child, which means nothing because it has no parent."));
            }

            return root;
        }

        private static Dictionary<string, List<FyniteStateDocument>> BuildChildren(
            List<FyniteStateDocument> states,
            Dictionary<string, FyniteStateDocument> byGuid,
            FyniteStateDocument root,
            List<FyniteDiagnostic> diagnostics)
        {
            var children = new Dictionary<string, List<FyniteStateDocument>>(StringComparer.Ordinal);

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];

                if (string.IsNullOrEmpty(state.parentGuid))
                {
                    if (!state.isRoot)
                    {
                        diagnostics.Add(FyniteDiagnostic.For(
                            state,
                            FyniteDiagnosticCodes.StateOrphan,
                            FyniteDiagnosticSeverity.Error,
                            FyniteDiagnosticCategory.Hierarchy,
                            "This state has no parent and is not the root, so nothing can ever enter it. " +
                            "Connect it to a parent, or mark it as the root."));
                    }

                    continue;
                }

                if (string.Equals(state.parentGuid, state.guid, StringComparison.Ordinal))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        state,
                        FyniteDiagnosticCodes.ParentSelf,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Hierarchy,
                        "This state is its own parent."));
                    continue;
                }

                if (!byGuid.TryGetValue(state.parentGuid, out var parent))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        state,
                        FyniteDiagnosticCodes.ParentNotFound,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Hierarchy,
                        "This state's parent no longer exists."));
                    continue;
                }

                if (!children.TryGetValue(parent.guid, out var list))
                {
                    list = new List<FyniteStateDocument>();
                    children[parent.guid] = list;
                }

                list.Add(state);
            }

            return children;
        }

        private static bool ValidateParentChains(
            List<FyniteStateDocument> states,
            Dictionary<string, FyniteStateDocument> byGuid,
            List<FyniteDiagnostic> diagnostics)
        {
            bool clean = true;

            // 0 = unvisited, 1 = on the chain being walked, 2 = proven to terminate.
            var status = new Dictionary<string, byte>(StringComparer.Ordinal);
            var chain = new List<FyniteStateDocument>();

            for (int i = 0; i < states.Count; i++)
            {
                if (status.TryGetValue(states[i].guid, out byte seen) && seen != 0)
                {
                    continue;
                }

                chain.Clear();
                var current = states[i];

                while (current != null && (!status.TryGetValue(current.guid, out byte state) || state == 0))
                {
                    status[current.guid] = 1;
                    chain.Add(current);
                    current = string.IsNullOrEmpty(current.parentGuid) ||
                              string.Equals(current.parentGuid, current.guid, StringComparison.Ordinal)
                        ? null
                        : byGuid.TryGetValue(current.parentGuid, out var parent)
                            ? parent
                            : null;
                }

                if (current != null && status.TryGetValue(current.guid, out byte onChain) && onChain == 1)
                {
                    clean = false;
                    diagnostics.Add(FyniteDiagnostic.For(
                        current,
                        FyniteDiagnosticCodes.HierarchyCycle,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Hierarchy,
                        "The parent chain loops back on itself: " + DescribeCycle(chain, current) + "."));
                }

                for (int c = 0; c < chain.Count; c++)
                {
                    status[chain[c].guid] = 2;
                }
            }

            return clean;
        }

        private static string DescribeCycle(List<FyniteStateDocument> chain, FyniteStateDocument cycleStart)
        {
            var text = new System.Text.StringBuilder();
            int start = chain.IndexOf(cycleStart);
            if (start < 0)
            {
                start = 0;
            }

            for (int i = start; i < chain.Count; i++)
            {
                text.Append('\'').Append(chain[i].name).Append("' -> ");
            }

            text.Append('\'').Append(cycleStart.name).Append('\'');
            return text.ToString();
        }

        private static Dictionary<string, FyniteStateDocument> ValidateInitialChildren(
            List<FyniteStateDocument> states,
            Dictionary<string, List<FyniteStateDocument>> children,
            FyniteStateDocument root,
            List<FyniteDiagnostic> diagnostics)
        {
            var initialByParent = new Dictionary<string, FyniteStateDocument>(StringComparer.Ordinal);

            for (int i = 0; i < states.Count; i++)
            {
                var parent = states[i];
                if (!children.TryGetValue(parent.guid, out var list) || list.Count == 0)
                {
                    continue;
                }

                FyniteStateDocument initial = null;

                for (int c = 0; c < list.Count; c++)
                {
                    if (!list[c].isInitial)
                    {
                        continue;
                    }

                    if (initial == null)
                    {
                        initial = list[c];
                        continue;
                    }

                    diagnostics.Add(FyniteDiagnostic.For(
                        list[c],
                        FyniteDiagnosticCodes.InitialDuplicate,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Hierarchy,
                        "'" + parent.name + "' already enters '" + initial.name + "' by default. A composite " +
                        "state has exactly one initial child."));
                }

                if (initial == null)
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        parent,
                        FyniteDiagnosticCodes.InitialMissing,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Hierarchy,
                        "'" + parent.name + "' has children but none of them is marked as the initial child, " +
                        "so entering it would not know where to go."));
                    continue;
                }

                initialByParent[parent.guid] = initial;
            }

            // A leaf marked as initial is fine — that flag belongs to the child, and its parent reads it.
            // What is wrong is a state marked initial whose parent has no children, which cannot happen,
            // or a childless state that the author expects to descend further.
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                bool hasChildren = children.TryGetValue(state.guid, out var list) && list.Count > 0;

                if (!hasChildren && state != root && !states[i].isInitial && string.IsNullOrEmpty(state.parentGuid))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        state,
                        FyniteDiagnosticCodes.InitialOnLeaf,
                        FyniteDiagnosticSeverity.Warning,
                        FyniteDiagnosticCategory.Hierarchy,
                        "'" + state.name + "' is a leaf with no parent."));
                }
            }

            return initialByParent;
        }

        private void Order(
            FyniteStateDocument root,
            Dictionary<string, List<FyniteStateDocument>> children,
            Dictionary<string, FyniteStateDocument> byGuid,
            List<FyniteStateDocument> allStates,
            Dictionary<string, FyniteStateDocument> initialByParent,
            List<FyniteDiagnostic> diagnostics)
        {
            // Depth-first from the root, children in authoring order. Deterministic, parent-first, and
            // completely independent of where anything sits on the canvas.
            var stack = new Stack<FyniteStateDocument>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var state = stack.Pop();
                if (IndexByGuid.ContainsKey(state.guid))
                {
                    continue;
                }

                IndexByGuid[state.guid] = Ordered.Count;
                Ordered.Add(state);

                if (!children.TryGetValue(state.guid, out var list))
                {
                    continue;
                }

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    stack.Push(list[i]);
                }
            }

            for (int i = 0; i < allStates.Count; i++)
            {
                if (!IndexByGuid.ContainsKey(allStates[i].guid))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        allStates[i],
                        FyniteDiagnosticCodes.StateUnreachable,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Hierarchy,
                        "'" + allStates[i].name + "' cannot be reached from the root '" + root.name + "'."));
                }
            }

            RootIndex = IndexByGuid[root.guid];

            for (int i = 0; i < Ordered.Count; i++)
            {
                var state = Ordered[i];

                _parentIndices.Add(
                    !string.IsNullOrEmpty(state.parentGuid) && IndexByGuid.TryGetValue(state.parentGuid, out int parent)
                        ? parent
                        : -1);

                _initialIndices.Add(
                    initialByParent.TryGetValue(state.guid, out var initial) && IndexByGuid.TryGetValue(initial.guid, out int initialIndex)
                        ? initialIndex
                        : -1);
            }
        }
    }
}
