using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace Fynite.Tests
{
    /// <summary>
    /// A <see cref="GraphLogger"/> the tests can inspect.
    /// </summary>
    /// <remarks>
    /// Graph Toolkit hands the graph a logger backed by the window's error panel. A bare
    /// <c>GraphLogger</c> has no sink and throws on first use, so tests supply one of these to see what
    /// the graph reported and about which node.
    /// </remarks>
    internal sealed class RecordingGraphLogger : IErrorsAndWarnings
    {
        internal readonly List<(string Message, object Context)> Errors = new List<(string, object)>();
        internal readonly List<(string Message, object Context)> Warnings = new List<(string, object)>();

        /// <summary>A logger writing into a fresh recorder.</summary>
        /// <remarks>
        /// The sink property is not public, so it is assigned reflectively. That is fine here: this is
        /// test scaffolding standing in for the sink the graph window would supply.
        /// </remarks>
        internal static GraphLogger Create(out RecordingGraphLogger recorder)
        {
            recorder = new RecordingGraphLogger();
            var logger = new GraphLogger();

            var sink = typeof(GraphLogger).GetProperty(
                "errorsAndWarnings",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            if (sink == null || !sink.CanWrite)
            {
                throw new System.InvalidOperationException(
                    "GraphLogger no longer exposes a writable sink; the graph validation tests need one.");
            }

            sink.SetValue(logger, recorder);
            return logger;
        }

        public void LogError(object message, object context = null) =>
            Errors.Add((message?.ToString(), context));

        public void LogWarning(object message, object context = null) =>
            Warnings.Add((message?.ToString(), context));

        public void Log(object message, object context = null)
        {
        }
    }
}
