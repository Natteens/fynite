using System;
using System.Collections.Generic;

namespace Fynite.Authoring
{
    /// <summary>
    /// What a compilation produced: either usable runtime data, or the reasons it could not be made.
    /// </summary>
    /// <remarks>
    /// A failed result carries no runtime data at all. That is deliberate: the importer writes whatever
    /// the result contains, so a failure that still handed back the previous graph would let a broken
    /// edit keep running silently.
    /// </remarks>
    public sealed class FyniteCompilationResult
    {
        private readonly List<FyniteDiagnostic> _diagnostics;

        internal FyniteCompilationResult(List<FyniteDiagnostic> diagnostics)
        {
            _diagnostics = diagnostics ?? new List<FyniteDiagnostic>();
        }

        /// <summary>Everything found, in the order it was found.</summary>
        public IReadOnlyList<FyniteDiagnostic> Diagnostics => _diagnostics;

        /// <summary>True when the graph compiled and the runtime data can be written.</summary>
        public bool Succeeded { get; internal set; }

        /// <summary>The dense structure, or null on failure.</summary>
        public FyniteRuntimeGraph Graph { get; internal set; }

        /// <summary>The binder carrying the static context type, or null on failure.</summary>
        public FyniteContextBinder Binder { get; internal set; }

        /// <summary>One configured prototype per block occurrence, or null on failure.</summary>
        public FyniteBlock[] Blocks { get; internal set; }

        /// <summary>Payload carrier per signal, index-aligned with the compiled signals.</summary>
        public FynitePayloadType[] Payloads { get; internal set; }

        /// <summary>Context type the graph compiled against, when it could be resolved.</summary>
        public Type ContextType { get; internal set; }

        /// <summary>Schema version of the document that was compiled.</summary>
        public int SchemaVersion { get; internal set; }

        /// <summary>True when at least one diagnostic is an error.</summary>
        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < _diagnostics.Count; i++)
                {
                    if (_diagnostics[i].IsError)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Number of errors.</summary>
        public int ErrorCount => Count(FyniteDiagnosticSeverity.Error);

        /// <summary>Number of warnings.</summary>
        public int WarningCount => Count(FyniteDiagnosticSeverity.Warning);

        /// <summary>A one-line summary suitable for an asset's stored error message.</summary>
        public string DescribeErrors()
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < _diagnostics.Count; i++)
            {
                if (!_diagnostics[i].IsError)
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.Append(" | ");
                }

                text.Append(_diagnostics[i].ToString());
            }

            return text.Length == 0 ? "The graph could not be compiled." : text.ToString();
        }

        private int Count(FyniteDiagnosticSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < _diagnostics.Count; i++)
            {
                if (_diagnostics[i].Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
