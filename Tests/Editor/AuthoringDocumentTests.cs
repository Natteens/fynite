using System.Collections.Generic;
using System.Linq;
using Fynite.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// The authoring document as a persisted artefact: what survives a round trip, and what is refused.
    /// </summary>
    public sealed class AuthoringDocumentTests
    {
        [Test]
        public void RoundTripPreservesIdentitiesNamesAndPositions()
        {
            var original = AuthoringDocumentBuilder.Valid();

            var json = FyniteDocumentJson.ToJson(original);
            Assert.IsTrue(FyniteDocumentJson.TryParse(json, out var restored, out _));

            Assert.AreEqual(original.graphGuid, restored.graphGuid);
            Assert.AreEqual(original.states.Count, restored.states.Count);

            for (int i = 0; i < original.states.Count; i++)
            {
                Assert.AreEqual(original.states[i].guid, restored.states[i].guid, "state identity");
                Assert.AreEqual(original.states[i].name, restored.states[i].name, "state name");
                Assert.AreEqual(original.states[i].position, restored.states[i].position, "state position");
                Assert.AreEqual(original.states[i].parentGuid, restored.states[i].parentGuid, "parent identity");
                Assert.AreEqual(original.states[i].isRoot, restored.states[i].isRoot, "root flag");
                Assert.AreEqual(original.states[i].isInitial, restored.states[i].isInitial, "initial flag");
            }
        }

        [Test]
        public void RoundTripPreservesSignalPayloadTypes()
        {
            var json = FyniteDocumentJson.ToJson(AuthoringDocumentBuilder.Valid());
            Assert.IsTrue(FyniteDocumentJson.TryParse(json, out var restored, out _));

            var notify = restored.FindSignal(AuthoringDocumentBuilder.NotifyGuid);
            Assert.AreEqual(FyniteTypeReference.From(typeof(string)), notify.payloadType);

            var move = restored.FindSignal(AuthoringDocumentBuilder.MoveGuid);
            Assert.IsFalse(move.payloadType.IsSet, "Move carries no payload.");
        }

        [Test]
        public void RoundTripPreservesBlockConfiguration()
        {
            var document = AuthoringDocumentBuilder.Valid();
            var idle = document.FindState(AuthoringDocumentBuilder.IdleGuid);

            var configured = new GraphTestEnterAction();
            var config = AuthoringDocumentBuilder.Config(configured);
            AuthoringDocumentBuilder.Block(idle.enter, typeof(GraphTestEnterAction), "block-1", config);

            var json = FyniteDocumentJson.ToJson(document);
            Assert.IsTrue(FyniteDocumentJson.TryParse(json, out var restored, out _));

            var block = restored.FindState(AuthoringDocumentBuilder.IdleGuid).enter[0];
            Assert.AreEqual("block-1", block.guid);
            Assert.AreEqual(FyniteTypeReference.From(typeof(GraphTestEnterAction)), block.blockType);
            Assert.AreEqual(config, block.configJson);
        }

        [Test]
        public void RenamingAnElementLeavesEveryReferenceIntact()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Reaction(
                document, "r1", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.MoveGuid,
                AuthoringDocumentBuilder.MovingGuid);

            document.FindSignal(AuthoringDocumentBuilder.MoveGuid).name = "Advance";
            document.FindState(AuthoringDocumentBuilder.MovingGuid).name = "Running";

            var result = FyniteGraphCompiler.Compile(document);

            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.AreEqual("Advance", result.Graph.signals.Single(s => s.guid == AuthoringDocumentBuilder.MoveGuid).name);
            Assert.AreEqual(1, result.Graph.reactions.Length, "the reaction still resolves after both renames");
        }

        [Test]
        public void UnknownFieldsAreIgnoredRatherThanRefused()
        {
            var json = FyniteDocumentJson.ToJson(AuthoringDocumentBuilder.Valid());
            var withExtra = json.TrimEnd().TrimEnd('}') + ",\n    \"somethingFromTheFuture\": 12\n}";

            Assert.IsTrue(FyniteDocumentJson.TryParse(withExtra, out var restored, out _));
            Assert.AreEqual(3, restored.states.Count);
        }

        [Test]
        public void MissingSchemaVersionIsRefusedWithADiagnostic()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.schemaVersion = 0;

            Assert.IsFalse(FyniteDocumentJson.TryParse(FyniteDocumentJson.ToJson(document), out _, out var diagnostics));
            Assert.IsTrue(diagnostics.Any(d => d.Code == FyniteDiagnosticCodes.SchemaVersionMissing), Describe(diagnostics));
        }

        [Test]
        public void MalformedJsonIsRefusedWithADiagnostic()
        {
            Assert.IsFalse(FyniteDocumentJson.TryParse("{ not json at all", out _, out var diagnostics));
            Assert.IsTrue(diagnostics.Any(d => d.IsError), Describe(diagnostics));
        }

        [Test]
        public void AFutureSchemaVersionIsRefusedRatherThanReinterpreted()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.schemaVersion = FyniteGraphDocument.CurrentSchemaVersion + 1;

            Assert.IsFalse(FyniteDocumentJson.TryParse(FyniteDocumentJson.ToJson(document), out var parsed, out var diagnostics));
            Assert.IsNull(parsed, "nothing is handed back that could be mistaken for a usable graph");
            Assert.IsTrue(diagnostics.Any(d => d.Code == FyniteDiagnosticCodes.SchemaVersionFromFuture), Describe(diagnostics));
        }

        [Test]
        public void AnOlderSchemaVersionIsExplicitlyRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.schemaVersion = FyniteGraphDocument.CurrentSchemaVersion - 1;

            Assert.IsFalse(FyniteDocumentJson.TryParse(
                FyniteDocumentJson.ToJson(document), out var parsed, out var diagnostics));
            Assert.IsNull(parsed);
            Assert.IsTrue(diagnostics.Any(d => d.Code == FyniteDiagnosticCodes.SchemaVersionUnsupported), Describe(diagnostics));
        }

        [Test]
        public void SerializingTheSameDocumentTwiceProducesIdenticalText()
        {
            var document = AuthoringDocumentBuilder.Valid();
            Assert.AreEqual(FyniteDocumentJson.ToJson(document), FyniteDocumentJson.ToJson(document));
        }

        internal static string Describe(FyniteCompilationResult result) =>
            string.Join("\n", result.Diagnostics.Select(d => d.ToString()));

        internal static string Describe(IReadOnlyList<FyniteDiagnostic> diagnostics) =>
            string.Join("\n", diagnostics.Select(d => d.ToString()));
    }
}
