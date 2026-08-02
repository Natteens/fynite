using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fynite.Authoring
{
    /// <summary>What slot a block can fill.</summary>
    public enum FyniteBlockKind
    {
        /// <summary>Usable as an enter, tick, fixed-tick or exit block, and as a reaction effect.</summary>
        Action = 0,

        /// <summary>Usable only as a reaction guard.</summary>
        Guard = 1
    }

    /// <summary>One block type the editor can offer, with everything the pickers need.</summary>
    public sealed class FyniteBlockTypeInfo
    {
        internal FyniteBlockTypeInfo(Type type, FyniteBlockKind kind, Type[] contextContracts)
        {
            Type = type;
            Kind = kind;
            ContextContracts = contextContracts;
            Reference = FyniteTypeReference.From(type);
        }

        /// <summary>The concrete block class.</summary>
        public Type Type { get; }

        /// <summary>Whether it is an action or a guard.</summary>
        public FyniteBlockKind Kind { get; }

        /// <summary>
        /// Context contracts the block declares, for example <c>PlayerContext</c> or <c>IMovable</c>.
        /// </summary>
        /// <remarks>
        /// A block may declare several. It is usable by a graph whose context is assignable to any one
        /// of them, because the block interfaces are contravariant in the context.
        /// </remarks>
        public Type[] ContextContracts { get; }

        /// <summary>How the authoring model refers to this type.</summary>
        public FyniteTypeReference Reference { get; }

        /// <summary>Short name for menus.</summary>
        public string DisplayName => Type.Name;

        /// <summary>Namespace, for disambiguating two blocks with the same short name.</summary>
        public string Namespace => Type.Namespace ?? string.Empty;

        /// <summary>Name plus namespace, for the search list.</summary>
        public string SearchLabel => string.IsNullOrEmpty(Namespace) ? DisplayName : DisplayName + "  (" + Namespace + ")";

        /// <summary>True when a graph with this context type may use this block.</summary>
        public bool SupportsContext(Type contextType)
        {
            if (contextType == null)
            {
                return false;
            }

            for (int i = 0; i < ContextContracts.Length; i++)
            {
                if (ContextContracts[i].IsAssignableFrom(contextType))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Discovers the block types the editor may offer for a given context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Discovery is done with <see cref="TypeCache"/> rather than a hand-maintained list or a switch, so
    /// adding a block is nothing more than writing the class. The catalogue is editor-only; the compiled
    /// asset carries instances, so nothing is ever looked up by name while the game runs.
    /// </para>
    /// <para>
    /// A type is only offered when it can actually be used: abstract types, interfaces, open generics
    /// and types without a parameterless constructor are dropped here rather than accepted and then
    /// failing at compile time.
    /// </para>
    /// </remarks>
    public static class FyniteBlockCatalog
    {
        private static List<FyniteBlockTypeInfo> _all;

        /// <summary>Every usable block type in the project.</summary>
        public static IReadOnlyList<FyniteBlockTypeInfo> All
        {
            get
            {
                EnsureIndex();
                return _all;
            }
        }

        /// <summary>Block types of a given kind that are compatible with a context type.</summary>
        /// <param name="kind">Whether an action or a guard is wanted.</param>
        /// <param name="contextType">The graph's context type, or null to get nothing.</param>
        public static List<FyniteBlockTypeInfo> ForContext(FyniteBlockKind kind, Type contextType)
        {
            var results = new List<FyniteBlockTypeInfo>();
            if (contextType == null)
            {
                return results;
            }

            EnsureIndex();
            for (int i = 0; i < _all.Count; i++)
            {
                var info = _all[i];
                if (info.Kind == kind && info.SupportsContext(contextType))
                {
                    results.Add(info);
                }
            }

            results.Sort(CompareForDisplay);
            return results;
        }

        /// <summary>Looks up a discovered block type, or null when it is not usable.</summary>
        public static FyniteBlockTypeInfo Find(Type type)
        {
            if (type == null)
            {
                return null;
            }

            EnsureIndex();
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Type == type)
                {
                    return _all[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Describes a type that is not in the catalogue, so callers can say precisely why.
        /// </summary>
        /// <returns>A reason, or null when the type is in fact usable.</returns>
        public static string DescribeRejection(Type type)
        {
            if (type == null)
            {
                return "the type no longer exists";
            }

            if (type.IsInterface)
            {
                return "'" + type.FullName + "' is an interface";
            }

            if (type.IsAbstract)
            {
                return "'" + type.FullName + "' is abstract";
            }

            if (type.ContainsGenericParameters)
            {
                return "'" + type.FullName + "' is an open generic type";
            }

            if (!typeof(FyniteBlock).IsAssignableFrom(type))
            {
                return "'" + type.FullName + "' does not derive from FyniteAction<> or FyniteGuard<>";
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                return "'" + type.FullName + "' has no parameterless constructor";
            }

            if (CollectContracts(type, typeof(IFyniteAction<>)).Length == 0 &&
                CollectContracts(type, typeof(IFyniteGuard<>)).Length == 0)
            {
                return "'" + type.FullName + "' implements neither IFyniteAction<> nor IFyniteGuard<>";
            }

            return null;
        }

        /// <summary>Drops the cached catalogue. Called when assemblies change.</summary>
        public static void Invalidate() => _all = null;

        private static int CompareForDisplay(FyniteBlockTypeInfo a, FyniteBlockTypeInfo b)
        {
            int byName = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.Compare(a.Namespace, b.Namespace, StringComparison.Ordinal);
        }

        private static void EnsureIndex()
        {
            if (_all != null)
            {
                return;
            }

            var all = new List<FyniteBlockTypeInfo>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<FyniteBlock>())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                var actionContracts = CollectContracts(type, typeof(IFyniteAction<>));
                if (actionContracts.Length > 0)
                {
                    all.Add(new FyniteBlockTypeInfo(type, FyniteBlockKind.Action, actionContracts));
                }

                var guardContracts = CollectContracts(type, typeof(IFyniteGuard<>));
                if (guardContracts.Length > 0)
                {
                    all.Add(new FyniteBlockTypeInfo(type, FyniteBlockKind.Guard, guardContracts));
                }
            }

            all.Sort(CompareForDisplay);
            _all = all;
        }

        private static Type[] CollectContracts(Type type, Type openInterface)
        {
            var contracts = new List<Type>();
            var interfaces = type.GetInterfaces();

            for (int i = 0; i < interfaces.Length; i++)
            {
                var candidate = interfaces[i];
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == openInterface)
                {
                    contracts.Add(candidate.GetGenericArguments()[0]);
                }
            }

            return contracts.ToArray();
        }

        [InitializeOnLoadMethod]
        private static void ResetOnLoad() => Invalidate();
    }

    /// <summary>
    /// Discovers the types a graph may use as its context.
    /// </summary>
    /// <remarks>
    /// The list is restricted to what a <see cref="FyniteRunner"/> can actually be handed: the runner's
    /// context field is a <see cref="MonoBehaviour"/>, so a context type has to be one, or an interface
    /// a <see cref="MonoBehaviour"/> could implement. Offering anything else would let the user pick a
    /// type that can never be assigned in the inspector.
    /// </remarks>
    public static class FyniteContextCatalog
    {
        private static List<Type> _all;

        /// <summary>Every type a graph may declare as its context, sorted for display.</summary>
        public static IReadOnlyList<Type> All
        {
            get
            {
                EnsureIndex();
                return _all;
            }
        }

        /// <summary>True when a type can be a Fynite context.</summary>
        public static bool IsValidContextType(Type type)
        {
            if (type == null || type.IsValueType || type.ContainsGenericParameters)
            {
                return false;
            }

            if (type.IsInterface)
            {
                return true;
            }

            return typeof(MonoBehaviour).IsAssignableFrom(type) && !type.IsAbstract;
        }

        /// <summary>Says why a type cannot be a context, or null when it can.</summary>
        public static string DescribeRejection(Type type)
        {
            if (type == null)
            {
                return "the type no longer exists";
            }

            if (type.IsValueType)
            {
                return "'" + type.FullName + "' is a value type; a context must be a reference type";
            }

            if (type.ContainsGenericParameters)
            {
                return "'" + type.FullName + "' is an open generic type";
            }

            if (type.IsInterface)
            {
                return null;
            }

            if (type.IsAbstract)
            {
                return "'" + type.FullName + "' is abstract, so no component of that exact type can be assigned";
            }

            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                return "'" + type.FullName + "' is not a MonoBehaviour and not an interface, so a FyniteRunner " +
                       "could never be given one";
            }

            return null;
        }

        /// <summary>Drops the cached list.</summary>
        public static void Invalidate() => _all = null;

        private static void EnsureIndex()
        {
            if (_all != null)
            {
                return;
            }

            var all = new List<Type>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (IsValidContextType(type))
                {
                    all.Add(type);
                }
            }

            // Interfaces are worth offering too: a block written against a narrow contract works for
            // every component implementing it, which is the whole point of the contravariant blocks.
            foreach (var info in FyniteBlockCatalog.All)
            {
                for (int i = 0; i < info.ContextContracts.Length; i++)
                {
                    var contract = info.ContextContracts[i];
                    if (contract.IsInterface && !all.Contains(contract))
                    {
                        all.Add(contract);
                    }
                }
            }

            all.Sort((a, b) =>
            {
                int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : string.Compare(a.FullName, b.FullName, StringComparison.Ordinal);
            });

            _all = all;
        }

        [InitializeOnLoadMethod]
        private static void ResetOnLoad() => Invalidate();
    }
}
