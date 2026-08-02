using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fynite.Authoring
{
    /// <summary>
    /// Turns a block occurrence into the configured prototype the runtime asset stores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is where the reflective work happens, and it happens exactly once per occurrence, in the
    /// editor. What leaves this class is a real object of the authored type carrying the authored
    /// configuration. The runtime never sees the type name and never constructs anything reflectively:
    /// it clones the prototype.
    /// </para>
    /// <para>
    /// Configuration is applied with <see cref="JsonUtility.FromJsonOverwrite"/>, which is deliberately
    /// forgiving. When the block's fields changed since the graph was authored, values that still fit
    /// are carried over and the rest fall back to the type's defaults, rather than the whole occurrence
    /// being lost.
    /// </para>
    /// </remarks>
    public static class FyniteBlockFactory
    {
        /// <summary>
        /// Builds the prototype for one occurrence, or returns null after recording why it could not.
        /// </summary>
        /// <param name="occurrence">The authored occurrence.</param>
        /// <param name="expectedKind">Whether this slot needs an action or a guard.</param>
        /// <param name="contextType">The graph's context type.</param>
        /// <param name="diagnostics">Where problems are recorded.</param>
        public static FyniteBlock CreatePrototype(
            FyniteBlockDocument occurrence,
            FyniteBlockKind expectedKind,
            Type contextType,
            List<FyniteDiagnostic> diagnostics)
        {
            if (!occurrence.blockType.IsSet)
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    occurrence,
                    FyniteDiagnosticCodes.BlockTypeMissing,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Block,
                    "This " + Describe(expectedKind) + " slot has no type selected."));
                return null;
            }

            if (!FyniteTypeResolver.TryResolve(occurrence.blockType, out var type, out bool ambiguous))
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    occurrence,
                    FyniteDiagnosticCodes.BlockTypeNotFound,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Block,
                    ambiguous
                        ? "The " + Describe(expectedKind) + " type '" + occurrence.blockType.FullName +
                          "' exists in more than one assembly and the one it was saved with is gone, so it " +
                          "cannot be resolved without guessing. Select the type again."
                        : "The " + Describe(expectedKind) + " type '" + occurrence.blockType +
                          "' no longer exists. It was renamed, moved or deleted; select the type again " +
                          "rather than letting a different block take its place."));
                return null;
            }

            var rejection = FyniteBlockCatalog.DescribeRejection(type);
            if (rejection != null)
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    occurrence,
                    FyniteDiagnosticCodes.BlockTypeNotConstructible,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Block,
                    "This " + Describe(expectedKind) + " cannot be instantiated because " + rejection + "."));
                return null;
            }

            var info = FindMatchingKind(type, expectedKind);
            if (info == null)
            {
                var other = expectedKind == FyniteBlockKind.Guard ? FyniteBlockKind.Action : FyniteBlockKind.Guard;
                bool isOther = FindMatchingKind(type, other) != null;

                diagnostics.Add(FyniteDiagnostic.For(
                    occurrence,
                    FyniteDiagnosticCodes.BlockWrongKind,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Block,
                    isOther
                        ? "'" + type.Name + "' is " + Article(other) + " " + Describe(other) + " and cannot be used as " +
                          Article(expectedKind) + " " + Describe(expectedKind) + "."
                        : "'" + type.Name + "' is not " + Article(expectedKind) + " " + Describe(expectedKind) + "."));
                return null;
            }

            if (contextType != null && !info.SupportsContext(contextType))
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    occurrence,
                    FyniteDiagnosticCodes.BlockContextIncompatible,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Block,
                    "'" + type.Name + "' requires a context of type " + DescribeContracts(info) +
                    ", but this graph's context is '" + contextType.FullName + "'." +
                    SuggestAlternatives(expectedKind, contextType)));
                return null;
            }

            FyniteBlock prototype;
            try
            {
                // Editor-side construction. The instance, not the type name, is what gets serialized.
                prototype = (FyniteBlock)Activator.CreateInstance(type);
            }
            catch (Exception exception)
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    occurrence,
                    FyniteDiagnosticCodes.BlockTypeNotConstructible,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Block,
                    "'" + type.FullName + "' could not be constructed: " + exception.Message));
                return null;
            }

            ApplyConfiguration(occurrence, prototype, diagnostics);
            return prototype;
        }

        /// <summary>
        /// Serializes a configured block back into the form an occurrence stores.
        /// </summary>
        public static string SerializeConfiguration(FyniteBlock block) =>
            block == null ? null : JsonUtility.ToJson(block);

        /// <summary>
        /// Creates a default-configured instance of a block type, for the editor to fill a new occurrence.
        /// </summary>
        public static FyniteBlock CreateDefault(Type type)
        {
            if (type == null || FyniteBlockCatalog.DescribeRejection(type) != null)
            {
                return null;
            }

            try
            {
                return (FyniteBlock)Activator.CreateInstance(type);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ApplyConfiguration(
            FyniteBlockDocument occurrence,
            FyniteBlock prototype,
            List<FyniteDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(occurrence.configJson))
            {
                return;
            }

            try
            {
                JsonUtility.FromJsonOverwrite(occurrence.configJson, prototype);
            }
            catch (Exception exception)
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    occurrence,
                    FyniteDiagnosticCodes.BlockConfigInvalid,
                    FyniteDiagnosticSeverity.Warning,
                    FyniteDiagnosticCategory.Block,
                    "The stored configuration of '" + prototype.GetType().Name + "' could not be applied and " +
                    "its defaults were used instead: " + exception.Message));
            }
        }

        private static FyniteBlockTypeInfo FindMatchingKind(Type type, FyniteBlockKind kind)
        {
            var all = FyniteBlockCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Type == type && all[i].Kind == kind)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static string DescribeContracts(FyniteBlockTypeInfo info)
        {
            if (info.ContextContracts.Length == 1)
            {
                return "'" + info.ContextContracts[0].FullName + "'";
            }

            var text = new System.Text.StringBuilder();
            for (int i = 0; i < info.ContextContracts.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(i == info.ContextContracts.Length - 1 ? " or " : ", ");
                }

                text.Append('\'').Append(info.ContextContracts[i].FullName).Append('\'');
            }

            return text.ToString();
        }

        /// <summary>
        /// Names the blocks that would fit, so a rejection says what to do instead of only what is wrong.
        /// </summary>
        private static string SuggestAlternatives(FyniteBlockKind kind, Type contextType)
        {
            var compatible = FyniteBlockCatalog.ForContext(kind, contextType);
            if (compatible.Count == 0)
            {
                return " No " + Describe(kind) + " in this project is compatible with that context yet.";
            }

            var text = new System.Text.StringBuilder(" Compatible ");
            text.Append(Describe(kind)).Append("s here: ");

            int shown = Math.Min(compatible.Count, 8);
            for (int i = 0; i < shown; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append(compatible[i].DisplayName);
            }

            if (compatible.Count > shown)
            {
                text.Append(" and ").Append(compatible.Count - shown).Append(" more");
            }

            return text.Append('.').ToString();
        }

        private static string Describe(FyniteBlockKind kind) => kind == FyniteBlockKind.Guard ? "guard" : "action";

        private static string Article(FyniteBlockKind kind) => kind == FyniteBlockKind.Guard ? "a" : "an";
    }
}
