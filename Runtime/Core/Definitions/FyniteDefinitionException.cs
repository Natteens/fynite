using System;

namespace Fynite
{
    /// <summary>
    /// Thrown by <see cref="FyniteBuilder{TContext}.Build"/> when the described machine is not a valid HFSM.
    /// </summary>
    public sealed class FyniteDefinitionException : Exception
    {
        /// <summary>Creates the exception with a message describing exactly what is wrong.</summary>
        public FyniteDefinitionException(string message) : base(message)
        {
        }

        /// <summary>Creates the exception with a message and an underlying cause.</summary>
        public FyniteDefinitionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
