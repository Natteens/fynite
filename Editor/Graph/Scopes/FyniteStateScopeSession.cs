using System;
using System.Collections.Generic;
using System.Linq;

namespace Fynite.GraphEditor
{
    /// <summary>Per-window, non-semantic navigation state for one focused hierarchy projection.</summary>
    internal sealed class FyniteStateScopeSession
    {
        private string _ownerGuid;

        internal FyniteStateScopeSession(FyniteGraph graph)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _ownerGuid = graph.FindRoot()?.FyniteGuid;
        }

        internal FyniteGraph Graph { get; private set; }
        internal IFyniteIdentifiedNode Owner => Resolve(_ownerGuid) ?? Graph.FindRoot();
        internal IReadOnlyList<FyniteStateNode> VisibleChildren => FyniteStateOperations.ChildrenOf(Owner);
        internal string Breadcrumb => Owner != null ? FyniteStateOperations.PathOf(Owner) : "Missing Root";

        internal void ReplaceGraph(FyniteGraph graph)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            Repair();
        }

        internal bool Open(FyniteStateNode state)
        {
            if (state == null)
            {
                return false;
            }
            _ownerGuid = state.FyniteGuid;
            return true;
        }

        internal bool Back()
        {
            if (Owner is FyniteStateNode state && state.ResolveParent() != null)
            {
                _ownerGuid = state.ResolveParent().FyniteGuid;
                return true;
            }
            return false;
        }

        internal void ToRoot() => _ownerGuid = Graph.FindRoot()?.FyniteGuid;

        internal void OpenAncestor(IFyniteIdentifiedNode ancestor)
        {
            if (ancestor != null)
            {
                _ownerGuid = ancestor.FyniteGuid;
            }
        }

        internal void Repair()
        {
            var current = Resolve(_ownerGuid);
            if (current != null)
            {
                return;
            }

            // A deleted scope cannot yield its former parent after reload because that relationship was
            // on the deleted node. The nearest still-known ancestor is tracked by the window when it
            // navigates back; when none can be resolved, Root is the safe presentation fallback.
            ToRoot();
        }

        internal IReadOnlyList<IFyniteIdentifiedNode> BreadcrumbNodes()
        {
            var nodes = new List<IFyniteIdentifiedNode>();
            var current = Owner;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (current != null && seen.Add(current.FyniteGuid))
            {
                nodes.Add(current);
                current = (current as FyniteStateNode)?.ResolveParent();
            }
            nodes.Reverse();
            return nodes;
        }

        private IFyniteIdentifiedNode Resolve(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }
            return Graph.GetNodes().OfType<IFyniteIdentifiedNode>().FirstOrDefault(n => n.FyniteGuid == guid);
        }
    }
}
