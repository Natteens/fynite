using Fynite;
using UnityEngine;

namespace FyniteSamples.CodeFirst
{
    public sealed class ExampleInput : MonoBehaviour
    {
        [SerializeField] private Vector2 move;
        [SerializeField] private bool grounded = true;

        public Vector2 Move
        {
            get => move;
            set => move = value;
        }

        public bool IsGrounded
        {
            get => grounded;
            set => grounded = value;
        }

        public bool HasMovement => move.sqrMagnitude > 0.0001f;
    }
}
