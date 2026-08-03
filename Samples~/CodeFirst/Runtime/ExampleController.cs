using Fynite;
using UnityEngine;

namespace FyniteSamples.CodeFirst
{
    public sealed class ExampleController : MonoBehaviour
    {
        [SerializeField] private ExampleInput input;
        [SerializeField] private float speed = 3f;
        [SerializeField] private float fallSpeed = 9f;

        private FyniteMachine<ExampleContext> machine;

        private void Awake()
        {
            var context = new ExampleContext(input, transform, speed, fallSpeed);

            machine = Machine
                .Attach(this, context)
                .Start<GroundedState>()
                .Child<GroundedState, IdleState>()
                .Child<GroundedState, WalkState>()
                .Use<LocomotionTransitions>()
                .Use<AirTransitions>()
                .Build();
        }
    }
}
