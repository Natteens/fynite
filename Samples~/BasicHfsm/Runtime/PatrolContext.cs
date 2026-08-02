using System.Collections.Generic;
using UnityEngine;

namespace Fynite.Samples.BasicHfsm
{
    /// <summary>
    /// The component the sample graph's blocks operate on.
    /// </summary>
    /// <remarks>
    /// A context is just a component holding whatever the blocks need. It is handed to the machine
    /// explicitly by the <see cref="FyniteRunner"/> on the same object — nothing is discovered by
    /// searching the scene, and nothing about the machine leaks into it.
    /// </remarks>
    [AddComponentMenu("Fynite/Samples/Patrol Context")]
    public sealed class PatrolContext : MonoBehaviour
    {
        [Tooltip("Set false to make the guard on Idle → Moving reject the transition.")]
        public bool canMove = true;

        [Tooltip("Accumulated by the Tick action while Moving is active.")]
        public float distance;

        [Tooltip("Counted by the FixedTick action while Moving is active.")]
        public int fixedSteps;

        /// <summary>Everything the sample's blocks recorded, newest last.</summary>
        public readonly List<string> trace = new List<string>();

        /// <summary>Payload of the last Notify signal that reached the machine.</summary>
        public string lastNote;

        /// <summary>Appends a line to the trace, which the sample component prints.</summary>
        public void Record(string line) => trace.Add(line);
    }
}
