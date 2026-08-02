namespace Fynite
{
    /// <summary>
    /// The only machine capability exposed to blocks: raising signals.
    /// </summary>
    /// <remarks>
    /// Blocks receive this instead of the machine itself so they cannot mutate the active path,
    /// start or stop the machine, or otherwise re-enter the executor. Signals raised from inside a
    /// block are always queued and processed once the current operation has completed.
    /// </remarks>
    public interface IFyniteSignalSink
    {
        /// <summary>Queues a signal that was declared without a payload.</summary>
        void Raise(SignalHandle signal);

        /// <summary>Queues a signal together with a payload; the payload type is validated.</summary>
        void Raise(SignalHandle signal, object payload);

        /// <summary>Queues a typed signal together with its payload.</summary>
        void Raise<TPayload>(SignalHandle<TPayload> signal, TPayload payload);
    }
}
