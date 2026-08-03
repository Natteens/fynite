using Fynite;
using UnityEngine;

namespace FyniteSamples.CodeFirst
{
    public sealed class ExampleContext
    {
        private readonly Transform body;
        private readonly float speed;
        private readonly float fallSpeed;

        public ExampleContext(ExampleInput input, Transform body, float speed, float fallSpeed)
        {
            Input = input;
            this.body = body;
            this.speed = speed;
            this.fallSpeed = fallSpeed;
        }

        public ExampleInput Input { get; }

        public void Move(float deltaTime)
            => body.Translate(Input.Move.normalized * (speed * deltaTime), Space.World);

        public void Fall(float deltaTime)
            => body.Translate(Vector3.down * (fallSpeed * deltaTime), Space.World);
    }
}
