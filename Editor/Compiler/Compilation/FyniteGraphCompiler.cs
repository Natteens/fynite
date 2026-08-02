using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fynite.Authoring
{
    /// <summary>
    /// Turns an authoring document into the data a <see cref="FyniteGraphAsset"/> executes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The compiler is deliberately separate from both the visual editor and the importer: it takes a
    /// document and returns a result, touches no assets and opens no windows, so every validation rule
    /// can be exercised from a plain test.
    /// </para>
    /// <para>
    /// It validates first and stops at the first stage that produced errors, because later stages assume
    /// what earlier ones established — there is no point resolving block types for a hierarchy that does
    /// not have a root. Structural HFSM rules are not re-implemented here: once the dense data exists it
    /// is handed to <see cref="FyniteBuilder{TContext}"/>, and whatever the core rejects is translated
    /// back into a diagnostic.
    /// </para>
    /// </remarks>
    public static class FyniteGraphCompiler
    {
        /// <summary>Compiles a document.</summary>
        /// <param name="document">The authoring model. Not modified except by schema migration.</param>
        public static FyniteCompilationResult Compile(FyniteGraphDocument document)
        {
            var diagnostics = new List<FyniteDiagnostic>();
            var result = new FyniteCompilationResult(diagnostics);

            if (document == null)
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.SchemaUnreadable,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Schema,
                    "There is no graph document to compile."));
                return result;
            }

            FyniteDocumentJson.Normalize(document);

            if (!FyniteSchemaMigrator.CheckVersion(document.schemaVersion, diagnostics))
            {
                return result;
            }

            FyniteSchemaMigrator.Migrate(document, diagnostics);
            result.SchemaVersion = document.schemaVersion;

            ValidateIdentity(document, diagnostics);

            var contextType = ResolveContext(document, diagnostics);
            result.ContextType = contextType;

            var hierarchy = FyniteHierarchyAnalysis.Analyse(document, diagnostics);
            var signalIndices = ValidateSignals(document, diagnostics);
            var reactions = ValidateReactions(document, hierarchy, signalIndices, diagnostics);

            if (HasErrors(diagnostics))
            {
                return result;
            }

            var blocks = new List<FyniteBlock>();
            var graph = BuildRuntimeGraph(document, hierarchy, signalIndices, reactions, contextType, blocks, diagnostics);

            if (HasErrors(diagnostics))
            {
                return result;
            }

            var payloads = BuildPayloads(document, signalIndices, diagnostics);
            var binder = CreateBinder(contextType, diagnostics);

            if (binder == null || HasErrors(diagnostics))
            {
                return result;
            }

            // The core is the authority on HFSM validity. Anything it refuses becomes a diagnostic here
            // rather than a second implementation of the same rules.
            var blockArray = blocks.ToArray();
            try
            {
                binder.Build(graph, blockArray, payloads);
            }
            catch (FyniteDefinitionException exception)
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.BuilderRejected,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Graph,
                    "The compiled graph was rejected by the Fynite builder: " + exception.Message));
                return result;
            }
            catch (Exception exception)
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.InternalError,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Graph,
                    "The compiled graph could not be built: " + exception.Message));
                return result;
            }

            result.Graph = graph;
            result.Binder = binder;
            result.Blocks = blockArray;
            result.Payloads = payloads;
            result.Succeeded = true;
            return result;
        }

        private static bool HasErrors(List<FyniteDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].IsError)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateIdentity(FyniteGraphDocument document, List<FyniteDiagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(document.graphGuid))
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.GraphGuidMissing,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Graph,
                    "The graph has no persistent identity. External references could not point at it reliably."));
            }

            if (document.states.Count == 0)
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.GraphEmpty,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Graph,
                    "The graph declares no states. Add at least a root state."));
            }

            // One namespace for every identity in the graph: a state and a signal must not be able to
            // collide, because a diagnostic addressed to a GUID has to point at exactly one element.
            var seen = new Dictionary<string, IFyniteAuthoringElement>(StringComparer.Ordinal);

            foreach (var element in EnumerateElements(document))
            {
                if (string.IsNullOrEmpty(element.Guid))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        element,
                        FyniteDiagnosticCodes.MissingGuid,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Graph,
                        "A " + element.ElementKind + " has no persistent identity."));
                    continue;
                }

                if (seen.TryGetValue(element.Guid, out var other))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        element,
                        FyniteDiagnosticCodes.DuplicateGuid,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Graph,
                        "This " + element.ElementKind + " shares its identity '" + element.Guid + "' with a " +
                        other.ElementKind + ". Duplicating an element must give the copy a new identity."));
                    continue;
                }

                seen[element.Guid] = element;
            }
        }

        private static IEnumerable<IFyniteAuthoringElement> EnumerateElements(FyniteGraphDocument document)
        {
            foreach (var state in document.states)
            {
                if (state == null)
                {
                    continue;
                }

                yield return state;

                foreach (var phase in state.Phases)
                {
                    foreach (var block in phase.Value)
                    {
                        if (block != null)
                        {
                            yield return block;
                        }
                    }
                }
            }

            foreach (var signal in document.signals)
            {
                if (signal != null)
                {
                    yield return signal;
                }
            }

            foreach (var reaction in document.reactions)
            {
                if (reaction == null)
                {
                    continue;
                }

                yield return reaction;

                foreach (var guard in reaction.guards)
                {
                    if (guard != null)
                    {
                        yield return guard;
                    }
                }

                foreach (var effect in reaction.effects)
                {
                    if (effect != null)
                    {
                        yield return effect;
                    }
                }
            }
        }

        private static Type ResolveContext(FyniteGraphDocument document, List<FyniteDiagnostic> diagnostics)
        {
            if (!document.contextType.IsSet)
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.ContextMissing,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Context,
                    "The graph declares no context type. Select one before the graph can compile."));
                return null;
            }

            if (!FyniteTypeResolver.TryResolve(document.contextType, out var type, out bool ambiguous))
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.ContextTypeNotFound,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Context,
                    ambiguous
                        ? "The context type '" + document.contextType.FullName + "' exists in more than one " +
                          "assembly and the assembly it was saved with is gone, so it cannot be resolved " +
                          "without guessing. Select the context again."
                        : "The context type '" + document.contextType + "' no longer exists. It was renamed, " +
                          "moved or deleted; select the context again."));
                return null;
            }

            var rejection = FyniteContextCatalog.DescribeRejection(type);
            if (rejection != null)
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.ContextTypeInvalid,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Context,
                    "The context type cannot be used: " + rejection + "."));
                return null;
            }

            // A runner hands the machine a MonoBehaviour. An interface context is fine because a
            // component can implement it, but a sealed non-component class could never be supplied.
            if (!type.IsInterface && !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.ContextNotRunnable,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Context,
                    "The context type '" + type.FullName + "' is neither a MonoBehaviour nor an interface, so a " +
                    "FyniteRunner could never be given one."));
                return null;
            }

            return type;
        }

        private static Dictionary<string, int> ValidateSignals(FyniteGraphDocument document, List<FyniteDiagnostic> diagnostics)
        {
            var indices = new Dictionary<string, int>(StringComparer.Ordinal);
            var names = new Dictionary<string, FyniteSignalDocument>(StringComparer.Ordinal);

            for (int i = 0; i < document.signals.Count; i++)
            {
                var signal = document.signals[i];
                if (signal == null || string.IsNullOrEmpty(signal.guid))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(signal.name))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        signal,
                        FyniteDiagnosticCodes.SignalNameEmpty,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Signal,
                        "A signal has no name. Names are only labels, but every signal needs one to be " +
                        "identifiable in diagnostics."));
                }
                else if (names.TryGetValue(signal.name, out _))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        signal,
                        FyniteDiagnosticCodes.SignalNameDuplicate,
                        FyniteDiagnosticSeverity.Warning,
                        FyniteDiagnosticCategory.Signal,
                        "Another signal is also called '" + signal.name + "'. They stay distinct because " +
                        "identity is by GUID, but diagnostics will be ambiguous."));
                }
                else
                {
                    names[signal.name] = signal;
                }

                if (signal.payloadType.IsSet)
                {
                    ValidatePayloadType(signal, diagnostics);
                }

                if (!indices.ContainsKey(signal.guid))
                {
                    indices[signal.guid] = indices.Count;
                }
            }

            return indices;
        }

        private static void ValidatePayloadType(FyniteSignalDocument signal, List<FyniteDiagnostic> diagnostics)
        {
            if (!FyniteTypeResolver.TryResolve(signal.payloadType, out var payload, out bool ambiguous))
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    signal,
                    FyniteDiagnosticCodes.PayloadTypeNotFound,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Signal,
                    ambiguous
                        ? "The payload type '" + signal.payloadType.FullName + "' is ambiguous; select it again."
                        : "The payload type '" + signal.payloadType + "' no longer exists."));
                return;
            }

            if (payload.ContainsGenericParameters)
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    signal,
                    FyniteDiagnosticCodes.PayloadTypeInvalid,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Signal,
                    "The payload type '" + payload.FullName + "' is an open generic type and cannot be a payload."));
                return;
            }

            if (payload == typeof(void) || payload.IsByRef || payload.IsPointer)
            {
                diagnostics.Add(FyniteDiagnostic.For(
                    signal,
                    FyniteDiagnosticCodes.PayloadTypeInvalid,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Signal,
                    "The payload type '" + payload.FullName + "' cannot be carried by a signal."));
            }
        }

        private static List<FyniteReactionDocument> ValidateReactions(
            FyniteGraphDocument document,
            FyniteHierarchyAnalysis hierarchy,
            Dictionary<string, int> signalIndices,
            List<FyniteDiagnostic> diagnostics)
        {
            var valid = new List<FyniteReactionDocument>();
            var byOrder = new Dictionary<int, FyniteReactionDocument>();

            for (int i = 0; i < document.reactions.Count; i++)
            {
                var reaction = document.reactions[i];
                if (reaction == null || string.IsNullOrEmpty(reaction.guid))
                {
                    continue;
                }

                bool usable = true;

                if (string.IsNullOrEmpty(reaction.sourceGuid))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        reaction,
                        FyniteDiagnosticCodes.ReactionSourceMissing,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Reaction,
                        "This reaction is not attached to a source state."));
                    usable = false;
                }
                else if (!hierarchy.IndexByGuid.ContainsKey(reaction.sourceGuid))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        reaction,
                        FyniteDiagnosticCodes.ReactionSourceNotFound,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Reaction,
                        "This reaction's source state no longer exists."));
                    usable = false;
                }

                if (string.IsNullOrEmpty(reaction.signalGuid))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        reaction,
                        FyniteDiagnosticCodes.ReactionSignalMissing,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Reaction,
                        "This reaction listens to no signal."));
                    usable = false;
                }
                else if (!signalIndices.ContainsKey(reaction.signalGuid))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        reaction,
                        FyniteDiagnosticCodes.ReactionSignalNotFound,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Reaction,
                        "The signal this reaction listens to no longer exists. It was deleted after the " +
                        "reaction was created."));
                    usable = false;
                }

                if (reaction.HasTarget && !hierarchy.IndexByGuid.ContainsKey(reaction.targetGuid))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        reaction,
                        FyniteDiagnosticCodes.ReactionTargetNotFound,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Reaction,
                        "This reaction's target state no longer exists. Clear the target to make it an " +
                        "effect-only reaction, or point it at an existing state."));
                    usable = false;
                }

                if (byOrder.TryGetValue(reaction.order, out _))
                {
                    diagnostics.Add(FyniteDiagnostic.For(
                        reaction,
                        FyniteDiagnosticCodes.ReactionOrderAmbiguous,
                        FyniteDiagnosticSeverity.Warning,
                        FyniteDiagnosticCategory.Reaction,
                        "Another reaction also declares order " + reaction.order + ". Registration order is " +
                        "the last tie-break between equal priorities, so give them distinct orders to make " +
                        "the outcome explicit."));
                }
                else
                {
                    byOrder[reaction.order] = reaction;
                }

                if (usable)
                {
                    valid.Add(reaction);
                }
            }

            // Explicit order first, then identity, so two reactions that share an order still compile to
            // the same thing on every machine rather than following list position.
            valid.Sort((a, b) =>
            {
                int byExplicitOrder = a.order.CompareTo(b.order);
                return byExplicitOrder != 0 ? byExplicitOrder : string.CompareOrdinal(a.guid, b.guid);
            });

            return valid;
        }

        private static FyniteRuntimeGraph BuildRuntimeGraph(
            FyniteGraphDocument document,
            FyniteHierarchyAnalysis hierarchy,
            Dictionary<string, int> signalIndices,
            List<FyniteReactionDocument> reactions,
            Type contextType,
            List<FyniteBlock> blocks,
            List<FyniteDiagnostic> diagnostics)
        {
            var graph = new FyniteRuntimeGraph
            {
                graphGuid = document.graphGuid,
                rootIndex = hierarchy.RootIndex,
                states = new FyniteRuntimeState[hierarchy.Ordered.Count],
                signals = new FyniteRuntimeSignal[signalIndices.Count],
                reactions = new FyniteRuntimeReaction[reactions.Count]
            };

            for (int i = 0; i < hierarchy.Ordered.Count; i++)
            {
                var state = hierarchy.Ordered[i];
                graph.states[i] = new FyniteRuntimeState
                {
                    name = state.name,
                    parent = hierarchy.ParentIndex(i),
                    initialChild = hierarchy.InitialChildIndex(i),
                    enter = CompileBlocks(state.enter, FyniteBlockKind.Action, contextType, blocks, diagnostics),
                    tick = CompileBlocks(state.tick, FyniteBlockKind.Action, contextType, blocks, diagnostics),
                    fixedTick = CompileBlocks(state.fixedTick, FyniteBlockKind.Action, contextType, blocks, diagnostics),
                    exit = CompileBlocks(state.exit, FyniteBlockKind.Action, contextType, blocks, diagnostics)
                };
            }

            foreach (var pair in signalIndices)
            {
                var signal = document.FindSignal(pair.Key);
                graph.signals[pair.Value] = new FyniteRuntimeSignal
                {
                    guid = pair.Key,
                    name = signal != null ? signal.name : pair.Key
                };
            }

            for (int i = 0; i < reactions.Count; i++)
            {
                var reaction = reactions[i];
                graph.reactions[i] = new FyniteRuntimeReaction
                {
                    source = hierarchy.IndexByGuid[reaction.sourceGuid],
                    signal = signalIndices[reaction.signalGuid],
                    target = reaction.HasTarget ? hierarchy.IndexByGuid[reaction.targetGuid] : -1,
                    priority = reaction.priority,
                    guards = CompileBlocks(reaction.guards, FyniteBlockKind.Guard, contextType, blocks, diagnostics),
                    effects = CompileBlocks(reaction.effects, FyniteBlockKind.Action, contextType, blocks, diagnostics)
                };
            }

            return graph;
        }

        private static int[] CompileBlocks(
            List<FyniteBlockDocument> occurrences,
            FyniteBlockKind expectedKind,
            Type contextType,
            List<FyniteBlock> blocks,
            List<FyniteDiagnostic> diagnostics)
        {
            if (occurrences == null || occurrences.Count == 0)
            {
                return Array.Empty<int>();
            }

            var indices = new List<int>(occurrences.Count);

            for (int i = 0; i < occurrences.Count; i++)
            {
                var occurrence = occurrences[i];
                if (occurrence == null)
                {
                    continue;
                }

                var prototype = FyniteBlockFactory.CreatePrototype(occurrence, expectedKind, contextType, diagnostics);
                if (prototype == null)
                {
                    continue;
                }

                indices.Add(blocks.Count);
                blocks.Add(prototype);
            }

            return indices.ToArray();
        }

        private static FynitePayloadType[] BuildPayloads(
            FyniteGraphDocument document,
            Dictionary<string, int> signalIndices,
            List<FyniteDiagnostic> diagnostics)
        {
            var payloads = new FynitePayloadType[signalIndices.Count];

            foreach (var pair in signalIndices)
            {
                var signal = document.FindSignal(pair.Key);
                if (signal == null || !signal.payloadType.IsSet)
                {
                    continue;
                }

                var payloadType = FyniteTypeResolver.Resolve(signal.payloadType);
                if (payloadType == null)
                {
                    continue;
                }

                // Reflection is fine here: this runs in the editor, and what gets stored is an instance
                // whose closed generic type the deserializer will rebuild on its own.
                payloads[pair.Value] = (FynitePayloadType)Activator.CreateInstance(
                    typeof(FynitePayloadType<>).MakeGenericType(payloadType));
            }

            return payloads;
        }

        private static FyniteContextBinder CreateBinder(Type contextType, List<FyniteDiagnostic> diagnostics)
        {
            if (contextType == null)
            {
                return null;
            }

            try
            {
                return (FyniteContextBinder)Activator.CreateInstance(
                    typeof(FyniteContextBinder<>).MakeGenericType(contextType));
            }
            catch (Exception exception)
            {
                diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.ContextTypeInvalid,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Context,
                    "No binder could be created for context type '" + contextType.FullName + "': " + exception.Message));
                return null;
            }
        }
    }
}
