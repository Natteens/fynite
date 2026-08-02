using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Fynite.Tests
{
    /// <summary>
    /// Runner behaviour that does not need the player loop. Unity does not call OnEnable in edit mode,
    /// so these drive the runner through its public API instead.
    /// </summary>
    [TestFixture]
    internal sealed class RunnerEditModeTests
    {
        private GameObject _gameObject;

        [SetUp]
        public void SetUp() => _gameObject = new GameObject("runner-host");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        [Test]
        public void MissingDefinitionIsReportedOnce()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has no definition assigned"));
            runner.StartMachine();

            // A second attempt must not spam the console.
            runner.StartMachine();

            Assert.IsNull(runner.Machine);
            Assert.IsFalse(runner.IsRunning);
        }

        [Test]
        public void MissingContextIsReported()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                runner.Bind(asset, null);

                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has no context assigned"));
                runner.StartMachine();

                Assert.IsNull(runner.Machine);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void IncompatibleContextIsReported()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var wrongContext = _gameObject.AddComponent<UnrelatedBehaviour>();
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                runner.Bind(asset, wrongContext);

                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("incompatible context"));
                runner.StartMachine();

                Assert.IsNull(runner.Machine);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void StartAndStopDriveTheMachine()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var context = _gameObject.AddComponent<RunnerTestContext>();
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                runner.Bind(asset, context);

                runner.StartMachine();
                Assert.IsTrue(runner.IsRunning);
                Assert.AreEqual(1, context.Entered);

                runner.StopMachine();
                Assert.IsFalse(runner.IsRunning);
                Assert.AreEqual(1, context.Exited);
                Assert.AreEqual(FyniteLifecycle.Stopped, runner.Machine.Lifecycle);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void StartingTwiceKeepsTheSameMachine()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var context = _gameObject.AddComponent<RunnerTestContext>();
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                runner.Bind(asset, context);
                runner.StartMachine();
                var machine = runner.Machine;

                runner.StartMachine();

                Assert.AreSame(machine, runner.Machine);
                Assert.AreEqual(1, context.Entered);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void EachRunnerOwnsItsOwnMachine()
        {
            var secondHost = new GameObject("second-host");
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                var firstRunner = _gameObject.AddComponent<FyniteRunner>();
                var firstContext = _gameObject.AddComponent<RunnerTestContext>();
                var secondRunner = secondHost.AddComponent<FyniteRunner>();
                var secondContext = secondHost.AddComponent<RunnerTestContext>();

                firstRunner.Bind(asset, firstContext);
                secondRunner.Bind(asset, secondContext);
                firstRunner.StartMachine();
                secondRunner.StartMachine();

                Assert.AreNotSame(firstRunner.Machine, secondRunner.Machine);

                firstRunner.Raise(RunnerTestDefinitionAsset.Advance);

                Assert.AreEqual("Working", firstRunner.Machine.Definition.GetStateName(firstRunner.Machine.ActiveLeaf));
                Assert.AreEqual("Ready", secondRunner.Machine.Definition.GetStateName(secondRunner.Machine.ActiveLeaf));
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(secondHost);
            }
        }

        [Test]
        public void SignalsAreForwardedToTheMachine()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var context = _gameObject.AddComponent<RunnerTestContext>();
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                runner.Bind(asset, context);
                runner.StartMachine();

                runner.Raise(RunnerTestDefinitionAsset.Advance);

                Assert.AreEqual(1, context.Advanced);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void RaisingBeforeTheMachineExistsThrowsAClearError()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();

            var error = Assert.Throws<InvalidOperationException>(() => runner.Raise(RunnerTestDefinitionAsset.Advance));
            StringAssert.Contains("has no machine yet", error.Message);
        }

        [Test]
        public void RebindingIsRejectedWhileRunning()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var context = _gameObject.AddComponent<RunnerTestContext>();
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                runner.Bind(asset, context);
                runner.StartMachine();

                var error = Assert.Throws<InvalidOperationException>(() => runner.Bind(asset, context));
                StringAssert.Contains("cannot be rebound", error.Message);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AssetRejectsAnIncompatibleContextDirectly()
        {
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                Assert.Throws<ArgumentNullException>(() => asset.CreateMachine(null));

                var error = Assert.Throws<ArgumentException>(() => asset.CreateMachine("not a context"));
                StringAssert.Contains("needs a context of type", error.Message);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AssetExposesItsContextType()
        {
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                Assert.AreEqual(typeof(RunnerTestContext), asset.ContextType);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AutoSerializesTheOnlyCompatibleLocalComponent()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var context = _gameObject.AddComponent<RunnerTestContext>();
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                Configure(runner, asset, false, null);
                Assert.AreSame(context, runner.Context);
                Assert.IsFalse(runner.ContextOverride);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AutoLeavesContextEmptyWhenThereIsNoCompatibleComponent()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                Configure(runner, asset, false, null);
                Assert.IsNull(runner.Context);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AutoKeepsAnExplicitLocalChoiceWhenSeveralAreCompatible()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            _gameObject.AddComponent<RunnerTestContext>();
            var selected = _gameObject.AddComponent<RunnerTestContext>();
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                Configure(runner, asset, false, selected);
                Assert.AreSame(selected, runner.Context);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void InterfaceAutoSupportsOneAndSeveralImplementations()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var first = _gameObject.AddComponent<RunnerInterfaceContextA>();
            var asset = ScriptableObject.CreateInstance<RunnerInterfaceDefinitionAsset>();

            try
            {
                Configure(runner, asset, false, null);
                Assert.AreSame(first, runner.Context, "one implementation resolves automatically");

                var second = _gameObject.AddComponent<RunnerInterfaceContextB>();
                Configure(runner, asset, false, second);
                Assert.AreSame(second, runner.Context, "with several implementations the explicit local choice is kept");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void OverrideMayUseAnotherGameObjectAndDisablingItReturnsToLocalAuto()
        {
            var other = new GameObject("external-context");
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var local = _gameObject.AddComponent<RunnerTestContext>();
            var external = other.AddComponent<RunnerTestContext>();
            var asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();

            try
            {
                Configure(runner, asset, true, external);
                Assert.AreSame(external, runner.Context);
                Assert.IsTrue(runner.ContextOverride);

                Configure(runner, asset, false, external);
                Assert.AreSame(local, runner.Context);
                Assert.IsFalse(runner.ContextOverride);
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void ChangingDefinitionInvalidatesAnIncompatibleSerializedContext()
        {
            var runner = _gameObject.AddComponent<FyniteRunner>();
            var concrete = _gameObject.AddComponent<RunnerTestContext>();
            var first = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();
            var second = ScriptableObject.CreateInstance<RunnerInterfaceDefinitionAsset>();

            try
            {
                Configure(runner, first, false, concrete);
                Assert.AreSame(concrete, runner.Context);
                Configure(runner, second, false, concrete);
                Assert.IsNull(runner.Context);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        private static void Configure(
            FyniteRunner runner, FyniteDefinitionAsset definition, bool useOverride, MonoBehaviour context)
        {
            var serialized = new SerializedObject(runner);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.FindProperty("contextOverride").boolValue = useOverride;
            serialized.FindProperty("context").objectReferenceValue = context;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            typeof(FyniteRunner)
                .GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(runner, null);
        }
    }
}
