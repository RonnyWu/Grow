// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using Grow.Core.Log;
using Grow.Kernel.Entry;
using Grow.Kernel.Ioc;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Loop;
using Grow.Kernel.Modules;
using Grow.Kernel.Scheduling;
using NUnit.Framework;

namespace Grow.Tests.Editor.Entry {
    [Category("Grow.Integration")]
    public class GrowAppAsyncBootTests {
        [SetUp]
        public void SetUp() {
            GrowLog.SetSink(new AsyncBootNullLogSink());
            GrowApp.Shutdown();
            GrowApp.ResetStaticState();
            GrowLoop.ResetStaticState();
            MonoManager.ResetStaticState();
            GrowScheduler.ResetStaticState();
            GrowModules.ResetStaticState();
            IOC.Clear();
            GrowBoot.Shutdown();
        }

        [TearDown]
        public void TearDown() {
            GrowApp.Shutdown();
            GrowApp.ResetStaticState();
            GrowLoop.ResetStaticState();
            MonoManager.ResetStaticState();
            GrowScheduler.ResetStaticState();
            GrowModules.ResetStaticState();
            IOC.Clear();
            GrowBoot.Shutdown();
            GrowLog.Reset();
        }

        [Test]
        public void AsyncBoot_ReportsLoadingThenReady() {
            var module = new AsyncProbeModule("Slow", YieldTwice);
            GrowApp.Initialize(new IModule[] { module });

            Assert.AreEqual(GrowState.Loading, GrowBoot.State);
            Assert.IsFalse(GrowApp.IsInitialized);
            Assert.AreEqual(0, module.StartCount);
            Assert.Less(GrowBoot.Progress, 1f);

            DrivePump();

            Assert.AreEqual(GrowState.Ready, GrowBoot.State);
            Assert.IsTrue(GrowApp.IsInitialized);
            Assert.AreEqual(1, module.StartCount);
            Assert.AreEqual(1f, GrowBoot.Progress);
        }

        [Test]
        public void AsyncBoot_PublishesReadyOnlyAfterTheStartPass() {
            var module = new AsyncProbeModule("Slow", YieldTwice);
            var startedAtReady = -1;
            var subscription = GrowBoot.StateChanged.Subscribe(change => {
                if (change.Next == GrowState.Ready) {
                    startedAtReady = module.StartCount;
                }
            });

            try {
                GrowApp.Initialize(new IModule[] { module });
                DrivePump();
            } finally {
                subscription.Dispose();
            }

            Assert.AreEqual(1, startedAtReady);
        }

        [Test]
        public void SyncBoot_ReachesReadyWithoutThePump() {
            var module = new AsyncProbeModule("Sync", () => null);
            GrowApp.Initialize(new IModule[] { module });

            Assert.AreEqual(GrowState.Ready, GrowBoot.State);
            Assert.IsTrue(GrowApp.IsInitialized);
            Assert.AreEqual(1f, GrowBoot.Progress);
            Assert.AreEqual(1, module.StartCount);
        }

        [Test]
        public void LoadFailureDuringThePump_PublishesFailedThenShutsDown() {
            var module = new AsyncProbeModule("Broken", ThrowAfterYield);
            var states = new List<GrowState>();
            var subscription = GrowBoot.StateChanged.Subscribe(change => states.Add(change.Next));

            try {
                GrowApp.Initialize(new IModule[] { module });
                Assert.AreEqual(GrowState.Loading, GrowBoot.State);

                DrivePump();
            } finally {
                subscription.Dispose();
            }

            CollectionAssert.Contains(states, GrowState.Failed);
            Assert.AreEqual(GrowState.Shutdown, GrowBoot.State);
            StringAssert.Contains("boom", GrowBoot.FailureMessage);
            Assert.AreEqual(0, module.StartCount);
            Assert.IsFalse(GrowApp.IsInitialized);
            Assert.IsFalse(GrowLoop.IsInstalled, "a failed boot rolls the composition back");
        }

