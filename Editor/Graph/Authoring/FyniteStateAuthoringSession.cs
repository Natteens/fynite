using System;
using System.Collections.Generic;
using UnityEditor;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// The state authoring commands as the <c>.fyn</c> inspector runs them, without any GUI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a command surface, not an editor. It selects a state by identity, reports what that state
    /// is, and runs one <see cref="FyniteStateOperations"/> call at a time against the loaded
    /// <see cref="FyniteGraph"/> — the same object the canvas has open and the same object the importer
    /// compiles. It draws nothing, mirrors nothing and remembers no hierarchy of its own: every list it
    /// returns is read out of the graph at the moment it is asked, so it cannot drift from the file.
    /// </para>
    /// <para>
    /// It exists because the hierarchy operations have no home on the canvas. Graph Toolkit's node
    /// context menu, its command pipeline and its selection are not extensible from a package, so
    /// marking a child Initial or reparenting a subtree cannot be offered where the wires are drawn.
    /// The inspector of the file is the surface Unity does let a package own, so that is where the
    /// commands live — as a form of popups, fields and buttons, which is what an inspector is, and
    /// deliberately not as anything that resembles a canvas.
    /// </para>
    /// <para>
    /// Every command that succeeds writes the file and reimports it, so the compiled asset the user sees
    /// underneath always describes the graph as it now is.
    /// </para>
    /// </remarks>
    public sealed class FyniteStateAuthoringSession
    {
        private string _selectedGuid;

        private FyniteStateAuthoringSession(string assetPath, FyniteGraph graph)
        {
            AssetPath = assetPath;
            Graph = graph;
        }

        /// <summary>Path of the <c>.fyn</c> this session edits.</summary>
        public string AssetPath { get; }

        /// <summary>The graph being edited. Never a copy.</summary>
        public FyniteGraph Graph { get; }

        /// <summary>Identity of the selected state, or null when nothing is selected.</summary>
        public string SelectedGuid => _selectedGuid;

        /// <summary>Opens a session over the graph stored at a path.</summary>
        public static FyniteStateAuthoringSession Load(string assetPath)
        {
            var graph = string.IsNullOrEmpty(assetPath) ? null : GraphDatabase.LoadGraph<FyniteGraph>(assetPath);
            return graph != null ? new FyniteStateAuthoringSession(assetPath, graph) : null;
        }

        /// <summary>Opens a session over a graph already loaded by someone else.</summary>
        /// <remarks>
        /// The inspector shares its graph instance with this session on purpose. Two independent loads
        /// of the same file would be two objects the user could edit into disagreement, and the last one
        /// saved would silently win.
        /// </remarks>
        public static FyniteStateAuthoringSession For(string assetPath, FyniteGraph graph) =>
            graph != null ? new FyniteStateAuthoringSession(assetPath, graph) : null;

        /// <summary>Every state in the graph, ordered by hierarchical path.</summary>
        public List<FyniteStateNode> States()
        {
            var states = new List<FyniteStateNode>();

            foreach (var node in Graph.GetNodes())
            {
                if (node is FyniteStateNode state)
                {
                    states.Add(state);
                }
            }

            states.Sort((left, right) => string.CompareOrdinal(
                FyniteStateOperations.PathOf(left), FyniteStateOperations.PathOf(right)));
            return states;
        }

        /// <summary>The selected state, or null when the selection is empty or no longer exists.</summary>
        public FyniteStateNode SelectedState() => FyniteStateOperations.FindState(Graph, _selectedGuid);

        /// <summary>Selects a state by identity. Passing an unknown identity clears the selection.</summary>
        public bool Select(string fyniteGuid)
        {
            var state = FyniteStateOperations.FindState(Graph, fyniteGuid);
            _selectedGuid = state?.FyniteGuid;
            return state != null;
        }

        /// <summary>Path of the selected state, from the root down, or null when nothing is selected.</summary>
        public string SelectedPath()
        {
            var state = SelectedState();
            return state != null ? FyniteStateOperations.PathOf(state) : null;
        }

        /// <summary>Nodes the selected state may be reparented to.</summary>
        public List<IFyniteIdentifiedNode> ParentChoices() =>
            FyniteStateOperations.ValidParents(Graph, SelectedState());

        /// <summary>States connected to no parent.</summary>
        public List<FyniteStateNode> Orphans() => FyniteStateOperations.Orphans(Graph);

        /// <summary>Adds a child under the selected state and selects it.</summary>
        public bool CreateChild(out string error) =>
            CreateChildOf(_selectedGuid, out error);

        /// <summary>Adds a child under any state or under the root, and selects it.</summary>
        public bool CreateChildOf(string parentGuid, out string error)
        {
            error = null;
            var parent = FyniteStateOperations.FindParentTarget(Graph, parentGuid);

            if (parent == null)
            {
                error = "Choose the parent the new state belongs under.";
                return false;
            }

            var position = NextChildPosition(parent);
            var child = FyniteStateOperations.CreateChild(Graph, parent, position);

            if (child == null)
            {
                error = "The state could not be created.";
                return false;
            }

            _selectedGuid = child.FyniteGuid;
            Persist();
            return true;
        }

        /// <summary>Renames the selected state, keeping its identity and every wire attached to it.</summary>
        public bool Rename(string name, out string error)
        {
            error = null;
            var state = SelectedState();

            if (state == null)
            {
                error = "Select a state first.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "A state needs a name.";
                return false;
            }

            if (!FyniteStateOperations.Rename(Graph, state, name))
            {
                error = "That is already the name of this state.";
                return false;
            }

            Persist();
            return true;
        }

        /// <summary>Makes the selected state the one its parent enters by default.</summary>
        public bool SetAsInitial(out string error)
        {
            error = null;
            var state = SelectedState();

            if (state == null)
            {
                error = "Select a state first.";
                return false;
            }

            if (state.ResolveParent() == null)
            {
                error = "An orphan has no parent to be the initial child of. Give it a parent first.";
                return false;
            }

            if (!FyniteStateOperations.SetAsInitial(Graph, state))
            {
                error = "The state could not be marked as initial.";
                return false;
            }

            Persist();
            return true;
        }

        /// <summary>Adds a sibling copy of the selected state and selects it.</summary>
        public bool Duplicate(out string error)
        {
            error = null;
            var state = SelectedState();

            if (state == null)
            {
                error = "Select a state first.";
                return false;
            }

            if (state.ResolveParent() == null)
            {
                error = "An orphan cannot be duplicated: the copy would have nowhere to go.";
                return false;
            }

            var copy = FyniteStateOperations.Duplicate(Graph, state, state.Position + new Vector2(0f, 160f));

            if (copy == null)
            {
                error = "The state could not be duplicated.";
                return false;
            }

            _selectedGuid = copy.FyniteGuid;
            Persist();
            return true;
        }

        /// <summary>Reparents the selected state to an explicitly chosen node.</summary>
        public bool MoveTo(string parentGuid, out string error) =>
            Reparent(SelectedState(), parentGuid, out error);

        /// <summary>Gives an orphan a parent, and selects the recovered state.</summary>
        public bool AdoptOrphan(string orphanGuid, string parentGuid, out string error)
        {
            error = null;
            var orphan = FyniteStateOperations.FindState(Graph, orphanGuid);

            if (orphan == null)
            {
                error = "Choose the state to recover.";
                return false;
            }

            if (orphan.ResolveParent() != null)
            {
                error = "'" + orphan.StateName + "' already has a parent.";
                return false;
            }

            if (!Reparent(orphan, parentGuid, out error))
            {
                return false;
            }

            _selectedGuid = orphan.FyniteGuid;
            return true;
        }

        /// <summary>Writes the graph and recompiles the asset the file produces.</summary>
        /// <remarks>
        /// Public because a caller that ran several operations may prefer one save. Each command already
        /// calls it, so the inspector never has to.
        /// </remarks>
        public void Persist()
        {
            GraphDatabase.SaveGraph(Graph);

            if (string.IsNullOrEmpty(AssetPath))
            {
                return;
            }

            // Let the Asset Database observe the written file before compiling it. A forced import
            // issued against the just-saved object can otherwise reuse the graph the importer already
            // had loaded and compile the previous shape once more.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>The compiled asset this graph currently produces, or null before the first import.</summary>
        public FyniteGraphAsset CompiledAsset() =>
            string.IsNullOrEmpty(AssetPath) ? null : AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(AssetPath);

        private bool Reparent(FyniteStateNode state, string parentGuid, out string error)
        {
            error = null;

            if (state == null)
            {
                error = "Select a state first.";
                return false;
            }

            var parent = FyniteStateOperations.FindParentTarget(Graph, parentGuid);

            if (parent == null)
            {
                // Nothing is guessed here. Without an explicit choice the command does not run, because
                // a silently picked parent is a hierarchy the user did not author.
                error = "Choose the parent to move this state under.";
                return false;
            }

            if (!FyniteStateOperations.MoveToParent(Graph, state, parent, out var reason))
            {
                error = reason ?? "'" + state.StateName + "' is already under that parent.";
                return false;
            }

            Persist();
            return true;
        }

        /// <summary>
        /// Somewhere free below the parent, so a created or duplicated node does not land on another.
        /// </summary>
        /// <remarks>
        /// This is a starting position, not a layout. Graph Toolkit owns where nodes end up once the user
        /// drags them, and nothing here ever moves a node the user placed.
        /// </remarks>
        private static Vector2 NextChildPosition(IFyniteIdentifiedNode parent)
        {
            var origin = parent is FyniteRootStateNode root
                ? root.Position
                : ((FyniteStateNode)parent).Position;

            var siblings = FyniteStateOperations.ChildrenOf(parent);
            return origin + new Vector2(320f, siblings.Count * 160f);
        }
    }
}
