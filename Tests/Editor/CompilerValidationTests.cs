using System.Linq;
using Fynite.Authoring;
using NUnit.Framework;

namespace Fynite.Tests
{
    /// <summary>
    /// Every structural rule the compiler enforces, and the element each failure is addressed to.
    /// </summary>
    /// <remarks>
    /// A diagnostic that says the right thing about the wrong element is nearly useless in the editor,
    /// where the whole point is being able to jump to what is broken. So these assert on the code
    /// <em>and</em> on the GUID it carries.
    /// </remarks>
    public sealed class CompilerValidationTests
    {
        private static FyniteCompilationResult Compile(FyniteGraphDocument document) =>
            FyniteGraphCompiler.Compile(document);

        private static void AssertError(FyniteCompilationResult result, string code, string elementGuid = null)
        {
            Assert.IsFalse(result.Succeeded, "compilation should have failed\n" + Describe(result));

            var matching = result.Diagnostics.Where(d => d.Code == code && d.IsError).ToList();
            Assert.IsNotEmpty(matching, "expected error " + code + "\n" + Describe(result));

            if (elementGuid != null)
            {
                Assert.IsTrue(
                    matching.Any(d => d.ElementGuid == elementGuid),
                    "expected " + code + " to point at '" + elementGuid + "'\n" + Describe(result));
            }
        }

        private static string Describe(FyniteCompilationResult result) => AuthoringDocumentTests.Describe(result);

        // --- the reference graph ----------------------------------------------------------------

        [Test]
        public void AValidGraphCompiles()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Reaction(
                document, "r1", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.MoveGuid,
                AuthoringDocumentBuilder.MovingGuid);

            var result = Compile(document);

            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.AreEqual(typeof(GraphTestContext), result.ContextType);
            Assert.AreEqual(3, result.Graph.states.Length);
            Assert.AreEqual(3, result.Graph.signals.Length);
        }

        [Test]
        public void ADeepHierarchyCompiles()
        {
            var document = AuthoringDocumentBuilder.Valid();

            string previous = AuthoringDocumentBuilder.MovingGuid;
            for (int depth = 0; depth < 5; depth++)
            {
                var guid = "deep-" + depth;
                document.states.Add(new FyniteStateDocument
                {
                    guid = guid,
                    name = "Deep" + depth,
                    parentGuid = previous,
                    isInitial = true
                });

                previous = guid;
            }

            var result = Compile(document);

            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.AreEqual(8, result.Graph.states.Length);
        }

        // --- graph and context ------------------------------------------------------------------

        [Test]
        public void AnEmptyGraphIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.states.Clear();

