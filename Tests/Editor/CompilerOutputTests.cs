using System.Linq;
using Fynite.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// What the compiler actually emits: ordering, identity mapping, determinism and factory isolation.
    /// </summary>
    public sealed class CompilerOutputTests
    {
        private static string Describe(FyniteCompilationResult result) => AuthoringDocumentTests.Describe(result);

        [Test]
        public void IdentitiesBecomeDenseParentFirstIndices()
        {
            var document = AuthoringDocumentBuilder.Valid();
            var result = FyniteGraphCompiler.Compile(document);

            Assert.IsTrue(result.Succeeded, Describe(result));

            var states = result.Graph.states;
            Assert.AreEqual("Root", states[result.Graph.rootIndex].name);

            for (int i = 0; i < states.Length; i++)
            {
                Assert.Less(states[i].parent, i, "a parent must come before its child so the builder can wire it");
            }

            // No authoring identity leaks into the executable structure.
            Assert.IsFalse(states.Any(s => s.name.Contains("state-")));
        }

        [Test]
        public void SignalIdentitiesSurviveForExternalReferencesOnly()
        {
            var result = FyniteGraphCompiler.Compile(AuthoringDocumentBuilder.Valid());

            Assert.IsTrue(result.Succeeded, Describe(result));
            CollectionAssert.AreEquivalent(
                new[] { AuthoringDocumentBuilder.MoveGuid, AuthoringDocumentBuilder.StopGuid, AuthoringDocumentBuilder.NotifyGuid },
                result.Graph.signals.Select(s => s.guid).ToArray());
        }

        [Test]
        public void NoVisualDataReachesTheCompiledGraph()
        {
            var document = AuthoringDocumentBuilder.Valid();
            var result = FyniteGraphCompiler.Compile(document);

            Assert.IsTrue(result.Succeeded, Describe(result));

            // The runtime structure has no field able to carry a position at all; this asserts the shape
            // stays that way if anyone ever adds one.
            var fields = typeof(FyniteRuntimeState).GetFields();
            Assert.IsFalse(
                fields.Any(f => f.FieldType == typeof(Vector2) || f.FieldType == typeof(Vector3)),
                "compiled states must not carry layout data");
        }

        [Test]
        public void BlockOrderWithinAPhaseIsPreserved()
        {
            var document = AuthoringDocumentBuilder.Valid();
            var idle = document.FindState(AuthoringDocumentBuilder.IdleGuid);

            AuthoringDocumentBuilder.Block(idle.enter, typeof(GraphTestEnterAction), "b1", "{\"label\":\"first\"}");
            AuthoringDocumentBuilder.Block(idle.enter, typeof(GraphTestEnterAction), "b2", "{\"label\":\"second\"}");
            AuthoringDocumentBuilder.Block(idle.enter, typeof(GraphTestEnterAction), "b3", "{\"label\":\"third\"}");

            var result = FyniteGraphCompiler.Compile(document);
            Assert.IsTrue(result.Succeeded, Describe(result));

            var compiledIdle = result.Graph.states.Single(s => s.name == "Idle");
            Assert.AreEqual(3, compiledIdle.enter.Length);

            var labels = compiledIdle.enter
                .Select(index => JsonUtility.ToJson(result.Blocks[index]))
                .ToArray();

            Assert.IsTrue(labels[0].Contains("first"), labels[0]);
            Assert.IsTrue(labels[1].Contains("second"), labels[1]);
            Assert.IsTrue(labels[2].Contains("third"), labels[2]);
        }

        [Test]
        public void ReactionOrderFollowsExplicitOrderNotListPosition()
        {
            var document = AuthoringDocumentBuilder.Valid();

            AuthoringDocumentBuilder.Reaction(document, "late", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.MoveGuid, null, order: 20);
            AuthoringDocumentBuilder.Reaction(document, "early", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.StopGuid, null, order: 5);

            var result = FyniteGraphCompiler.Compile(document);
            Assert.IsTrue(result.Succeeded, Describe(result));

            var moveIndex = System.Array.FindIndex(result.Graph.signals, s => s.guid == AuthoringDocumentBuilder.MoveGuid);
            var stopIndex = System.Array.FindIndex(result.Graph.signals, s => s.guid == AuthoringDocumentBuilder.StopGuid);

            Assert.AreEqual(stopIndex, result.Graph.reactions[0].signal, "the lower explicit order registers first");
            Assert.AreEqual(moveIndex, result.Graph.reactions[1].signal);
        }

        [Test]
        public void PositionsDoNotAffectTheCompiledResult()
        {
            var first = AuthoringDocumentBuilder.Valid();
            var second = AuthoringDocumentBuilder.Valid();

            foreach (var state in second.states)
            {
                state.position = new Vector2(-9999f, 9999f);
            }

            var a = FyniteGraphCompiler.Compile(first);
            var b = FyniteGraphCompiler.Compile(second);

            Assert.IsTrue(a.Succeeded && b.Succeeded);
            CollectionAssert.AreEqual(
                a.Graph.states.Select(s => s.name).ToArray(),
                b.Graph.states.Select(s => s.name).ToArray());
        }

        [Test]
        public void CompilingTheSameDocumentTwiceProducesTheSameStructure()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Reaction(
                document, "r1", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.MoveGuid,
                AuthoringDocumentBuilder.MovingGuid, order: 1);

            var a = FyniteGraphCompiler.Compile(document);
            var b = FyniteGraphCompiler.Compile(document);

            Assert.IsTrue(a.Succeeded && b.Succeeded);
            Assert.AreEqual(JsonUtility.ToJson(a.Graph), JsonUtility.ToJson(b.Graph));
        }

        [Test]
        public void AFailedCompilationHandsBackNothingUsable()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.RootGuid).isRoot = false;

            var result = FyniteGraphCompiler.Compile(document);

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Graph, "a failure must not hand back a structure that looks runnable");
            Assert.IsNull(result.Binder);
            Assert.IsNull(result.Blocks);
        }

        [Test]
        public void EveryOccurrenceGetsItsOwnPrototype()
        {
            var document = AuthoringDocumentBuilder.Valid();
            var idle = document.FindState(AuthoringDocumentBuilder.IdleGuid);

            AuthoringDocumentBuilder.Block(idle.enter, typeof(GraphTestEnterAction), "b1", "{\"label\":\"a\"}");
            AuthoringDocumentBuilder.Block(idle.tick, typeof(GraphTestEnterAction), "b2", "{\"label\":\"b\"}");

            var result = FyniteGraphCompiler.Compile(document);
            Assert.IsTrue(result.Succeeded, Describe(result));

            Assert.AreEqual(2, result.Blocks.Length);
            Assert.AreNotSame(result.Blocks[0], result.Blocks[1], "the same type used twice must not share one prototype");
        }

        [Test]
        public void TheBinderCarriesTheContextTypeAndBuildsAgainstTheCore()
        {
            var document = AuthoringDocumentBuilder.Valid();
            var result = FyniteGraphCompiler.Compile(document);

            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.AreEqual(typeof(GraphTestContext), result.Binder.ContextType);

            var definition = result.Binder.Build(result.Graph, result.Blocks, result.Payloads);
            Assert.AreEqual(typeof(GraphTestContext), definition.ContextType);
            Assert.AreEqual("Root", definition.GetStateName(definition.Root));
        }

        [Test]
        public void PayloadTypesTravelAsTypedCarriersNotNames()
        {
            var result = FyniteGraphCompiler.Compile(AuthoringDocumentBuilder.Valid());
            Assert.IsTrue(result.Succeeded, Describe(result));

            int notify = System.Array.FindIndex(result.Graph.signals, s => s.guid == AuthoringDocumentBuilder.NotifyGuid);
            Assert.IsNotNull(result.Payloads[notify]);
            Assert.AreEqual(typeof(string), result.Payloads[notify].Type);
            Assert.AreEqual(typeof(FynitePayloadType<string>), result.Payloads[notify].GetType());

            int move = System.Array.FindIndex(result.Graph.signals, s => s.guid == AuthoringDocumentBuilder.MoveGuid);
            Assert.IsNull(result.Payloads[move], "a payload-less signal carries no type at all");
        }

        [Test]
        public void PrototypesCloneRatherThanBeingSharedBetweenMachines()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Block(
                document.FindState(AuthoringDocumentBuilder.IdleGuid).enter,
                typeof(GraphTestEnterAction), "b1", "{\"label\":\"x\"}");

            var result = FyniteGraphCompiler.Compile(document);
            var definition = (FyniteDefinition<GraphTestContext>)result.Binder.Build(result.Graph, result.Blocks, result.Payloads);

            var first = new GameObject("a").AddComponent<GraphTestContext>();
            var second = new GameObject("b").AddComponent<GraphTestContext>();

            try
            {
                var machineA = definition.CreateMachine(first);
                var machineB = definition.CreateMachine(second);

                machineA.Start();
                machineA.Stop();
                machineA.Start();
                machineB.Start();

                // The action counts its own runs, so a shared instance would show up as a bumped count.
                Assert.AreEqual("x#1,x#2", first.TraceText);
                Assert.AreEqual("x#1", second.TraceText);

                machineA.Dispose();
                machineB.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(first.gameObject);
                Object.DestroyImmediate(second.gameObject);
            }
        }

        [Test]
        public void CoreBuilderFailuresBecomeDiagnosticsRatherThanExceptions()
        {
            var document = AuthoringDocumentBuilder.Valid();

            // A leaf that declares itself initial while its parent is a leaf too is something the
            // document allows but the core builder rejects.
            document.states.Add(new FyniteStateDocument
            {
                guid = "extra",
                name = "Extra",
                parentGuid = AuthoringDocumentBuilder.MovingGuid
            });

            var result = FyniteGraphCompiler.Compile(document);

            Assert.IsFalse(result.Succeeded, Describe(result));
            Assert.IsTrue(result.Diagnostics.Any(d => d.IsError), Describe(result));
        }
    }
}
