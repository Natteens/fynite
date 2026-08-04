namespace Fynite
{
    /// <summary>
    /// What a <see cref="FyniteEvent"/> notifies. Keeps the event itself unaware of machines, states
    /// and contexts: it only hands back the slot the subscriber asked for.
    /// </summary>
    internal interface IFyniteEventSink
    {
        void Signal(int slot);
    }
}
