using Fynite.Authoring;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// Builds authoring documents for tests without going anywhere near the graph window.
    /// </summary>
    /// <remarks>
    /// The compiler's input is a document, so every validation rule can be exercised by assembling one
    /// directly. That keeps the compiler tests fast, and it means a broken rule points at the compiler
    /// rather than at the editor that produced the document.
    /// </remarks>
    internal static class AuthoringDocumentBuilder
    {
        /// <summary>Identity of the root state in every document this builds.</summary>
        internal const string RootGuid = "state-root";

        /// <summary>Identity of the Idle state.</summary>
        internal const string IdleGuid = "state-idle";

        /// <summary>Identity of the Moving state.</summary>
        internal const string MovingGuid = "state-moving";

        /// <summary>Identity of the payload-less Move signal.</summary>
        internal const string MoveGuid = "signal-move";

        /// <summary>Identity of the payload-less Stop signal.</summary>
        internal const string StopGuid = "signal-stop";

        /// <summary>Identity of the Notify signal, which carries a string.</summary>
        internal const string NotifyGuid = "signal-notify";

        /// <summary>
        /// The reference graph: Root with Idle and Moving under it, three signals, and reactions
        /// covering a guarded transition, a plain transition and one with no target.
        /// </summary>
        internal static FyniteGraphDocument Valid()
        {
            var document = new FyniteGraphDocument
            {
                schemaVersion = FyniteGraphDocument.CurrentSchemaVersion,
                graphGuid = "graph-under-test",
                contextType = FyniteTypeReference.From(typeof(GraphTestContext))
            };

            document.states.Add(new FyniteStateDocument
            {
                guid = RootGuid,
                name = "Root",
                isRoot = true,
                position = new Vector2(10f, 20f)
            });

            document.states.Add(new FyniteStateDocument
            {
                guid = IdleGuid,
                name = "Idle",
                parentGuid = RootGuid,
                isInitial = true,
                position = new Vector2(30f, 40f)
            });

            document.states.Add(new FyniteStateDocument
            {
                guid = MovingGuid,
                name = "Moving",
                parentGuid = RootGuid,
                position = new Vector2(50f, 60f)
            });

            document.signals.Add(new FyniteSignalDocument { guid = MoveGuid, name = "Move" });
            document.signals.Add(new FyniteSignalDocument { guid = StopGuid, name = "Stop" });
            document.signals.Add(new FyniteSignalDocument
            {
                guid = NotifyGuid,
                name = "Notify",
                payloadType = FyniteTypeReference.From(typeof(string))
            });

            return document;
        }

        /// <summary>Adds a block occurrence to a list and returns it.</summary>
        internal static FyniteBlockDocument Block(
            System.Collections.Generic.List<FyniteBlockDocument> slot,
            System.Type blockType,
            string guid,
            string configJson = null)
        {
            var block = new FyniteBlockDocument
            {
                guid = guid,
                blockType = FyniteTypeReference.From(blockType),
                configJson = configJson
            };

            slot.Add(block);
            return block;
        }

        /// <summary>Adds a reaction and returns it.</summary>
        internal static FyniteReactionDocument Reaction(
            FyniteGraphDocument document,
            string guid,
            string sourceGuid,
            string signalGuid,
            string targetGuid,
            int priority = 0,
            int order = 0)
        {
            var reaction = new FyniteReactionDocument
            {
                guid = guid,
                sourceGuid = sourceGuid,
                signalGuid = signalGuid,
                targetGuid = targetGuid,
                priority = priority,
                order = order
            };

            document.reactions.Add(reaction);
            return reaction;
        }

        /// <summary>Finds a state in a document by identity.</summary>
        internal static FyniteStateDocument State(FyniteGraphDocument document, string guid) =>
            document.FindState(guid);

        /// <summary>Serializes the configuration of a block instance the way an occurrence stores it.</summary>
        internal static string Config(FyniteBlock block) => FyniteBlockFactory.SerializeConfiguration(block);
    }
}
