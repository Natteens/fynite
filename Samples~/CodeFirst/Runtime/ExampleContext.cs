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

        /// <summary>Published by the activity of <see cref="ActionState"/> when it reaches its end.</summary>
        public FyniteEvent ActionFinished { get; } = new FyniteEvent();

        public void Move(float deltaTime)
            => body.Translate(Input.Move.normalized * (speed * deltaTime), Space.World);

        public void Fall(float deltaTime)
            => body.Translate(Vector3.down * (fallSpeed * deltaTime), Space.World);

        public void BeginAction() => Debug.Log("Fynite sample: action started.");

        public void MarkActionPoint() => Debug.Log("Fynite sample: action point reached.");

        public void CancelAction() => Debug.Log("Fynite sample: action cleaned up.");
    }
}
