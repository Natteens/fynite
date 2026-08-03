using Fynite;
using UnityEngine;

namespace FyniteSamples.CodeFirst
{
    public sealed class WalkState : FyniteState<ExampleContext>
    {
        protected override void Enter() => Debug.Log("Fynite sample: entering Walk.");

        protected override void Update() => Context.Move(DeltaTime);
    }
}