        [Test]
        public void LoadThrowDuringInitialize_FailsTheBoot() {
            var module = new AsyncProbeModule(
                "Broken", () => throw new InvalidOperationException("immediate boom"));
            var states = new List<GrowState>();
            var subscription = GrowBoot.StateChanged.Subscribe(change => states.Add(change.Next));

            try {
                GrowApp.Initialize(new IModule[] { module });
            } finally {
                subscription.Dispose();
            }

            CollectionAssert.Contains(states, GrowState.Failed);
            Assert.AreEqual(GrowState.Shutdown, GrowBoot.State);
            StringAssert.Contains("immediate boom", GrowBoot.FailureMessage);
            Assert.AreEqual(0, module.StartCount);
            Assert.IsFalse(GrowApp.IsInitialized);
        }

        [Test]
        public void InitializeWhileLoading_DoesNotRestartTheBoot() {
            var module = new AsyncProbeModule("Slow", YieldTwice);
            GrowApp.Initialize(new IModule[] { module });

            GrowApp.Initialize();

            Assert.AreEqual(GrowState.Loading, GrowBoot.State);
            Assert.AreEqual(1, module.InitializeCount);

            DrivePump();

            Assert.AreEqual(GrowState.Ready, GrowBoot.State);
            Assert.AreEqual(1, module.InitializeCount);
            Assert.AreEqual(1, module.StartCount);
        }

        [Test]
        public void ShutdownWhileLoading_RollsBackWithoutStarting() {
            var module = new AsyncProbeModule("Slow", YieldTwice);
            GrowApp.Initialize(new IModule[] { module });
            Assert.AreEqual(GrowState.Loading, GrowBoot.State);

            GrowApp.Shutdown();

            Assert.AreEqual(GrowState.Shutdown, GrowBoot.State);
            Assert.AreEqual(0, module.StartCount);
            Assert.IsFalse(GrowLoop.IsInstalled);
            Assert.IsFalse(GrowApp.IsInitialized);
        }

        [Test]
        public void FailureMessage_IsClearedByTheNextInitialize() {
            var broken = new AsyncProbeModule("Broken", ThrowAfterYield);
            GrowApp.Initialize(new IModule[] { broken });
            DrivePump();
            StringAssert.Contains("boom", GrowBoot.FailureMessage);

            GrowModules.ResetStaticState();
            var healthy = new AsyncProbeModule("Sync", () => null);
            GrowApp.Initialize(new IModule[] { healthy });

            Assert.IsNull(GrowBoot.FailureMessage);
            Assert.AreEqual(GrowState.Ready, GrowBoot.State);
        }

        [Test]
        public void Progress_IsZeroOutsideTheBootAndNotOneAfterAFailure() {
            Assert.AreEqual(0f, GrowBoot.Progress, "nothing has booted yet");

            var module = new AsyncProbeModule("Broken", ThrowAfterYield);
            GrowApp.Initialize(new IModule[] { module });
            DrivePump();

            Assert.AreEqual(GrowState.Shutdown, GrowBoot.State);
            Assert.AreEqual(0f, GrowBoot.Progress, "a rolled-back boot must not report 100%");
        }

        private static void DrivePump() {
            for (var i = 0; i < 64 && GrowBoot.State == GrowState.Loading; i++) {
                GrowApp.PumpBoot();
            }
        }

        private static IEnumerator YieldTwice() {
            yield return null;
            yield return null;
        }

        private static IEnumerator ThrowAfterYield() {
            yield return null;
            throw new InvalidOperationException("boom");
        }

        private sealed class AsyncProbeModule : IModule, IBootLoadable {
            private readonly Func<IEnumerator> _load;

            internal AsyncProbeModule(string name, Func<IEnumerator> load) {
                Name = name;
                _load = load;
            }

            public string Name { get; }

            public int Priority => 0;

            public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

            public float LoadProgress { get; set; }

            public int InitializeCount { get; private set; }

            public int StartCount { get; private set; }

            public void OnRegister(IModuleContext context) {
            }

            public void OnInitialize(IModuleContext context) {
                InitializeCount++;
            }

            public void OnStart(IModuleContext context) {
                StartCount++;
            }

            public void OnStop() {
            }

            public void OnDispose() {
            }

            public IEnumerator Load(IModuleContext context) {
                return _load();
            }
        }

        private sealed class AsyncBootNullLogSink : ILogSink {
            public void Write(in LogEntry entry) {
            }
        }
    }
}
