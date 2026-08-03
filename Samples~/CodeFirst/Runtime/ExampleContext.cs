using Fynite;
using UnityEngine;

namespace FyniteSamples.CodeFirst
{
    public sealed class ExampleContext
    {
        private readonly Transform body;
        private readonly float speed;

        public ExampleContext(ExampleInput input, Transform body, float speed)
        {
            Input = input;
            this.body = body;
            this.speed = speed;
        }

        public ExampleInput Input { get; }

        public void Move(float deltaTime)
            => body.Translate(Input.Move.normalized * (speed * deltaTime), Space.World);
    }
}
