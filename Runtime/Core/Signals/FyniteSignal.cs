using System;

namespace Fynite
{
    /// <summary>
    /// A queued signal occurrence: the signal identity plus its optional payload.
    /// </summary>
    /// <remarks>
    /// Payloads are stored as <see cref="object"/>, so value-type payloads are boxed once when raised.
    /// This is deliberate for the first version: prefer payload-less signals for high-frequency events
    /// whose data already lives in the context, and reserve payloads for events that truly carry data.
    /// </remarks>
    public readonly struct FyniteSignal
    {
        private readonly SignalHandle _handle;
        private readonly object _payload;

        internal FyniteSignal(SignalHandle handle, object payload)
        {
            _handle = handle;
            _payload = payload;
        }

        /// <summary>Identity of the raised signal.</summary>
        public SignalHandle Handle => _handle;

        /// <summary>The payload, or <c>null</c> when the signal carries none.</summary>
        public object Payload => _payload;

        /// <summary>True when a payload was supplied.</summary>
        public bool HasPayload => _payload != null;

        /// <summary>True when this struct refers to an actual signal rather than the default value.</summary>
        public bool IsValid => _handle.IsValid;

        /// <summary>Reads the payload when it is assignable to <typeparamref name="T"/>.</summary>
        public bool TryGetPayload<T>(out T value)
        {
            if (_payload is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Reads the payload, throwing when it is absent or of an incompatible type.</summary>
        /// <exception cref="InvalidOperationException">No payload, or the payload is not a <typeparamref name="T"/>.</exception>
        public T GetPayload<T>()
        {
            if (_payload == null)
            {
                throw new InvalidOperationException(
                    "Signal " + _handle + " carries no payload; requested type was '" + typeof(T).FullName + "'.");
            }

            if (_payload is T typed)
            {
                return typed;
            }

            throw new InvalidOperationException(
                "Signal " + _handle + " carries a payload of type '" + _payload.GetType().FullName +
                "' which is not assignable to '" + typeof(T).FullName + "'.");
        }

        public override string ToString() =>
            HasPayload ? _handle + " payload: " + _payload.GetType().Name : _handle.ToString();
    }
}
