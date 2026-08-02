using System;
using Fynite.Diagnostics;
using UnityEngine;

namespace Fynite
{
    /// <summary>
    /// Drives one machine from Unity's player loop.
    /// </summary>
    /// <remarks>
    /// The runner owns exactly one machine and never shares it. Both the definition and the context are
    /// supplied explicitly — nothing is discovered through scene searches, singletons or service
    /// locators. The machine is created once and then started and stopped as the component is enabled
    /// and disabled.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Fynite/Fynite Runner")]
    public sealed class FyniteRunner : MonoBehaviour
    {
        [Tooltip("Compiled machine structure to execute.")]
        [SerializeField]
        private FyniteDefinitionAsset definition;

        [Tooltip("Component the blocks operate on. Must be assignable to the definition's context type.")]
        [SerializeField]
        private MonoBehaviour context;

        [Tooltip("Create and start the machine when this component is enabled.")]
        [SerializeField]
        private bool startOnEnable = true;

        [Tooltip("Forward Update to the machine's Tick.")]
        [SerializeField]
        private bool tickInUpdate = true;

        [Tooltip("Forward FixedUpdate to the machine's FixedTick.")]
        [SerializeField]
        private bool tickInFixedUpdate = true;

        [Tooltip("Write every machine diagnostic to the console. Verbose; for investigation only.")]
        [SerializeField]
        private bool logDiagnostics;

        private IFyniteMachine _machine;
        private bool _configurationErrorLogged;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private FyniteRunnerObservation _observation;

        /// <summary>
        /// What this runner's machine last did, for the graph editor's play mode view.
        /// </summary>
        /// <remarks>
        /// Only compiled in the editor and in development builds. It is an observer installed when the
        /// machine is created; reading it never touches the machine and never modifies any asset.
        /// </remarks>
        public FyniteRunnerObservation Observation => _observation;
#endif

        /// <summary>The machine owned by this runner, or null before it is created.</summary>
        public IFyniteMachine Machine => _machine;

        /// <summary>The definition this runner executes.</summary>
        public FyniteDefinitionAsset Definition => definition;

        /// <summary>The context handed to the machine.</summary>
        public MonoBehaviour Context => context;

        /// <summary>True while the machine exists and has an active path.</summary>
        public bool IsRunning => _machine != null && _machine.IsRunning;

        /// <summary>
        /// Assigns the definition and context from code, replacing anything set in the inspector.
        /// Disposes an already created machine so the next start builds one from the new pair.
        /// </summary>
        /// <exception cref="InvalidOperationException">Called while the machine is running.</exception>
        public void Bind(FyniteDefinitionAsset newDefinition, MonoBehaviour newContext)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException(
                    Describe() + " cannot be rebound while its machine is running; call StopMachine first.");
            }

            definition = newDefinition;
            context = newContext;
            _configurationErrorLogged = false;

            DisposeMachine();
        }

        /// <summary>
        /// Creates the machine if needed and starts it. Does nothing when it is already running.
        /// </summary>
        public void StartMachine()
        {
            if (IsRunning)
            {
                return;
            }

            if (!TryEnsureMachine())
            {
                return;
            }

            _machine.Start();
        }

        /// <summary>Stops the machine if it is running. The instance is kept for a later start.</summary>
        public void StopMachine()
        {
            if (_machine != null && _machine.IsRunning)
            {
                _machine.Stop();
            }
        }

        /// <summary>Raises a payload-less signal on the machine.</summary>
        /// <exception cref="InvalidOperationException">No machine has been created yet.</exception>
        public void Raise(SignalHandle signal)
        {
            RequireMachine().Raise(signal);
        }

        /// <summary>Raises a signal with a payload on the machine.</summary>
        /// <exception cref="InvalidOperationException">No machine has been created yet.</exception>
        public void Raise(SignalHandle signal, object payload)
        {
            RequireMachine().Raise(signal, payload);
        }

        /// <summary>
        /// Raises a persistently referenced signal. The reference resolves to a dense handle on its
        /// first use and reuses it afterwards.
        /// </summary>
        /// <exception cref="InvalidOperationException">No machine yet, or the reference does not point at a signal of this runner's graph.</exception>
        public void Raise(FyniteSignalReference signal)
        {
            signal.Raise(RequireMachine());
        }

        /// <summary>Raises a persistently referenced signal together with a payload.</summary>
        /// <exception cref="InvalidOperationException">No machine yet, or the reference does not point at a signal of this runner's graph.</exception>
        public void Raise(FyniteSignalReference signal, object payload)
        {
            signal.Raise(RequireMachine(), payload);
        }

        private void OnEnable()
        {
            if (startOnEnable)
            {
                StartMachine();
            }
        }

        private void Update()
        {
            if (tickInUpdate && _machine != null && _machine.IsRunning)
            {
                _machine.Tick(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (tickInFixedUpdate && _machine != null && _machine.IsRunning)
            {
                _machine.FixedTick(Time.fixedDeltaTime);
            }
        }

        private void OnDisable()
        {
            StopMachine();
        }

        private void OnDestroy()
        {
            DisposeMachine();
        }

        private bool TryEnsureMachine()
        {
            if (_machine != null)
            {
                if (_machine.Lifecycle == FyniteLifecycle.Disposed || _machine.Lifecycle == FyniteLifecycle.Faulted)
                {
                    LogConfigurationError(
                        Describe() + " cannot start because its machine is " + _machine.Lifecycle +
                        "; rebind the runner to build a new one.");
                    return false;
                }

                return true;
            }

            if (definition == null)
            {
                LogConfigurationError(Describe() + " has no definition assigned.");
                return false;
            }

            if (context == null)
            {
                LogConfigurationError(
                    Describe() + " has no context assigned; definition '" + definition.name + "' expects a '" +
                    definition.ContextType.FullName + "'.");
                return false;
            }

            if (!definition.ContextType.IsInstanceOfType(context))
            {
                LogConfigurationError(
                    Describe() + " has an incompatible context: definition '" + definition.name + "' expects a '" +
                    definition.ContextType.FullName + "' but the assigned component is a '" +
                    context.GetType().FullName + "'.");
                return false;
            }

            IFyniteDiagnostics diagnostics = logDiagnostics
                ? new FyniteConsoleDiagnostics(this, name)
                : null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _observation = new FyniteRunnerObservation(diagnostics);
            diagnostics = _observation;
#endif

            _machine = definition.CreateMachine(context, diagnostics);
            _configurationErrorLogged = false;
            return true;
        }

        private IFyniteMachine RequireMachine()
        {
            if (_machine == null)
            {
                throw new InvalidOperationException(
                    Describe() + " has no machine yet; call StartMachine or enable the component first.");
            }

            return _machine;
        }

        private void DisposeMachine()
        {
            if (_machine == null)
            {
                return;
            }

            var machine = _machine;
            _machine = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _observation = null;
#endif
            machine.Dispose();
        }

        // A misconfigured runner is reported once instead of every frame it is asked to start.
        private void LogConfigurationError(string message)
        {
            if (_configurationErrorLogged)
            {
                return;
            }

            _configurationErrorLogged = true;
            Debug.LogError(message, this);
        }

        private string Describe() => "FyniteRunner on '" + name + "'";
    }
}
