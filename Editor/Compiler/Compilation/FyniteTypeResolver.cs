using System;
using System.Collections.Generic;
using UnityEditor;

namespace Fynite.Authoring
{
    /// <summary>
    /// Turns authoring type references back into <see cref="Type"/> objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is editor-only on purpose. Resolving a type from a string is exactly what the runtime must
    /// never do, so it happens here, once, during compilation — and what the compiler hands to the
    /// runtime is an instance, not a name.
    /// </para>
    /// <para>
    /// The cache is rebuilt from scratch on every domain reload because it is a plain static field: the
    /// domain that owned it is gone, so there is nothing stale to carry over.
    /// </para>
    /// </remarks>
    public static class FyniteTypeResolver
    {
        private static Dictionary<string, Type> _byIdentity;
        private static Dictionary<string, List<Type>> _byFullName;

        /// <summary>
        /// Resolves a reference to the type it names.
        /// </summary>
        /// <param name="reference">The stored reference.</param>
        /// <param name="type">The resolved type, or null.</param>
        /// <param name="ambiguous">
        /// True when the assembly no longer matches and more than one type carries that full name, so
        /// picking one would be a guess.
        /// </param>
        /// <returns>True when exactly one type matched.</returns>
        public static bool TryResolve(FyniteTypeReference reference, out Type type, out bool ambiguous)
        {
            type = null;
            ambiguous = false;

            var identity = reference.Identity;
            if (identity == null)
            {
                return false;
            }

            EnsureIndex();

            if (_byIdentity.TryGetValue(identity, out type))
            {
                return true;
            }

            // The assembly was renamed or the type moved between assemblies. Falling back to the full
            // name recovers the common case without silently accepting a different type when the name
            // is not unique.
            var fullName = reference.FullName;
            if (fullName != null && _byFullName.TryGetValue(fullName, out var candidates))
            {
                if (candidates.Count == 1)
                {
                    type = candidates[0];
                    return true;
                }

                ambiguous = candidates.Count > 1;
            }

            if (!ambiguous)
            {
                // Scanning assemblies only yields declared types, so a constructed generic such as
                // FyniteAction<PlayerContext> is never in the index. Asking the runtime resolves those.
                // This is editor-only code; the runtime never resolves a type from a string.
                type = Type.GetType(identity, false);
                if (type != null)
                {
                    return true;
                }
            }

            type = null;
            return false;
        }

        /// <summary>Resolves a reference, returning null when it cannot be resolved.</summary>
        public static Type Resolve(FyniteTypeReference reference) =>
            TryResolve(reference, out var type, out _) ? type : null;

        /// <summary>Drops the cached type index. Called when assemblies change.</summary>
        public static void Invalidate()
        {
            _byIdentity = null;
            _byFullName = null;
        }

        private static void EnsureIndex()
        {
            if (_byIdentity != null)
            {
                return;
            }

            var byIdentity = new Dictionary<string, Type>(StringComparer.Ordinal);
            var byFullName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException loadException)
                {
                    // A partially loadable assembly still contributes the types that did load.
                    types = Array.FindAll(loadException.Types, t => t != null);
                }
                catch (Exception)
                {
                    continue;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    var type = types[i];
                    if (type.FullName == null)
                    {
                        continue;
                    }

                    byIdentity[FyniteTypeReference.Format(type)] = type;

                    if (!byFullName.TryGetValue(type.FullName, out var list))
                    {
                        list = new List<Type>(1);
                        byFullName[type.FullName] = list;
                    }

                    list.Add(type);
                }
            }

            _byIdentity = byIdentity;
            _byFullName = byFullName;
        }

        [InitializeOnLoadMethod]
        private static void ResetOnLoad() => Invalidate();
    }
}
