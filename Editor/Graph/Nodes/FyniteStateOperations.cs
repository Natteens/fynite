using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>Atomic authoring operations shared by the State Scope window and tests.</summary>
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

            graph.UndoBeginRecordGraph("Rename State");
            state.SetDisplayName(normalized);
            graph.UndoEndRecordGraph();
            return true;
        }

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
                FyniteNodeOptions.Set(siblings[i], FyniteStateNode.InitialOption, siblings[i] == selected);
            }
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
            FyniteNodeOptions.Set(child, FyniteStateNode.InitialOption, existing.Count == 0);
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

            bool firstAtDestination = ChildrenOf(newParent).Count == 0;
            var oldParent = state.ResolveParent();

            graph.UndoBeginRecordGraph("Move to Parent");
            if (oldParent != null)
            {
                graph.Disconnect(ChildrenPort(oldParent), state.GetInputPortByName(FyniteStateNode.ParentPort));
            }

            graph.Connect(ChildrenPort(newParent), state.GetInputPortByName(FyniteStateNode.ParentPort));
            // Moving an Initial into a group that already has one must not duplicate Initial. A first
            // child, however, always becomes Initial as part of this explicit edit.
            FyniteNodeOptions.Set(state, FyniteStateNode.InitialOption, firstAtDestination);
            graph.UndoEndRecordGraph();
            return true;
        }

        public static List<FyniteStateNode> ChildrenOf(IFyniteIdentifiedNode parent)
        {
            if (parent is FyniteRootStateNode root)
            {
                return root.ResolveChildren();
            }

            return parent is FyniteStateNode state ? state.ResolveChildren() : new List<FyniteStateNode>();
        }

        public static List<FyniteStateNode> ValidParents(FyniteGraph graph, FyniteStateNode state)
        {
            var result = graph.GetNodes().OfType<FyniteStateNode>()
                .Where(candidate => candidate != state && !IsDescendantOf(candidate, state))
                .OrderBy(PathOf, StringComparer.Ordinal)
                .ToList();
            return result;
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
