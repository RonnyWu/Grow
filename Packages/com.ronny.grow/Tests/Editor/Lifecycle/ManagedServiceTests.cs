// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Threading;
using Grow.Core.Log;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Lifecycle.Impl;
using NUnit.Framework;

namespace Grow.Tests.Editor.Lifecycle {
    public class ManagedServiceTests {
        private FakeMonoExecutor _executor;

        [SetUp]
        public void SetUp() {
            MonoManager.ResetStaticState();
            GrowLog.SetSink(new NullLogSink());
            _executor = new FakeMonoExecutor();
            MonoManager.Instance.Bind(_executor);
            CountingService.ResetStaticState();
            RetryService.ResetStaticState();
        }

        [TearDown]
        public void TearDown() {
            if (CountingService.HasInstance) CountingService.Instance.Dispose();
            if (RetryService.HasInstance) RetryService.Instance.Dispose();
            CountingService.ResetStaticState();
            RetryService.ResetStaticState();
            MonoManager.ResetStaticState();
            _executor?.Dispose();
            GrowLog.Reset();
        }

        [Test]
        public void Service_IsAssignableToContainerFacets() {
            var service = CountingService.Instance;

            Assert.IsInstanceOf<IManagedService>(service);
            Assert.IsInstanceOf<IInitializable>(service);
            Assert.IsInstanceOf<IStartable>(service);
            Assert.IsInstanceOf<IDeinitializable>(service);
        }

        [Test]
        public void Constructor_RunsInitializeContractsAndStartInOrder() {
            var service = CountingService.Instance;

            Assert.AreEqual(1, service.InitializeCount);
            Assert.AreEqual(1, service.ContractsCount);
            Assert.AreEqual(1, service.StartCount);
            Assert.AreEqual(0, service.TeardownCount);
            Assert.AreEqual(1, _executor.Update.Count);
        }

        [Test]
        public void ContainerInitializeAndStart_Repeated_ExecuteOnce() {
            var service = CountingService.Instance;

            ((IInitializable)service).OnInitialize();
            ((IInitializable)service).OnInitialize();
            ((IStartable)service).OnStart();
            ((IStartable)service).OnStart();

            Assert.AreEqual(1, service.InitializeCount);
            Assert.AreEqual(1, service.ContractsCount);
            Assert.AreEqual(1, service.StartCount);
        }

        [Test]
        public void ContainerDeinitialize_Repeated_AndDispose_ExecuteTeardownOnce() {
            var service = CountingService.Instance;

            ((IDeinitializable)service).OnDeinitialize();
            ((IDeinitializable)service).OnDeinitialize();

            Assert.AreEqual(1, service.TeardownCount);
            Assert.AreEqual(1, service.ResetCount);

            service.Dispose();

            Assert.AreEqual(1, service.TeardownCount);
            Assert.AreEqual(1, service.ResetCount);
            Assert.IsFalse(CountingService.HasInstance);
        }

        [Test]
        public void InitializeFailure_SkipsStart_AndCanRetry() {
            RetryService.ThrowInitialize = true;
            var service = RetryService.Instance;

            Assert.AreEqual(0, service.InitializeCount);
            Assert.AreEqual(1, service.ContractsCount, "contracts are independent from initialization");
            Assert.AreEqual(0, service.StartCount, "start must wait for a successful initialization");

            RetryService.ThrowInitialize = false;
            ((IInitializable)service).OnInitialize();

            Assert.AreEqual(1, service.InitializeCount);
            Assert.AreEqual(1, service.ContractsCount);
            Assert.AreEqual(0, service.StartCount);

            ((IStartable)service).OnStart();
            Assert.AreEqual(1, service.StartCount);
        }

        [Test]
        public void StartFailure_RetriesOnce() {
            RetryService.ThrowStart = true;
            var service = RetryService.Instance;

            Assert.AreEqual(1, service.InitializeCount);
            Assert.AreEqual(0, service.StartCount);

            RetryService.ThrowStart = false;
            ((IStartable)service).OnStart();

            Assert.AreEqual(1, service.StartCount);
        }

        [Test]
        public void TeardownFailure_StillResetsStatics_AndRetries() {
            RetryService.ThrowTeardown = true;
            var service = RetryService.Instance;

            Assert.DoesNotThrow(() => ((IDeinitializable)service).OnDeinitialize());
            Assert.AreEqual(1, service.ResetCount, "static reset must run even when teardown throws");

            RetryService.ThrowTeardown = false;
            ((IDeinitializable)service).OnDeinitialize();
            Assert.AreEqual(1, service.TeardownCount);
        }

