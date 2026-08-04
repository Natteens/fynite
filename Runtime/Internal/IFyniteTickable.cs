namespace Fynite
{
    internal interface IFyniteTickable
    {
        int LoopSlot { get; set; }

        void LoopUpdate(float deltaTime);

        void LoopFixedUpdate(float fixedDeltaTime);

        /// <summary>
        /// Ends the machine because the loop itself is resetting. Does everything <c>Dispose</c> does
        /// except touch the loop registry, which the reset has already emptied, and never throws: a
        /// machine that fails on the way out must not stop the others from being cleaned up.
        /// </summary>
        void LoopShutdown();
    }
}
