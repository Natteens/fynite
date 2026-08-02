using UnityEngine;

namespace Fynite.Samples.BasicHfsm
{
    /// <summary>
    /// Drives the sample graph from the keyboard so it can be watched running.
    /// </summary>
    /// <remarks>
    /// The three signals are referenced with <see cref="FyniteSignalReference"/> rather than by name or
    /// index: the inspector shows a dropdown of the graph's signals and stores the signal's persistent
    /// identity, so renaming Move in the graph leaves this component pointing at the same signal.
    /// Resolution to a dense handle happens on first use and is cached from then on.
    /// </remarks>
    [RequireComponent(typeof(FyniteRunner))]
    [AddComponentMenu("Fynite/Samples/Patrol Driver")]
    public sealed class PatrolDriver : MonoBehaviour
    {
        [Tooltip("Signal that moves the machine from Idle to Moving.")]
        [SerializeField]
        private FyniteSignalReference move;

        [Tooltip("Signal that moves the machine from Moving back to Idle.")]
        [SerializeField]
        private FyniteSignalReference stop;

        [Tooltip("Signal handled without a transition; carries a string payload.")]
        [SerializeField]
        private FyniteSignalReference notify;

        [Tooltip("Payload sent with the Notify signal.")]
        [SerializeField]
        private string note = "hello";

        private FyniteRunner _runner;

        private void Awake()
        {
            _runner = GetComponent<FyniteRunner>();
        }

        private void Update()
        {
            if (_runner == null || !_runner.IsRunning)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                _runner.Raise(move);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                _runner.Raise(stop);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                _runner.Raise(notify, note);
            }
        }
    }
}
