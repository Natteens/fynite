using Fynite;
using UnityEngine;

namespace FyniteSamples.CodeFirst
{
    public sealed class ExampleInput : MonoBehaviour
    {
        [SerializeField] private Vector2 move;

        /// <summary>Published the moment a jump is asked for, not while the character is in the air.</summary>
        public FyniteEvent JumpRequested { get; } = new FyniteEvent();

        public FyniteEvent Landed { get; } = new FyniteEvent();

        public Vector2 Move
        {
            get => move;
            set => move = value;
        }

        public bool IsGrounded { get; private set; } = true;

        public bool HasMovement => move.sqrMagnitude > 0.0001f;

        /// <summary>In play mode, right click the component header to call this from the inspector.</summary>
        [ContextMenu("Request jump")]
        public void RequestJump()
        {
            IsGrounded = false;
            JumpRequested.Publish();
        }

        [ContextMenu("Land")]
        public void Land()
        {
            IsGrounded = true;
            Landed.Publish();
        }
    }
}
