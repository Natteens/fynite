using System;
using Fynite.Diagnostics;
using UnityEngine;

namespace Fynite
{
    /// <summary>
    /// A definition that can be referenced from a scene or prefab.
    /// </summary>
    /// <remarks>
    /// This is the boundary the future <c>.fyn</c> compiler will produce against: an importer will emit
    /// a concrete asset that bakes graph GUIDs into dense handles, while everything downstream keeps
    /// consuming plain <see cref="IFyniteMachine"/> instances. Nothing here knows about Graph Toolkit.
    /// Prefer deriving from <see cref="FyniteDefinitionAsset{TContext}"/>.
    /// </remarks>
    public abstract class FyniteDefinitionAsset : ScriptableObject
    {
        /// <summary>Context type a machine of this asset must be given.</summary>
        public abstract Type ContextType { get; }

        /// <summary>
        /// Context-agnostic view of the compiled structure, for tooling and diagnostics.
        /// </summary>
        public abstract IFyniteDefinition Definition { get; }

        /// <summary>Creates a machine bound to <paramref name="context"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="context"/> is not a <see cref="ContextType"/>.</exception>
        public abstract IFyniteMachine CreateMachine(object context, IFyniteDiagnostics diagnostics = null);
    }

    /// <summary>
    /// Typed base for definition assets: implements context validation and machine creation, leaving
    /// only the definition itself to the subclass.
    /// </summary>
    /// <typeparam name="TContext">Context type the definition executes against.</typeparam>
    public abstract class FyniteDefinitionAsset<TContext> : FyniteDefinitionAsset where TContext : class
    {
        /// <inheritdoc />
        public sealed override Type ContextType => typeof(TContext);

        /// <inheritdoc />
        public sealed override IFyniteDefinition Definition => GetDefinition();

        /// <summary>
        /// Returns the compiled definition. Implementations should build it once and cache it: a
        /// definition is immutable and meant to be shared by every machine of this asset.
        /// </summary>
        public abstract FyniteDefinition<TContext> GetDefinition();

        /// <inheritdoc />
        public sealed override IFyniteMachine CreateMachine(object context, IFyniteDiagnostics diagnostics = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!(context is TContext typed))
            {
                throw new ArgumentException(
                    "Definition asset '" + name + "' needs a context of type '" + typeof(TContext).FullName +
                    "' but received '" + context.GetType().FullName + "'.",
                    nameof(context));
            }

            var definition = GetDefinition();
            if (definition == null)
            {
                throw new InvalidOperationException(
                    "Definition asset '" + name + "' returned no definition from GetDefinition().");
            }

            return definition.CreateMachine(typed, diagnostics);
        }
    }
}
