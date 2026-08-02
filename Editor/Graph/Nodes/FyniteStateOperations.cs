using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>Atomic state authoring operations implemented through Graph Toolkit's public graph API.</summary>
    /// <remarks>
    /// <para>
    /// Every operation here edits the real nodes of a loaded <see cref="FyniteGraph"/>. There is no
    /// second model, no staged copy and no shadow hierarchy: what these methods change is what
    /// <see cref="FyniteGraphProjection"/> reads and what the importer compiles.
    /// </para>
    /// <para>
    /// The Initial mark is maintained here rather than exposed as a field the user ticks, because it is
    /// an invariant of a parent and not a property of a child: a composite has exactly one, a leaf has
    /// none, and no sequence of these operations may leave a composite without one. Every method below
    /// that can disturb that invariant repairs it before returning.
    /// </para>
    /// </remarks>
    public static class FyniteStateOperations
    {
        public static bool Rename(FyniteGraph graph, FyniteStateNode state, string name)
        {
            if (graph == null || state == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var normalized = name.Trim();
            if (string.Equals(normalized, state.StateName, StringComparison.Ordinal))
            {
                return false;
            }

            // A rename writes a serialized field and nothing else — no node appears, no wire moves — so
            // it is one of the edits Graph Toolkit's change model does not see on its own.
            graph.UndoBeginRecordGraph("Rename State");
            state.SetDisplayName(normalized);
            graph.TouchForFieldChange();
            graph.UndoEndRecordGraph();
            return true;
        }

        /// <summary>
        /// Makes this state the one its parent enters by default.
        /// </summary>
        /// <remarks>
        /// Only the direct siblings are cleared. The mark means "the child this parent enters", so a
        /// state deeper in the tree holding it for its own parent is unrelated and must survive.
        /// </remarks>
        public static bool SetAsInitial(FyniteGraph graph, FyniteStateNode selected)
        {
            var parent = selected?.ResolveParent();
            if (graph == null || selected == null || parent == null)
            {
                return false;
            }

            var siblings = ChildrenOf(parent);

            graph.UndoBeginRecordGraph("Set as Initial");
            for (int i = 0; i < siblings.Count; i++)
            {
                siblings[i].SetInitial(siblings[i] == selected);
            }

            graph.TouchForFieldChange();
            graph.UndoEndRecordGraph();
            return true;
        }

        public static FyniteStateNode CreateChild(FyniteGraph graph, IFyniteIdentifiedNode parent, Vector2 position)
        {
            if (graph == null || !(parent is FyniteRootStateNode) && !(parent is FyniteStateNode))
            {
                return null;
            }

            var existing = ChildrenOf(parent);
            var child = new FyniteStateNode();

            graph.UndoBeginRecordGraph("Create Child State");
            graph.AddNode(child);
            child.SetDisplayName(NextDefaultName(existing));
            child.Position = position;
            graph.Connect(ChildrenPort(parent), child.GetInputPortByName(FyniteStateNode.ParentPort));

            // The first child of a parent becomes Initial because a composite without one does not
            // compile. A later child never displaces it. The condition is written as "the parent has no
            // Initial yet" rather than "the parent has no children yet" so that a hierarchy wired by
            // hand on the canvas — which cannot mark anything Initial — is repaired by the first
            // controlled child added to it, instead of staying uncompilable.
            child.SetInitial(!HasInitial(existing));
            graph.UndoEndRecordGraph();
            return child;
        }

        public static bool MoveToParent(
            FyniteGraph graph, FyniteStateNode state, IFyniteIdentifiedNode newParent, out string error)
        {
            error = null;
            if (graph == null || state == null || newParent == null ||
                !(newParent is FyniteRootStateNode) && !(newParent is FyniteStateNode))
            {
                error = "A State and a valid Parent are required.";
                return false;
            }

            if (ReferenceEquals(state, newParent))
            {
                error = "A State cannot be its own Parent.";
                return false;
            }

            if (newParent is FyniteStateNode parentState && IsDescendantOf(parentState, state))
            {
                error = "Moving '" + state.StateName + "' below '" + parentState.StateName +
                        "' would create a hierarchy cycle (" + PathOf(state) + " / " + parentState.StateName + ").";
                return false;
            }

            if (ReferenceEquals(state.ResolveParent(), newParent))
            {
                return false;
            }

            // Read the destination before rewiring: once the state is connected it counts as one of its
            // own siblings, and "does this parent already have an Initial" would answer about the state
            // being moved.
            bool destinationNeedsInitial = !HasInitial(ChildrenOf(newParent));
            var oldParent = state.ResolveParent();
            bool wasInitial = state.IsInitial;

            graph.UndoBeginRecordGraph("Move to Parent");
            if (oldParent != null)
            {
                graph.Disconnect(ChildrenPort(oldParent), state.GetInputPortByName(FyniteStateNode.ParentPort));
            }

            graph.Connect(ChildrenPort(newParent), state.GetInputPortByName(FyniteStateNode.ParentPort));

            // Moving an Initial into a group that already has one must not produce two. Moving into an
            // empty group — or one whose Initial is missing — makes the moved state Initial, because
            // otherwise this edit would create a composite that cannot be entered.
            state.SetInitial(destinationNeedsInitial);

            // The move can strip the old parent of its Initial while leaving it composite. Nothing else
            // would put one back, and the graph would stop compiling because of an edit made somewhere
            // else entirely, so the successor is chosen here.
            if (wasInitial)
            {
                RepairInitial(graph, oldParent);
            }

            graph.UndoEndRecordGraph();
            return true;
        }

        /// <summary>
        /// Gives a parent an Initial child again when it still has children but lost the one it had.
        /// </summary>
        /// <remarks>
        /// A parent left with no children needs nothing: it is a leaf, and a leaf with an Initial is
        /// itself an error. A parent that still has children gets the first of them in the order
        /// <see cref="Graph.GetNodes"/> reports, which is the order the projection walks and therefore
        /// the order the compiler already calls "first". That order is serialized with the file, so the
        /// same move on the same graph always elects the same successor.
        /// </remarks>
        private static void RepairInitial(FyniteGraph graph, IFyniteIdentifiedNode parent)
        {
            if (graph == null || parent == null)
            {
                return;
            }

            var remaining = ChildrenOf(parent);
            if (remaining.Count == 0 || HasInitial(remaining))
            {
                return;
            }

            var successor = FirstInGraphOrder(graph, remaining);
            successor?.SetInitial(true);
        }

        /// <summary>True when one of these siblings is already the Initial child.</summary>
        private static bool HasInitial(List<FyniteStateNode> siblings)
        {
            for (int i = 0; i < siblings.Count; i++)
            {
                if (siblings[i].IsInitial)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The candidate the graph stores first, so the choice never depends on wiring order.</summary>
        private static FyniteStateNode FirstInGraphOrder(FyniteGraph graph, List<FyniteStateNode> candidates)
        {
            foreach (var node in graph.GetNodes())
            {
                if (node is FyniteStateNode state && candidates.Contains(state))
                {
                    return state;
                }
            }

            return candidates.Count > 0 ? candidates[0] : null;
        }

        public static FyniteStateNode Duplicate(FyniteGraph graph, FyniteStateNode source, Vector2 position)
        {
            var parent = source?.ResolveParent();
            if (graph == null || source == null || parent == null)
            {
                return null;
            }

            var duplicate = new FyniteStateNode();
            graph.UndoBeginRecordGraph("Duplicate State");
            graph.AddNode(duplicate);
            duplicate.SetDisplayName(source.StateName);
            duplicate.Position = position;
            graph.Connect(ChildrenPort(parent), duplicate.GetInputPortByName(FyniteStateNode.ParentPort));

            // The copy is a sibling, never a second Initial. It also gets its own identity — the node was
            // constructed rather than cloned, so nothing carried the source's GUID across — which is what
            // keeps reactions and external references pointing at the state they were authored against.
            duplicate.SetInitial(false);
            graph.UndoEndRecordGraph();
            return duplicate;
        }

        public static List<FyniteStateNode> ChildrenOf(IFyniteIdentifiedNode parent)
        {
            if (parent is FyniteRootStateNode root)
            {
                return root.ResolveChildren();
            }

            return parent is FyniteStateNode state ? state.ResolveChildren() : new List<FyniteStateNode>();
        }

        /// <summary>
        /// Every node this state could legally be parented to, ordered by path.
        /// </summary>
        /// <remarks>
        /// The root is included: reparenting a state to the top level is an ordinary edit, and a list
        /// that left it out would make the one hierarchy every graph has unreachable. The state itself
        /// and everything below it are excluded, which is what makes self-parenting and cycles
        /// unofferable rather than merely refused.
        /// </remarks>
        public static List<IFyniteIdentifiedNode> ValidParents(FyniteGraph graph, FyniteStateNode state)
        {
            if (graph == null || state == null)
            {
                return new List<IFyniteIdentifiedNode>();
            }

            return graph.GetNodes()
                .Select(node => node as IFyniteIdentifiedNode)
                .Where(candidate =>
                    candidate is FyniteRootStateNode ||
                    (candidate is FyniteStateNode s && s != state && !IsDescendantOf(s, state)))
                .OrderBy(PathOf, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// States that are not connected to any parent.
        /// </summary>
        /// <remarks>
        /// An orphan is not a shape the hierarchy allows: it belongs to no machine, the compiler warns
        /// about it, and dragging a wire is the only other way out. Listing them is what lets the
        /// authoring surface offer the state a home instead of the user hunting for a node that may be
        /// anywhere on the canvas.
        /// </remarks>
        public static List<FyniteStateNode> Orphans(FyniteGraph graph)
        {
            if (graph == null)
            {
                return new List<FyniteStateNode>();
            }

            return graph.GetNodes()
                .OfType<FyniteStateNode>()
                .Where(state => state.ResolveParent() == null)
                .ToList();
        }

        /// <summary>The state carrying this identity, or null when the graph has none.</summary>
        public static FyniteStateNode FindState(FyniteGraph graph, string fyniteGuid)
        {
            if (graph == null || string.IsNullOrEmpty(fyniteGuid))
            {
                return null;
            }

            foreach (var node in graph.GetNodes())
            {
                if (node is FyniteStateNode state && string.Equals(state.FyniteGuid, fyniteGuid, StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return null;
        }

        /// <summary>The node carrying this identity when it can hold children, or null.</summary>
        public static IFyniteIdentifiedNode FindParentTarget(FyniteGraph graph, string fyniteGuid)
        {
            if (graph == null || string.IsNullOrEmpty(fyniteGuid))
            {
                return null;
            }

            foreach (var node in graph.GetNodes())
            {
                if (!(node is IFyniteIdentifiedNode identified) ||
                    !string.Equals(identified.FyniteGuid, fyniteGuid, StringComparison.Ordinal))
                {
                    continue;
                }

                if (identified is FyniteRootStateNode || identified is FyniteStateNode)
                {
                    return identified;
                }
            }

            return null;
        }

        public static string PathOf(IFyniteIdentifiedNode node)
        {
            if (node is FyniteRootStateNode)
            {
                return FyniteRootStateNode.RootName;
            }

            var names = new List<string>();
            var current = node as FyniteStateNode;
            bool reachesRoot = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (current != null && seen.Add(current.FyniteGuid))
            {
                names.Add(current.StateName);
                var parent = current.ResolveParent();
                reachesRoot = parent is FyniteRootStateNode;
                current = parent as FyniteStateNode;
            }

            names.Add(reachesRoot ? FyniteRootStateNode.RootName : "Unparented States");
            names.Reverse();
            return string.Join(" / ", names);
        }

        private static bool IsDescendantOf(FyniteStateNode candidate, FyniteStateNode ancestor)
        {
            var current = candidate.ResolveParentState();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (current != null && seen.Add(current.FyniteGuid))
            {
                if (current == ancestor)
                {
                    return true;
                }
                current = current.ResolveParentState();
            }
            return false;
        }

        private static IPort ChildrenPort(IFyniteIdentifiedNode parent) =>
            parent is FyniteRootStateNode root
                ? root.GetOutputPortByName(FyniteRootStateNode.ChildrenPort)
                : ((FyniteStateNode)parent).GetOutputPortByName(FyniteStateNode.ChildrenPort);

        private static string NextDefaultName(List<FyniteStateNode> siblings)
        {
            var names = new HashSet<string>(siblings.Select(s => s.StateName), StringComparer.Ordinal);
            if (!names.Contains(FyniteStateNode.DefaultName))
            {
                return FyniteStateNode.DefaultName;
            }

            for (int suffix = 2; ; suffix++)
            {
                var candidate = FyniteStateNode.DefaultName + " " + suffix;
                if (!names.Contains(candidate))
                {
                    return candidate;
                }
            }
        }
    }
}
