using System;
using Fynite.Authoring;
using Fynite.Editor;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>Edits graph-wide Fynite metadata without putting settings nodes on the canvas.</summary>
    [CustomEditor(typeof(FyniteGraphImporter))]
    public sealed class FyniteGraphImporterEditor : ScriptedImporterEditor
    {
        private FyniteGraphInspectorSession _session;

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            ReloadSession();
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            // ScriptedImporterEditor requires exactly one call on every draw. In particular, keep it
            // outside the graph-load branch so a temporarily unreadable file still honours the base
            // editor's Apply/Revert contract.
            DrawSettings();
            ApplyRevertGUI();
        }

        private void DrawSettings()
        {
            if (_session == null)
            {
                ReloadSession();
            }

            if (_session == null || _session.Graph == null)
            {
                EditorGUILayout.HelpBox("The .fyn graph could not be loaded.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Graph Settings", EditorStyles.boldLabel);

            FyniteContextTypePicker.Draw(
                _session.PendingType,
                _session.PendingReference,
                _session.Select);

            EditorGUILayout.HelpBox(
                "The graph declares the context type. A FyniteRunner automatically resolves and serializes " +
                "the matching component instance on its own GameObject.",
                MessageType.Info);

            EditorGUILayout.Space();
            var asset = AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(_session.AssetPath);
            FyniteGraphAssetSummary.Draw(asset);
        }

        /// <summary>
        /// Draws and executes Apply/Revert for graph metadata staged by this editor.
        /// </summary>
        /// <remarks>
        /// The importer itself owns no duplicate Context Type field. The pending value exists only for
        /// this inspector session; Apply writes the graph once and reimports once, while Revert reloads
        /// the value already in the file without touching disk.
        /// </remarks>
        protected override bool OnApplyRevertGUI()
        {
            bool changed = _session != null && _session.HasChanges;
            bool applied = false;

            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(!changed))
            {
                if (GUILayout.Button("Revert"))
                {
                    _session.Revert();
                }

                if (GUILayout.Button("Apply"))
                {
                    _session.Apply();
                    applied = true;
                }
            }

            return applied;
        }

        private void ReloadSession()
        {
            var importer = target as AssetImporter;
            _session = importer != null
                ? FyniteGraphInspectorSession.Load(importer.assetPath)
                : null;
        }
    }

    /// <summary>Apply/Revert state for graph-wide settings, separated so the real flow is testable.</summary>
    internal sealed class FyniteGraphInspectorSession
    {
        private string _persistedIdentity;
        private string _pendingIdentity;

        private FyniteGraphInspectorSession(string assetPath, FyniteGraph graph)
        {
            AssetPath = assetPath;
            Graph = graph;
            _persistedIdentity = graph.ContextTypeReference.Identity;
            _pendingIdentity = _persistedIdentity;
        }

        internal string AssetPath { get; }
        internal FyniteGraph Graph { get; }
        internal bool HasChanges => !string.Equals(_persistedIdentity, _pendingIdentity, StringComparison.Ordinal);
        internal FyniteTypeReference PendingReference => new FyniteTypeReference(_pendingIdentity);
        internal Type PendingType => PendingReference.IsSet ? FyniteTypeResolver.Resolve(PendingReference) : null;

        internal static FyniteGraphInspectorSession Load(string assetPath)
        {
            var graph = string.IsNullOrEmpty(assetPath) ? null : GraphDatabase.LoadGraph<FyniteGraph>(assetPath);
            return graph != null ? new FyniteGraphInspectorSession(assetPath, graph) : null;
        }

        internal void Select(Type selected) => _pendingIdentity = FyniteTypeReference.Format(selected);

        internal void Revert() => _pendingIdentity = _persistedIdentity;

        internal void Apply()
        {
            if (!HasChanges)
            {
                return;
            }

            Graph.UndoBeginRecordGraph("Change Context Type");
            Graph.SetContextTypeIdentity(_pendingIdentity);
            Graph.TouchForFieldChange();
            Graph.UndoEndRecordGraph();
            GraphDatabase.SaveGraph(Graph);
            // Let the Asset Database observe the changed source before importing it. A forced import
            // issued immediately against Graph Toolkit's just-saved object can reuse the importer's
            // previous loaded graph and compile the old context value once more.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            _persistedIdentity = _pendingIdentity;
        }
    }
}
