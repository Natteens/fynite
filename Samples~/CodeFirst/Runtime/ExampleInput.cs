using Fynite;
using UnityEngine;

namespace FyniteSamples.CodeFirst
{
    public sealed class ExampleInput : MonoBehaviour
    {
        [SerializeField] private Vector2 move;

        public Vector2 Move
        {
            get => move;
            set => move = value;
        }

        public bool HasMovement => move.sqrMagnitude > 0.0001f;
    }
}
