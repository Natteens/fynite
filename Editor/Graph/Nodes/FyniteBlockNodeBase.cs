using System;
using System.Collections.Generic;
using System.Reflection;
using Fynite.Authoring;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Shared persisted data and compilation projection for every block node.
    /// </summary>
    /// <remarks>The script reference and configuration JSON are the single persisted representation.</remarks>
    [Serializable]
    public abstract class FyniteBlockNodeBase : BlockNode, IFyniteIdentifiedNode
    {
        [SerializeField]
        [HideInInspector]
        private string m_FyniteGuid;

        [SerializeField, HideInInspector]
        private MonoScript m_BlockScript;

        [SerializeField, HideInInspector]
        private string m_ConfigJson;

        /// <inheritdoc />
        public string FyniteGuid => m_FyniteGuid;

        /// <summary>Whether this slot takes an action or a guard.</summary>
        public abstract FyniteBlockKind BlockKind { get; }

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

        /// <summary>The block type this node currently selects, or null.</summary>
        public Type ResolveBlockType() => m_BlockScript != null ? m_BlockScript.GetClass() : null;

        /// <summary>Selects the block script through Fynite's public authoring surface.</summary>
        public void SetBlockScript(MonoScript script)
        {
            m_BlockScript = script;
            var type = ResolveBlockType();
            m_ConfigJson = type != null
                ? FyniteBlockFactory.SerializeConfiguration(FyniteBlockFactory.CreateDefault(type))
                : null;
        }

        /// <summary>Sets one serialized configuration field on the selected block.</summary>
        public bool SetConfiguration(string fieldName, object value)
        {
            var type = ResolveBlockType();
            var prototype = type != null ? FyniteBlockFactory.CreateDefault(type) : null;
            if (prototype == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(m_ConfigJson))
            {
                JsonUtility.FromJsonOverwrite(m_ConfigJson, prototype);
            }

            foreach (var field in EnumerateConfigurableFields(type))
            {
                if (field.Name != fieldName)
                {
                    continue;
                }

                field.SetValue(prototype, value);
                m_ConfigJson = FyniteBlockFactory.SerializeConfiguration(prototype);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads the configuration the user typed into the generated options and returns it in the form
        /// an authoring occurrence stores.
        /// </summary>
        /// <param name="blockType">The resolved block type.</param>
        /// <returns>Serialized configuration, or null when there is nothing to store.</returns>
        public string ReadConfiguration(Type blockType)
        {
            return m_ConfigJson;
        }

        /// <summary>Fields of a block that make up its authored configuration.</summary>
        /// <remarks>
        /// The same rules Unity uses for serialization, because that is what the runtime asset will
        /// store: public instance fields, plus non-public ones marked <c>[SerializeField]</c>, minus
        /// anything explicitly excluded.
        /// </remarks>
        public static IEnumerable<FieldInfo> EnumerateConfigurableFields(Type blockType)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var declaring = new List<Type>();

            for (var type = blockType; type != null && type != typeof(FyniteBlock) && type != typeof(object); type = type.BaseType)
            {
                declaring.Add(type);
            }

            // Base fields first so the inspector reads top-down the way the class hierarchy does.
            for (int i = declaring.Count - 1; i >= 0; i--)
            {
                var fields = declaring[i].GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                for (int f = 0; f < fields.Length; f++)
                {
                    var field = fields[f];

                    if (field.IsInitOnly || field.IsLiteral || !seen.Add(field.Name))
                    {
                        continue;
                    }

                    if (field.IsDefined(typeof(NonSerializedAttribute), false) ||
                        field.IsDefined(typeof(HideInInspector), false))
                    {
                        continue;
                    }

                    if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), false))
                    {
                        continue;
                    }

                    yield return field;
                }
            }
        }

    }
}
