using System;

namespace Fynite
{
    /// <summary>
    /// Opaque, immutable identity of a signal inside a single <see cref="FyniteDefinition{TContext}"/>.
    /// </summary>
    /// <remarks>
    /// Like <see cref="StateHandle"/>, the handle carries a dense index plus an owner tag so that
    /// handles coming from another definition are rejected rather than misinterpreted.
    /// </remarks>
    public readonly struct SignalHandle : IEquatable<SignalHandle>
    {
        private readonly int _owner;
        private readonly int _index;

        internal SignalHandle(int owner, int index)
        {
            _owner = owner;
            _index = index;
        }

        /// <summary>The explicit invalid handle. Equivalent to <c>default</c>.</summary>
        public static SignalHandle Invalid => default;

        /// <summary>True when this handle refers to a signal of some definition.</summary>
        public bool IsValid => _owner != 0;

        /// <summary>Dense zero-based index of the signal within its owning definition.</summary>
        public int Index => _index;

        internal int Owner => _owner;

        public bool Equals(SignalHandle other) => _owner == other._owner && _index == other._index;

        public override bool Equals(object obj) => obj is SignalHandle other && Equals(other);

        public override int GetHashCode() => unchecked((_owner * 397) ^ _index);

        public static bool operator ==(SignalHandle left, SignalHandle right) => left.Equals(right);

        public static bool operator !=(SignalHandle left, SignalHandle right) => !left.Equals(right);

        public override string ToString() =>
            IsValid ? "SignalHandle(index: " + _index + ", definition: " + _owner + ")" : "SignalHandle(invalid)";
    }

    /// <summary>
    /// A <see cref="SignalHandle"/> that additionally carries, at compile time, the payload type the
    /// signal was declared with. Converts implicitly to the untyped handle.
    /// </summary>
    /// <typeparam name="TPayload">Payload type declared for this signal.</typeparam>
    public readonly struct SignalHandle<TPayload> : IEquatable<SignalHandle<TPayload>>
    {
        private readonly SignalHandle _handle;

        internal SignalHandle(SignalHandle handle)
        {
            _handle = handle;
        }

        /// <summary>The underlying untyped handle.</summary>
        public SignalHandle Handle => _handle;

        /// <summary>True when this handle refers to a signal of some definition.</summary>
        public bool IsValid => _handle.IsValid;

        /// <summary>Dense zero-based index of the signal within its owning definition.</summary>
        public int Index => _handle.Index;

        public static implicit operator SignalHandle(SignalHandle<TPayload> signal) => signal._handle;

        public bool Equals(SignalHandle<TPayload> other) => _handle.Equals(other._handle);

        public override bool Equals(object obj) => obj is SignalHandle<TPayload> other && Equals(other);

        public override int GetHashCode() => _handle.GetHashCode();

        public static bool operator ==(SignalHandle<TPayload> left, SignalHandle<TPayload> right) => left.Equals(right);

        public static bool operator !=(SignalHandle<TPayload> left, SignalHandle<TPayload> right) => !left.Equals(right);

        public override string ToString() =>
            IsValid
                ? "SignalHandle<" + typeof(TPayload).Name + ">(index: " + _handle.Index + ", definition: " + _handle.Owner + ")"
                : "SignalHandle<" + typeof(TPayload).Name + ">(invalid)";
    }
}
