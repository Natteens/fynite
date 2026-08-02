using System.Collections.Generic;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// Context the compiled-graph tests bind machines to.
    /// </summary>
    /// <remarks>
    /// It lives in a runtime test assembly because Unity refuses to attach a component whose type comes
    /// from an editor-only assembly, and the vertical tests need a real component on a real GameObject.
    /// </remarks>
    public sealed class GraphTestContext : MonoBehaviour
    {
        public readonly List<string> Trace = new List<string>();

        public bool Allow = true;

        public int Ticks;

        public int FixedTicks;

        public string LastPayload;

        public void Record(string entry) => Trace.Add(entry);

        public string TraceText => string.Join(",", Trace);
    }

    /// <summary>A second context, used to prove that incompatible blocks are rejected.</summary>
    public sealed class UnrelatedGraphContext : MonoBehaviour
    {
    }
}
