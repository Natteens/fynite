using System;

namespace Fynite
{
    /// <summary>
    /// Carries a signal's declared payload type across serialization without ever resolving a type by
    /// name at runtime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A compiled graph has to remember that <c>Notify</c> carries a <c>string</c>. Persisting
    /// <c>"System.String, mscorlib"</c> and calling <see cref="Type.GetType(string)"/> when the asset
    /// loads is exactly what the runtime is not allowed to do.
    /// </para>
    /// <para>
    /// Instead the compiler stores a <see cref="FynitePayloadType{T}"/> instance. Unity's own
    /// serializer reconstructs that closed generic when the asset loads, and
    /// <see cref="FynitePayloadType{T}.Type"/> is a plain <c>typeof</c> the compiler baked in — so the
    /// type arrives through the deserializer rather than through Fynite reflecting over strings.
    /// </para>
    /// </remarks>
    [Serializable]
    public abstract class FynitePayloadType
    {
        /// <summary>The payload type this carrier stands for.</summary>
        public abstract Type Type { get; }
    }

    /// <summary>A payload carrier for one concrete payload type.</summary>
    /// <typeparam name="TPayload">The declared payload type.</typeparam>
    [Serializable]
    public sealed class FynitePayloadType<TPayload> : FynitePayloadType
    {
        /// <inheritdoc />
        public override Type Type => typeof(TPayload);
    }
}
