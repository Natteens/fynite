using System;
using System.Reflection;
using Unity.GraphToolkit.Editor;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Writes node option values, which Graph Toolkit only exposes for reading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>INodeOption</c> has <c>TryGetValue</c> and no setter: options are meant to be typed by the
    /// user in the inspector, and for ordinary editing that is exactly right. But a graph also has to be
    /// buildable in code — the sample ships a real <c>.fyn</c>, and the tests assert on graphs they
    /// construct — and there is no public path to do that.
    /// </para>
    /// <para>
    /// So this reaches the option's backing constant through the model behind it. It is deliberately the
    /// only place in Fynite that touches Graph Toolkit internals, it is confined to editor-side
    /// construction, and it reports failure instead of throwing, so a future version of Graph Toolkit
    /// that moves this can only break programmatic authoring — never a user editing a graph by hand.
    /// </para>
    /// </remarks>
    public static class FyniteNodeOptions
    {
        private const BindingFlags Members =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        /// <summary>
        /// Sets an option's value.
        /// </summary>
        /// <param name="node">The node owning the option.</param>
        /// <param name="optionName">Name the option was declared with.</param>
        /// <param name="value">The value to store.</param>
        /// <returns>False when the option does not exist or its backing store could not be reached.</returns>
        public static bool TrySet(INode node, string optionName, object value)
        {
            var option = node?.GetNodeOptionByName(optionName);
            if (option == null)
            {
                return false;
            }

            try
            {
                var portModel = option.GetType().GetProperty("PortModel", Members)?.GetValue(option);
                if (portModel == null)
                {
                    return false;
                }

                var constant = FindConstant(portModel);
                if (constant == null)
                {
                    return false;
                }

                var objectValue = constant.GetType().GetProperty("ObjectValue", Members);
                if (objectValue == null || !objectValue.CanWrite)
                {
                    return false;
                }

                objectValue.SetValue(constant, value);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Sets an option's value, throwing when that is not possible.
        /// </summary>
        /// <remarks>
        /// Used by code that is building a graph and cannot carry on meaningfully if a value did not
        /// land, such as the sample generator and the tests.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The option could not be written.</exception>
        public static void Set(INode node, string optionName, object value)
        {
            if (!TrySet(node, optionName, value))
            {
                throw new InvalidOperationException(
                    "Could not set node option '" + optionName + "' on '" + (node?.Title ?? "<null>") +
                    "'. Either the option is not declared on this node, or this version of Graph Toolkit " +
                    "stores option values somewhere Fynite does not know about.");
            }
        }

        private static object FindConstant(object portModel)
        {
            var type = portModel.GetType();

            // Named first, so the common case does not depend on scanning.
            foreach (var name in new[] { "EmbeddedValue", "Constant", "Value" })
            {
                var property = type.GetProperty(name, Members);
                var candidate = property?.GetValue(portModel);
                if (IsConstant(candidate))
                {
                    return candidate;
                }
            }

            foreach (var property in type.GetProperties(Members))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                try
                {
                    var candidate = property.GetValue(portModel);
                    if (IsConstant(candidate))
                    {
                        return candidate;
                    }
                }
                catch (Exception)
                {
                    // A property that throws on this model simply is not the one being looked for.
                }
            }

            return null;
        }

        private static bool IsConstant(object candidate)
        {
            for (var type = candidate?.GetType(); type != null; type = type.BaseType)
            {
                if (type.Name == "Constant" && type.Namespace == "Unity.GraphToolkit.Editor")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
