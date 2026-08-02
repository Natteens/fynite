using System;

namespace Fynite
{
    /// <summary>Tuning knobs for a machine instance. Values are copied when the machine is created.</summary>
    public sealed class FyniteMachineOptions
    {
        private int _initialSignalCapacity = 16;
        private int _maxMicrostepsPerPump = 128;

        /// <summary>Shared defaults. Treat as read-only.</summary>
        public static FyniteMachineOptions Default { get; } = new FyniteMachineOptions();

        /// <summary>Pre-allocated size of the signal queue. The queue still grows if it overflows.</summary>
        public int InitialSignalCapacity
        {
            get => _initialSignalCapacity;
            set => _initialSignalCapacity = value;
        }

        /// <summary>
        /// Maximum number of reactions that may run while draining the queue once. Every selected
        /// reaction counts, whether it transitions or only runs effects. Exceeding the budget aborts
        /// the pump with a <see cref="FyniteCycleException"/> instead of looping forever.
        /// </summary>
        public int MaxMicrostepsPerPump
        {
            get => _maxMicrostepsPerPump;
            set => _maxMicrostepsPerPump = value;
        }

        /// <summary>Throws when any value is out of range.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A value is less than one.</exception>
        public void Validate()
        {
            if (_initialSignalCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(InitialSignalCapacity),
                    _initialSignalCapacity,
                    "InitialSignalCapacity must be at least 1.");
            }

            if (_maxMicrostepsPerPump < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxMicrostepsPerPump),
                    _maxMicrostepsPerPump,
                    "MaxMicrostepsPerPump must be at least 1.");
            }
        }
    }
}