        [Test]
        public void PostToMain_OnMainThread_RunsInline() {
            var service = CountingService.Instance;
            var ran = false;

            service.QueueForMain(() => ran = true);

            Assert.IsTrue(ran);
        }

        [Test]
        public void PostToMain_FromWorker_FlushesBeforeServiceUpdate() {
            var service = CountingService.Instance;
            var order = new List<string>();

            var worker = new Thread(() => service.QueueForMain(() => order.Add("queued")));
            worker.Start();
            worker.Join();

            service.OrderForTest = order;
            ((ILifecycle)service).OnUpdate();

            CollectionAssert.AreEqual(new[] { "queued", "update" }, order);
        }

        [Test]
        public void PostToMain_FromWorker_WithoutUpdateFlag_IsRejected() {
            LifecycleSession.ResetForTests();
            var service = new NoUpdateService();
            var ran = false;

            var worker = new Thread(() => service.QueueForMain(() => ran = true));
            worker.Start();
            worker.Join();

            Assert.IsFalse(ran, "a service without UpdateFlag.Update must not execute worker actions inline");
            service.Dispose();
        }

        [Test]
        public void Singleton_InstanceIsStable_AndDisposeAllowsRecreation() {
            var first = CountingService.Instance;

            Assert.AreSame(first, CountingService.Instance);
            Assert.IsTrue(CountingService.HasInstance);

            first.Dispose();

            Assert.IsFalse(CountingService.HasInstance);
            Assert.AreNotSame(first, CountingService.Instance);
        }

        [Test]
        public void Singleton_NewSession_DropsInstance() {
            var first = CountingService.Instance;

            LifecycleSession.ResetForTests();

            Assert.IsFalse(CountingService.HasInstance);
            Assert.AreNotSame(first, CountingService.Instance);
        }

        [Test]
        public void Singleton_StaleInstanceDispose_DoesNotClearTheNewInstance() {
            var stale = CountingService.Instance;
            LifecycleSession.ResetForTests();
            var fresh = CountingService.Instance;

            stale.Dispose();

            Assert.AreSame(fresh, CountingService.Instance);
            Assert.IsTrue(CountingService.HasInstance);
        }

        [Test]
        public void Singleton_AfterAppQuit_DoesNotRecreate() {
            var service = CountingService.Instance;

            ((ILifecycle)service).OnAppQuit();

            Assert.IsFalse(CountingService.HasInstance);
            Assert.IsNull(CountingService.Instance);
        }

        private sealed class CountingService : UnifiedClassService<CountingService> {
            public int InitializeCount { get; private set; }
            public int ContractsCount { get; private set; }
            public int StartCount { get; private set; }
            public int TeardownCount { get; private set; }
            public int ResetCount { get; private set; }

            public List<string> OrderForTest { get; set; }

            public CountingService() {
            }

            protected override UpdateFlag UpdateFlag => UpdateFlag.Update;

            public void QueueForMain(System.Action action) {
                PostToMain(action);
            }

            protected override void OnServiceInitialize() {
                InitializeCount++;
            }

            protected override void OnServiceContractsRegister() {
                ContractsCount++;
            }

            protected override void OnServiceStart() {
                StartCount++;
            }

            protected override void OnServiceUpdate() {
                OrderForTest?.Add("update");
            }

            protected override void OnServiceTeardown() {
                TeardownCount++;
            }

            protected override void ResetStatics() {
                ResetCount++;
            }
        }

        private sealed class NoUpdateService : UnifiedClassService<NoUpdateService> {
            public NoUpdateService() {
            }

            public void QueueForMain(System.Action action) {
                PostToMain(action);
            }
        }

        private sealed class RetryService : UnifiedClassService<RetryService> {
            public static bool ThrowInitialize;
            public static bool ThrowStart;
            public static bool ThrowTeardown;

            public int InitializeCount { get; private set; }
            public int ContractsCount { get; private set; }
            public int StartCount { get; private set; }
            public int TeardownCount { get; private set; }
            public int ResetCount { get; private set; }

            public RetryService() {
            }

            protected override void OnServiceInitialize() {
                if (ThrowInitialize) throw new System.InvalidOperationException("initialize");
                InitializeCount++;
            }

            protected override void OnServiceContractsRegister() {
                ContractsCount++;
            }

            protected override void OnServiceStart() {
                if (ThrowStart) throw new System.InvalidOperationException("start");
                StartCount++;
            }

            protected override void OnServiceTeardown() {
                if (ThrowTeardown) throw new System.InvalidOperationException("teardown");
                TeardownCount++;
            }

            protected override void ResetStatics() {
                ResetCount++;
            }
        }
    }
}
