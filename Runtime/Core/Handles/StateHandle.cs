using System;

namespace Fynite
{
    /// <summary>
    /// Opaque, immutable identity of a state inside a single <see cref="FyniteDefinition{TContext}"/>.
    /// </summary>
    /// <remarks>
    /// A handle carries a dense zero-based index plus the identity of the builder that produced it.
    /// The owner tag lets the runtime reject handles that belong to a different definition instead of
    /// silently resolving to an unrelated state that happens to share the same index.
    /// State names are diagnostic labels only; nothing is ever looked up by name at runtime.
    /// </remarks>
    public readonly struct StateHandle : IEquatable<StateHandle>
    {
        private readonly int _owner;
        private readonly int _index;

        internal StateHandle(int owner, int index)
        {
            _owner = owner;
            _index = index;
        }

        /// <summary>The explicit invalid handle. Equivalent to <c>default</c>.</summary>
        public static StateHandle Invalid => default;

        /// <summary>True when this handle refers to a state of some definition.</summary>
        public bool IsValid => _owner != 0;

        /// <summary>Dense zero-based index of the state within its owning definition.</summary>
        public int Index => _index;

        internal int Owner => _owner;

        public bool Equals(StateHandle other) => _owner == other._owner && _index == other._index;

        public override bool Equals(object obj) => obj is StateHandle other && Equals(other);

        public override int GetHashCode() => unchecked((_owner * 397) ^ _index);

        public static bool operator ==(StateHandle left, StateHandle right) => left.Equals(right);

        public static bool operator !=(StateHandle left, StateHandle right) => !left.Equals(right);

        public override string ToString() =>
            IsValid ? "StateHandle(index: " + _index + ", definition: " + _owner + ")" : "StateHandle(invalid)";
    }
}
