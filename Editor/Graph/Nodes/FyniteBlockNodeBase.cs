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
    /// Shared behaviour of every block node: choosing a block type and editing that block's configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The block type is picked as a <see cref="MonoScript"/>. Graph Toolkit renders node options with
    /// editors chosen by data type, and an object reference is the only one that comes out as a real
    /// searchable picker — a raw type name would be a text field the user has to spell correctly, which
    /// the brief rules out. Referencing the script asset also survives renaming the type, because the
    /// reference is to the asset, not to the name.
    /// </para>
    /// <para>
    /// Configuration is not a JSON blob the user edits. Once the type is known, one node option is
    /// declared per serialized field of the block, so the inspector shows real typed widgets. Graph
    /// Toolkit re-runs option definition when the node changes and drops options that no longer exist,
    /// so switching the block type reshapes the configuration instead of stranding old values.
    /// </para>
    /// </remarks>
    [Serializable]
    public abstract class FyniteBlockNodeBase : BlockNode, IFyniteIdentifiedNode
    {
        /// <summary>Name of the option holding the script that defines the block type.</summary>
        public const string BlockScriptOption = "blockScript";

        /// <summary>Prefix given to the options generated from the block's serialized fields.</summary>
        public const string ConfigOptionPrefix = "cfg_";

        [SerializeField]
        [HideInInspector]
        private string m_FyniteGuid;

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
        public Type ResolveBlockType()
        {
            var option = GetNodeOptionByName(BlockScriptOption);
            if (option == null)
            {
                return null;
            }

            MonoScript script = null;
            option.TryGetValue(out script);
            return script != null ? script.GetClass() : null;
        }

        /// <inheritdoc />
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            context.AddOption<MonoScript>(BlockScriptOption)
                .WithDisplayName(BlockKind == FyniteBlockKind.Guard ? "Guard Script" : "Action Script")
                .WithTooltip(
                    BlockKind == FyniteBlockKind.Guard
                        ? "Script declaring the FyniteGuard<> class this guard runs."
                        : "Script declaring the FyniteAction<> class this block runs.")
                .Build();

            // The type is read back straight away so the configuration fields can be declared in the
            // same pass; Graph Toolkit reconciles the option set whenever it redefines the node.
            var blockType = ResolveBlockType();
            if (blockType == null)
            {
                return;
            }

            // A default instance supplies each option's initial value. Without it every option would
            // start at default(T) and, because the configuration is read back field by field, a block's
            // own field initialisers would be silently overwritten with zeros the moment it was used.
            var defaults = FyniteBlockFactory.CreateDefault(blockType);

            foreach (var field in EnumerateConfigurableFields(blockType))
            {
                try
                {
                    var option = context.AddOption(ConfigOptionPrefix + field.Name, field.FieldType)
                        .WithDisplayName(ObjectNames.NicifyVariableName(field.Name))
                        .WithTooltip(DescribeField(field));

                    if (defaults != null)
                    {
                        var initial = field.GetValue(defaults);
                        if (initial != null)
                        {
                            option = option.WithDefaultValue(initial);
                        }
                    }

                    option.Build();
                }
                catch (Exception)
                {
                    // A field whose type Graph Toolkit cannot edit simply keeps the block's default;
                    // the compiler reports it rather than the node failing to define.
                }
            }
        }

        /// <summary>
        /// Reads the configuration the user typed into the generated options and returns it in the form
        /// an authoring occurrence stores.
        /// </summary>
        /// <param name="blockType">The resolved block type.</param>
        /// <returns>Serialized configuration, or null when there is nothing to store.</returns>
        public string ReadConfiguration(Type blockType)
        {
            var prototype = FyniteBlockFactory.CreateDefault(blockType);
            if (prototype == null)
            {
                return null;
            }

            foreach (var field in EnumerateConfigurableFields(blockType))
            {
                var option = GetNodeOptionByName(ConfigOptionPrefix + field.Name);
                if (option == null)
                {
                    continue;
                }

                if (TryReadOption(option, field.FieldType, out object value))
                {
                    try
                    {
                        field.SetValue(prototype, value);
                    }
                    catch (Exception)
                    {
                        // Leave the default in place; the compiler surfaces mismatched configuration.
                    }
                }
            }

            return FyniteBlockFactory.SerializeConfiguration(prototype);
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

        private static bool TryReadOption(INodeOption option, Type valueType, out object value)
        {
            value = null;

            // INodeOption.TryGetValue is generic and the field type is only known at run time, so the
            // call is closed reflectively. This is editor-side authoring code; nothing here reaches the
            // compiled asset, which stores a real instance instead.
            var method = typeof(INodeOption).GetMethod(nameof(INodeOption.TryGetValue));
            if (method == null)
            {
                return false;
            }

            try
            {
                var closed = method.MakeGenericMethod(valueType);
                var arguments = new object[] { null };
                bool read = (bool)closed.Invoke(option, arguments);
                value = arguments[0];
                return read;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string DescribeField(FieldInfo field)
        {
            var tooltip = field.GetCustomAttribute<TooltipAttribute>();
            return tooltip != null ? tooltip.tooltip : field.FieldType.Name;
        }
    }
}
