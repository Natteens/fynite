using System;
using UnityEngine;

namespace Fynite.Authoring
{
    /// <summary>
    /// How the authoring model points at a C# type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A type reference is stored as a name because there is nothing more stable to store: a type is not
    /// an asset and has no GUID. That is acceptable here because type references only ever live on the
    /// authoring side. The compiler resolves them once, in the editor, and what reaches the runtime
    /// asset is a real object of that type — never the string.
    /// </para>
    /// <para>
    /// The stored form is <c>Namespace.Type, Assembly</c>. The assembly is kept so that two types with
    /// the same full name in different assemblies stay distinguishable; resolution falls back to the
    /// full name alone when an assembly was renamed, which is common enough to be worth surviving.
    /// </para>
    /// </remarks>
    [Serializable]
    public struct FyniteTypeReference : IEquatable<FyniteTypeReference>
    {
        [Tooltip("Type identity in the form 'Namespace.Type, Assembly'.")]
        [SerializeField]
        private string typeName;

        /// <summary>Creates a reference from a stored identity string.</summary>
        public FyniteTypeReference(string identity)
        {
            typeName = identity;
        }

        /// <summary>An unset reference.</summary>
        public static FyniteTypeReference None => default;

        /// <summary>The stored identity, or null when nothing was selected.</summary>
        public string Identity => string.IsNullOrEmpty(typeName) ? null : typeName;

        /// <summary>True when a type was selected. Says nothing about whether it still exists.</summary>
        public bool IsSet => Identity != null;

        /// <summary>Creates a reference to a concrete type.</summary>
        public static FyniteTypeReference From(Type type) =>
            type == null ? None : new FyniteTypeReference(Format(type));

        /// <summary>Formats a type the way this reference stores it.</summary>
        public static string Format(Type type)
        {
            if (type == null)
            {
                return null;
            }

            return type.FullName + ", " + type.Assembly.GetName().Name;
        }

        /// <summary>The part of the identity before the comma, for display and diagnostics.</summary>
        public string FullName
        {
            get
            {
                var identity = Identity;
                if (identity == null)
                {
                    return null;
                }

                int comma = identity.IndexOf(',');
                return comma < 0 ? identity : identity.Substring(0, comma).Trim();
            }
        }

        /// <summary>The assembly name part of the identity, or null when none was stored.</summary>
        public string AssemblyName
        {
            get
            {
                var identity = Identity;
                if (identity == null)
                {
                    return null;
                }

                int comma = identity.IndexOf(',');
                return comma < 0 ? null : identity.Substring(comma + 1).Trim();
            }
        }

        /// <summary>Short name for menus and node subtitles.</summary>
        public string DisplayName
        {
            get
            {
                var full = FullName;
                if (full == null)
                {
                    return "<none>";
                }

                int dot = full.LastIndexOf('.');
                return dot < 0 ? full : full.Substring(dot + 1);
            }
        }

        /// <inheritdoc />
        public bool Equals(FyniteTypeReference other) => string.Equals(Identity, other.Identity, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is FyniteTypeReference other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Identity != null ? Identity.GetHashCode() : 0;

        /// <inheritdoc />
        public override string ToString() => Identity ?? "<none>";
    }
}
