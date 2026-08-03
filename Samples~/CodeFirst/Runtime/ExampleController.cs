using Fynite;
using UnityEngine;

namespace FyniteSamples.CodeFirst
{
    public sealed class ExampleController : MonoBehaviour
    {
        [SerializeField] private ExampleInput input;
        [SerializeField] private float speed = 3f;

        private FyniteMachine<ExampleContext> machine;

        private void Awake()
        {
            var context = new ExampleContext(input, transform, speed);

            machine = Machine
                .Attach(this, context)
                .Start<IdleState>()
                .Use<LocomotionTransitions>()
                .Build();
        }
    }
}
