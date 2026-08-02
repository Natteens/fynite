using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
    }
}