            AssertError(Compile(document), FyniteDiagnosticCodes.GraphEmpty);
        }

        [Test]
        public void AGraphWithoutIdentityIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.graphGuid = null;

            AssertError(Compile(document), FyniteDiagnosticCodes.GraphGuidMissing);
        }

        [Test]
        public void DuplicateIdentitiesAreRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.MovingGuid).guid = AuthoringDocumentBuilder.IdleGuid;

            AssertError(Compile(document), FyniteDiagnosticCodes.DuplicateGuid);
        }

        [Test]
        public void AnElementWithoutIdentityIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.MovingGuid).guid = null;

            AssertError(Compile(document), FyniteDiagnosticCodes.MissingGuid);
        }

        [Test]
        public void AGraphWithoutAContextIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.contextType = FyniteTypeReference.None;

            AssertError(Compile(document), FyniteDiagnosticCodes.ContextMissing);
        }

        [Test]
        public void AContextTypeThatNoLongerExistsIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.contextType = new FyniteTypeReference("Some.Deleted.Context, Nowhere");

            AssertError(Compile(document), FyniteDiagnosticCodes.ContextTypeNotFound);
        }

        [Test]
        public void AContextTypeTheRunnerCouldNeverSupplyIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.contextType = FyniteTypeReference.From(typeof(System.Text.StringBuilder));

            var result = Compile(document);

            Assert.IsFalse(result.Succeeded, Describe(result));
            Assert.IsTrue(
                result.Diagnostics.Any(d =>
                    d.Code == FyniteDiagnosticCodes.ContextNotRunnable ||
                    d.Code == FyniteDiagnosticCodes.ContextTypeInvalid),
                Describe(result));
        }

        // --- hierarchy --------------------------------------------------------------------------

        [Test]
        public void AGraphWithNoRootIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.RootGuid).isRoot = false;

            AssertError(Compile(document), FyniteDiagnosticCodes.RootMissing);
        }

        [Test]
        public void AGraphWithTwoRootsIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.MovingGuid).isRoot = true;

            AssertError(Compile(document), FyniteDiagnosticCodes.RootDuplicate, AuthoringDocumentBuilder.MovingGuid);
        }

        [Test]
        public void AStateWithAnEmptyNameIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.IdleGuid).name = "  ";

            AssertError(Compile(document), FyniteDiagnosticCodes.StateNameEmpty, AuthoringDocumentBuilder.IdleGuid);
        }

        [Test]
        public void AnOrphanStateIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.MovingGuid).parentGuid = null;

            AssertError(Compile(document), FyniteDiagnosticCodes.StateOrphan, AuthoringDocumentBuilder.MovingGuid);
        }

        [Test]
        public void AParentThatDoesNotExistIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.MovingGuid).parentGuid = "state-that-was-deleted";

            AssertError(Compile(document), FyniteDiagnosticCodes.ParentNotFound, AuthoringDocumentBuilder.MovingGuid);
        }

        [Test]
        public void AStateThatIsItsOwnParentIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.MovingGuid).parentGuid = AuthoringDocumentBuilder.MovingGuid;

            AssertError(Compile(document), FyniteDiagnosticCodes.ParentSelf, AuthoringDocumentBuilder.MovingGuid);
        }

        [Test]
        public void AnIndirectParentCycleIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();

            document.states.Add(new FyniteStateDocument { guid = "a", name = "A", parentGuid = "b" });
            document.states.Add(new FyniteStateDocument { guid = "b", name = "B", parentGuid = "c" });
            document.states.Add(new FyniteStateDocument { guid = "c", name = "C", parentGuid = "a" });

            AssertError(Compile(document), FyniteDiagnosticCodes.HierarchyCycle);
        }

        [Test]
        public void AStateUnreachableFromTheRootIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();

            // Two states parented to each other form an island the root cannot reach.
            document.states.Add(new FyniteStateDocument { guid = "island-a", name = "IslandA", parentGuid = "island-b", isInitial = true });
            document.states.Add(new FyniteStateDocument { guid = "island-b", name = "IslandB", parentGuid = "island-a", isInitial = true });

            var result = Compile(document);

            Assert.IsFalse(result.Succeeded, Describe(result));
            Assert.IsTrue(
                result.Diagnostics.Any(d =>
                    d.Code == FyniteDiagnosticCodes.StateUnreachable || d.Code == FyniteDiagnosticCodes.HierarchyCycle),
                Describe(result));
        }

        [Test]
        public void ACompositeWithoutAnInitialChildIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.IdleGuid).isInitial = false;

            AssertError(Compile(document), FyniteDiagnosticCodes.InitialMissing, AuthoringDocumentBuilder.RootGuid);
        }

        [Test]
        public void ACompositeWithTwoInitialChildrenIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.MovingGuid).isInitial = true;

            AssertError(Compile(document), FyniteDiagnosticCodes.InitialDuplicate, AuthoringDocumentBuilder.MovingGuid);
        }

        // --- signals ----------------------------------------------------------------------------

        [Test]
        public void ASignalWithAnEmptyNameIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindSignal(AuthoringDocumentBuilder.MoveGuid).name = string.Empty;

            AssertError(Compile(document), FyniteDiagnosticCodes.SignalNameEmpty, AuthoringDocumentBuilder.MoveGuid);
        }

        [Test]
        public void DuplicateSignalNamesAreWarnedAboutButStillCompile()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindSignal(AuthoringDocumentBuilder.StopGuid).name = "Move";

            var result = Compile(document);

            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.IsTrue(
                result.Diagnostics.Any(d => d.Code == FyniteDiagnosticCodes.SignalNameDuplicate),
                Describe(result));
        }

        [Test]
        public void APayloadTypeThatNoLongerExistsIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindSignal(AuthoringDocumentBuilder.NotifyGuid).payloadType =
                new FyniteTypeReference("Some.Deleted.Payload, Nowhere");

            AssertError(Compile(document), FyniteDiagnosticCodes.PayloadTypeNotFound, AuthoringDocumentBuilder.NotifyGuid);
        }

        [Test]
        public void DeletingASignalLeavesItsReactionReported()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Reaction(
                document, "r1", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.MoveGuid,
                AuthoringDocumentBuilder.MovingGuid);

            document.signals.RemoveAll(s => s.guid == AuthoringDocumentBuilder.MoveGuid);

            AssertError(Compile(document), FyniteDiagnosticCodes.ReactionSignalNotFound, "r1");
        }

        // --- reactions --------------------------------------------------------------------------

        [Test]
        public void AReactionWithNoSourceIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Reaction(document, "r1", null, AuthoringDocumentBuilder.MoveGuid, null);

            AssertError(Compile(document), FyniteDiagnosticCodes.ReactionSourceMissing, "r1");
        }

        [Test]
        public void AReactionWhoseSourceIsGoneIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Reaction(document, "r1", "state-deleted", AuthoringDocumentBuilder.MoveGuid, null);

            AssertError(Compile(document), FyniteDiagnosticCodes.ReactionSourceNotFound, "r1");
        }

        [Test]
        public void AReactionWithNoSignalIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Reaction(document, "r1", AuthoringDocumentBuilder.IdleGuid, null, null);

            AssertError(Compile(document), FyniteDiagnosticCodes.ReactionSignalMissing, "r1");
        }

        [Test]
        public void AReactionWhoseTargetIsGoneIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Reaction(
                document, "r1", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.MoveGuid, "state-deleted");

            AssertError(Compile(document), FyniteDiagnosticCodes.ReactionTargetNotFound, "r1");
        }

        [Test]
        public void AReactionWithNoTargetIsValidAndCompilesToNoTransition()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Reaction(document, "r1", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.MoveGuid, null);

            var result = Compile(document);

            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.AreEqual(-1, result.Graph.reactions[0].target);
        }

        [Test]
        public void TwoReactionsSharingAnOrderAreWarnedAbout()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Reaction(document, "r1", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.MoveGuid, null, order: 3);
            AuthoringDocumentBuilder.Reaction(document, "r2", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.StopGuid, null, order: 3);

            var result = Compile(document);

            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.IsTrue(
                result.Diagnostics.Any(d => d.Code == FyniteDiagnosticCodes.ReactionOrderAmbiguous),
                Describe(result));
        }

        // --- blocks -----------------------------------------------------------------------------

        [Test]
        public void ABlockWithNoTypeSelectedIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.IdleGuid).enter.Add(new FyniteBlockDocument { guid = "b1" });

            AssertError(Compile(document), FyniteDiagnosticCodes.BlockTypeMissing, "b1");
        }

        [Test]
        public void ABlockTypeThatNoLongerExistsIsRejectedRatherThanSubstituted()
        {
            var document = AuthoringDocumentBuilder.Valid();
            document.FindState(AuthoringDocumentBuilder.IdleGuid).enter.Add(new FyniteBlockDocument
            {
                guid = "b1",
                blockType = new FyniteTypeReference("Some.Renamed.Action, Nowhere")
            });

            AssertError(Compile(document), FyniteDiagnosticCodes.BlockTypeNotFound, "b1");
        }

        [Test]
        public void AnAbstractBlockTypeIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Block(
                document.FindState(AuthoringDocumentBuilder.IdleGuid).enter,
                typeof(FyniteAction<GraphTestContext>),
                "b1");

            AssertError(Compile(document), FyniteDiagnosticCodes.BlockTypeNotConstructible, "b1");
        }

        [Test]
        public void AnInterfaceAsABlockTypeIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Block(
                document.FindState(AuthoringDocumentBuilder.IdleGuid).enter,
                typeof(IFyniteAction<GraphTestContext>),
                "b1");

            AssertError(Compile(document), FyniteDiagnosticCodes.BlockTypeNotConstructible, "b1");
        }

        [Test]
        public void AnOpenGenericBlockTypeIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Block(
                document.FindState(AuthoringDocumentBuilder.IdleGuid).enter,
                typeof(FyniteAction<>),
                "b1");

            AssertError(Compile(document), FyniteDiagnosticCodes.BlockTypeNotConstructible, "b1");
        }

        [Test]
        public void AGuardUsedAsAnActionIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Block(
                document.FindState(AuthoringDocumentBuilder.IdleGuid).enter, typeof(GraphTestGuard), "b1");

            AssertError(Compile(document), FyniteDiagnosticCodes.BlockWrongKind, "b1");
        }

        [Test]
        public void AnActionUsedAsAGuardIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            var reaction = AuthoringDocumentBuilder.Reaction(
                document, "r1", AuthoringDocumentBuilder.IdleGuid, AuthoringDocumentBuilder.MoveGuid, null);

            AuthoringDocumentBuilder.Block(reaction.guards, typeof(GraphTestTickAction), "b1");

            AssertError(Compile(document), FyniteDiagnosticCodes.BlockWrongKind, "b1");
        }

        [Test]
        public void ABlockForAnotherContextIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Block(
                document.FindState(AuthoringDocumentBuilder.IdleGuid).enter, typeof(GraphTestUnrelatedAction), "b1");

            AssertError(Compile(document), FyniteDiagnosticCodes.BlockContextIncompatible, "b1");
        }

        [Test]
        public void AnIncompatibleBlockIsToldWhatWouldFit()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Block(
                document.FindState(AuthoringDocumentBuilder.IdleGuid).enter, typeof(GraphTestUnrelatedAction), "b1");

            var diagnostic = Compile(document).Diagnostics
                .First(d => d.Code == FyniteDiagnosticCodes.BlockContextIncompatible);

            Assert.IsTrue(
                diagnostic.Message.Contains(nameof(GraphTestTickAction)),
                "the message should name a block that would fit, but was: " + diagnostic.Message);
        }

        [Test]
        public void StaleConfigurationIsCarriedOverAsFarAsItFitsAndReported()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Block(
                document.FindState(AuthoringDocumentBuilder.IdleGuid).enter,
                typeof(GraphTestEnterAction),
                "b1",
                "{\"label\":\"kept\",\"fieldThatWasRemoved\":7}");

            var result = Compile(document);

            Assert.IsTrue(result.Succeeded, Describe(result));

            var prototype = (GraphTestEnterAction)result.Blocks[0];
            var applied = UnityEngine.JsonUtility.ToJson(prototype);
            Assert.IsTrue(applied.Contains("kept"), "the field that still exists keeps its value: " + applied);
        }

        [Test]
        public void ABlockOccurrenceWithoutIdentityIsRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            AuthoringDocumentBuilder.Block(
                document.FindState(AuthoringDocumentBuilder.IdleGuid).enter, typeof(GraphTestEnterAction), null);

            AssertError(Compile(document), FyniteDiagnosticCodes.MissingGuid);
        }

        [Test]
        public void TwoBlockOccurrencesSharingAnIdentityAreRejected()
        {
            var document = AuthoringDocumentBuilder.Valid();
            var idle = document.FindState(AuthoringDocumentBuilder.IdleGuid);

            AuthoringDocumentBuilder.Block(idle.enter, typeof(GraphTestEnterAction), "same");
            AuthoringDocumentBuilder.Block(idle.tick, typeof(GraphTestTickAction), "same");

            AssertError(Compile(document), FyniteDiagnosticCodes.DuplicateGuid, "same");
        }
    }
}
