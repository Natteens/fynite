using Fynite;
using UnityEngine;

namespace FyniteSamples.CodeFirst
{
    public sealed class GroundedState : FyniteState<ExampleContext>
    {
        protected override void Enter() => Debug.Log("Fynite sample: entering Grounded.");
    }
}
