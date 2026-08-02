using System;

namespace Fynite
{
    /// <summary>
    /// Thrown when draining the signal queue exceeded
    /// <see cref="FyniteMachineOptions.MaxMicrostepsPerPump"/>, which means enter blocks or effects are
    /// feeding each other signals without ever settling.
    /// </summary>
    /// <remarks>
    /// The pump is aborted, the remaining queue is cleared and the machine is faulted. The active path
    /// is left on the last fully established configuration.
    /// </remarks>
    public sealed class FyniteCycleException : Exception
    {
        internal FyniteCycleException(string message, int microsteps, int limit) : base(message)
        {
            Microsteps = microsteps;
            Limit = limit;
        }

        /// <summary>Reactions executed before the pump was aborted.</summary>
        public int Microsteps { get; }

        /// <summary>The configured budget that was exceeded.</summary>
        public int Limit { get; }
    }
}
