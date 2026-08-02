using UnityEditor;

namespace Fynite.Editor
{
    /// <summary>
    /// Inspector for a compiled graph: whether it compiled, and what it compiled to.
    /// </summary>
    /// <remarks>
    /// A <c>.fyn</c> file is shown through its importer's inspector, so this is what appears when the
    /// compiled asset is inspected on its own. Both draw the same summary, so they cannot disagree.
    /// </remarks>
    [CustomEditor(typeof(FyniteGraphAsset))]
    public sealed class FyniteGraphAssetEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override void OnInspectorGUI() => FyniteGraphAssetSummary.Draw((FyniteGraphAsset)target);
    }
}
