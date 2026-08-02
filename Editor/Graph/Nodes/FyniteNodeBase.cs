using System;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Gives every Fynite node a persistent identity that survives saving, reloading and renaming.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The GUID is a plain serialized field rather than a node option, because it is not something the
    /// user should ever see or type. Graph Toolkit stores the node object with the graph, so the value
    /// round-trips with everything else.
    /// </para>
    /// <para>
    /// It is assigned lazily in <see cref="OnEnable"/> so that a node created from the search menu — which
    /// Graph Toolkit constructs itself — still gets one. Copy and paste clones the field along with the
    /// rest of the node, so the graph re-issues identities for duplicates; see
    /// <see cref="FyniteGraph.OnGraphChanged"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public abstract class FyniteNodeBase : Node, IFyniteIdentifiedNode
    {
        [SerializeField]
        [HideInInspector]
        private string m_FyniteGuid;

        /// <inheritdoc />
        public string FyniteGuid => m_FyniteGuid;

        /// <inheritdoc />
        public void AssignNewFyniteGuid() => m_FyniteGuid = FyniteGuids.New();

        /// <inheritdoc />
        public void AdoptFyniteGuid(string guid) => m_FyniteGuid = guid;

        /// <inheritdoc />
        public void EnsureFyniteGuid()
        {
            if (string.IsNullOrEmpty(m_FyniteGuid))
            {
                m_FyniteGuid = FyniteGuids.New();
            }
        }

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            EnsureFyniteGuid();
        }
    }

    /// <summary>Anything in the graph carrying a persistent Fynite identity.</summary>
    public interface IFyniteIdentifiedNode
    {
        /// <summary>The persistent identity, or null before one was issued.</summary>
        string FyniteGuid { get; }

        /// <summary>Issues a fresh identity, discarding the current one.</summary>
        void AssignNewFyniteGuid();

        /// <summary>
        /// Takes over an existing identity.
        /// </summary>
        /// <remarks>
        /// Only migration does this, and only to carry a node's identity across a change of node kind so
        /// that everything referring to it still refers to the same thing.
        /// </remarks>
        void AdoptFyniteGuid(string guid);

        /// <summary>Issues an identity only if there is none.</summary>
        void EnsureFyniteGuid();
    }

    /// <summary>Creates the identities used throughout the authoring model.</summary>
    public static class FyniteGuids
    {
        /// <summary>A fresh identity, compact and stable.</summary>
        public static string New() => Guid.NewGuid().ToString("N");
    }
}
