namespace Fynite
{
    internal interface IFyniteTickable
    {
        int LoopSlot { get; set; }

        void LoopUpdate(float deltaTime);

        void LoopFixedUpdate(float fixedDeltaTime);
    }
}
