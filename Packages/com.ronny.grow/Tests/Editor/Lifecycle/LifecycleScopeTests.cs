// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Log;
using Grow.Kernel.Lifecycle;
using NUnit.Framework;

namespace Grow.Tests.Editor.Lifecycle {
    public class LifecycleScopeTests {
        private FakeMonoExecutor _executor;
        private LifecycleScope _scope;

        [SetUp]
        public void SetUp() {
            MonoManager.ResetStaticState();
            GrowLog.SetSink(new NullLogSink());
            _executor = new FakeMonoExecutor();
            MonoManager.Instance.Bind(_executor);
            _scope = new LifecycleScope();
            OrderProbe.DeinitOrder.Clear();
        }

        [TearDown]
        public void TearDown() {
            _scope?.Dispose();
            _scope = null;
            MonoManager.ResetStaticState();
            _executor?.Dispose();
            GrowLog.Reset();
        }

        [Test]
        public void Constructor_NullManager_Throws() {
            Assert.Throws<ArgumentNullException>(() => new LifecycleScope(null));
        }

        [Test]
        public void Attach_Null_Throws() {
            Assert.Throws<ArgumentNullException>(() => _scope.Attach(null));
            Assert.Throws<ArgumentNullException>(() => _scope.AttachApp(null));
        }

        [Test]
        public void Attach_AfterDispose_Throws_AndDetachIsSafe() {
            _scope.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _scope.Attach(new UpdateProbe()));
            Assert.Throws<ObjectDisposedException>(() => _scope.AttachApp(new AppProbe()));
            Assert.DoesNotThrow(() => _scope.Detach(new UpdateProbe()));
            Assert.DoesNotThrow(() => _scope.DetachApp(new AppProbe()));
            Assert.DoesNotThrow(() => _scope.Dispose());
        }

        [Test]
        public void Attach_RegistersUpdate_And_DetachUnregisters() {
            var probe = new UpdateProbe();

            _scope.Attach(probe);
            _executor.PublishUpdate();
            Assert.AreEqual(1, probe.UpdateCount);

            _scope.Detach(probe);
            _executor.PublishUpdate();
            Assert.AreEqual(1, probe.UpdateCount);
        }

        [Test]
        public void Attach_InvokesInitializeAndStartOnce_WhenRepeated() {
            var probe = new LifecycleProbe();

            _scope.Attach(probe);
            _scope.Attach(probe);

            Assert.AreEqual(1, probe.InitializeCount);
            Assert.AreEqual(1, probe.StartCount);
        }

        [Test]
        public void Attach_RoutesAllFiveUpdatePhases() {
            var probe = new LifecycleProbe();
            _scope.Attach(probe);

            _executor.PublishPreUpdate();
            _executor.PublishFixedUpdate();
            _executor.PublishUpdate();
            _executor.PublishPostUpdate();
            _executor.PublishLateUpdate();

            Assert.AreEqual(1, probe.PreUpdateCount);
            Assert.AreEqual(1, probe.FixedUpdateCount);
            Assert.AreEqual(1, probe.UpdateCount);
            Assert.AreEqual(1, probe.PostUpdateCount);
            Assert.AreEqual(1, probe.LateUpdateCount);

            _scope.Detach(probe);
            _executor.PublishPreUpdate();
            _executor.PublishFixedUpdate();
            _executor.PublishUpdate();
            _executor.PublishPostUpdate();
            _executor.PublishLateUpdate();

            Assert.AreEqual(1, probe.PreUpdateCount);
            Assert.AreEqual(1, probe.FixedUpdateCount);
            Assert.AreEqual(1, probe.UpdateCount);
            Assert.AreEqual(1, probe.PostUpdateCount);
            Assert.AreEqual(1, probe.LateUpdateCount);
        }

        [Test]
        public void AttachApp_RoutesAppEvents_And_DetachAppUnregisters() {
            var probe = new AppProbe();
            _scope.AttachApp(probe);

            _executor.PublishAppFocus(true);
            _executor.PublishAppPause(true);
            _executor.PublishAppQuit();

            Assert.AreEqual(1, probe.FocusCount);
            Assert.IsTrue(probe.LastFocus);
            Assert.AreEqual(1, probe.PauseCount);
            Assert.IsTrue(probe.LastPause);
            Assert.AreEqual(1, probe.QuitCount);

            _scope.DetachApp(probe);
            _executor.PublishAppFocus(false);
            _executor.PublishAppPause(false);
            _executor.PublishAppQuit();

            Assert.AreEqual(1, probe.FocusCount);
            Assert.AreEqual(1, probe.PauseCount);
            Assert.AreEqual(1, probe.QuitCount);
        }

        [Test]
        public void AttachApp_NonAwareTarget_IsIgnored() {
            Assert.DoesNotThrow(() => _scope.AttachApp(new UpdateProbe()));
        }

        [Test]
        public void AttachApp_AfterDetachApp_CanReattach() {
            var probe = new AppProbe();
            _scope.AttachApp(probe);
            _scope.DetachApp(probe);
            _scope.AttachApp(probe);

            _executor.PublishAppQuit();
            Assert.AreEqual(1, probe.QuitCount);
        }

        [Test]
        public void Dispose_IsIdempotent_AndDetachesInReverseOrder() {
            _scope.Attach(new OrderProbe("A"));
            _scope.Attach(new OrderProbe("B"));
            _scope.Attach(new OrderProbe("C"));

            _scope.Dispose();
            _scope.Dispose();

            CollectionAssert.AreEqual(new[] { "C", "B", "A" }, OrderProbe.DeinitOrder);
        }

        [Test]
        public void Dispose_UnregistersAllUpdateCallbacks() {
            var first = new UpdateProbe();
            var second = new UpdateProbe();
            _scope.Attach(first);
            _scope.Attach(second);

            _scope.Dispose();

            _executor.PublishUpdate();
            Assert.AreEqual(0, first.UpdateCount);
            Assert.AreEqual(0, second.UpdateCount);
        }

        [Test]
        public void Dispose_ReleasesAppSubscriptions() {
            var probe = new AppProbe();
            _scope.AttachApp(probe);

            _scope.Dispose();

            _executor.PublishAppQuit();
            Assert.AreEqual(0, probe.QuitCount);
        }

        [Test]
        public void Detach_IsIdempotent_AndDeinitializesOnce() {
            var probe = new LifecycleProbe();
            _scope.Attach(probe);

            _scope.Detach(probe);
            _scope.Detach(probe);

            Assert.AreEqual(1, probe.DeinitializeCount);
        }

        [Test]
        public void Attach_AfterDetach_CanReinitialize() {
            var probe = new LifecycleProbe();
            _scope.Attach(probe);
            _scope.Detach(probe);
            _scope.Attach(probe);

            Assert.AreEqual(2, probe.InitializeCount);
            Assert.AreEqual(2, probe.StartCount);
            Assert.AreEqual(1, probe.DeinitializeCount);
        }

        [Test]
        public void Attach_AfterHostRebind_ResubscribesWithoutReinitializing() {
            var probe = new LifecycleProbe();
            _scope.Attach(probe);
            _executor.PublishUpdate();
            Assert.AreEqual(1, probe.UpdateCount);

            var next = new FakeMonoExecutor();
            MonoManager.Instance.Bind(next);

            _scope.Attach(probe);
            next.PublishUpdate();

            Assert.AreEqual(2, probe.UpdateCount);
            Assert.AreEqual(1, probe.InitializeCount, "a host rebind must not rerun lifecycle hooks");
            Assert.AreEqual(1, probe.StartCount);

            next.Dispose();
        }

        [Test]
        public void Attach_InitializeThrows_DisposeSkipsDeinitialize() {
            var probe = new RetryProbe { InitializeThrows = true };
            var scope = new LifecycleScope();

            Assert.IsFalse(scope.Attach(probe));
            scope.Dispose();

            Assert.AreEqual(0, probe.DeinitializeCount,
                "a target whose initialization never succeeded must not be deinitialized");
        }

        [Test]
        public void Attach_InitializeThrows_CanRetry_WithoutRepeatingSuccessfulSteps() {
            var probe = new RetryProbe { InitializeThrows = true };

            Assert.DoesNotThrow(() => _scope.Attach(probe));
            Assert.AreEqual(0, probe.InitializeCount);
            Assert.AreEqual(0, probe.StartCount);

            probe.InitializeThrows = false;
            _scope.Attach(probe);

            Assert.AreEqual(1, probe.InitializeCount);
            Assert.AreEqual(1, probe.StartCount);

            _executor.PublishUpdate();
            Assert.AreEqual(1, probe.UpdateCount);

            _scope.Attach(probe);
            Assert.AreEqual(1, probe.InitializeCount);
            Assert.AreEqual(1, probe.StartCount);
        }

        [Test]
        public void Attach_StartThrows_Retry_SkipsSuccessfulInitialize() {
            var probe = new RetryProbe { StartThrows = true };

            Assert.DoesNotThrow(() => _scope.Attach(probe));
            Assert.AreEqual(1, probe.InitializeCount);
            Assert.AreEqual(0, probe.StartCount);

            probe.StartThrows = false;
            _scope.Attach(probe);

            Assert.AreEqual(1, probe.InitializeCount);
            Assert.AreEqual(1, probe.StartCount);
        }

        [Test]
        public void Dispose_DeinitializeThrows_StillUnregistersOthers() {
            var first = new UpdateProbe();
            var thrower = new ThrowingDeinitializeProbe();
            _scope.Attach(first);
            _scope.Attach(thrower);

            _executor.PublishUpdate();
            Assert.AreEqual(1, first.UpdateCount);
            Assert.AreEqual(1, thrower.UpdateCount);

            Assert.DoesNotThrow(() => _scope.Dispose());

            _executor.PublishUpdate();
            Assert.AreEqual(1, first.UpdateCount);
            Assert.AreEqual(1, thrower.UpdateCount);
            Assert.DoesNotThrow(() => _scope.Dispose());
        }

        [Test]
        public void Detach_DeinitializeThrows_IsIsolated() {
            var thrower = new ThrowingDeinitializeProbe();
            _scope.Attach(thrower);
            _executor.PublishUpdate();
            Assert.AreEqual(1, thrower.UpdateCount);

            Assert.DoesNotThrow(() => _scope.Detach(thrower));

            _executor.PublishUpdate();
            Assert.AreEqual(1, thrower.UpdateCount);
        }

        [Test]
        public void ExecutorUnavailable_AttachAndDispose_DoNotThrow() {
            MonoManager.Instance.Bind(new FakeMonoExecutor { Valid = false });
            var scope = new LifecycleScope();

            Assert.DoesNotThrow(() => scope.Attach(new LifecycleProbe()));
            Assert.DoesNotThrow(() => scope.AttachApp(new AppProbe()));
            Assert.DoesNotThrow(() => scope.Dispose());
        }

        [Test]
        public void ExecutorUnavailable_AttachIsRetryable_AfterBind() {
            MonoManager.Instance.Bind(new FakeMonoExecutor { Valid = false });
            var scope = new LifecycleScope();
            var probe = new LifecycleProbe();
            var app = new AppProbe();

            scope.Attach(probe);
            scope.AttachApp(app);
            Assert.AreEqual(0, probe.UpdateCount);

            var valid = new FakeMonoExecutor();
            MonoManager.Instance.Bind(valid);

            scope.Attach(probe);
            scope.AttachApp(app);

            valid.PublishUpdate();
            valid.PublishAppQuit();
            Assert.AreEqual(1, probe.UpdateCount);
            Assert.AreEqual(1, app.QuitCount);

            scope.Dispose();
            valid.Dispose();
        }

        private sealed class UpdateProbe : IUpdatable {
            public int UpdateCount;

            public void OnUpdate() => UpdateCount++;
        }

        private sealed class LifecycleProbe : IInitializable, IStartable, IPreUpdatable, IUpdatable,
            IFixedUpdatable, ILateUpdatable, IPostUpdatable, IDeinitializable {
            public int InitializeCount;
            public int StartCount;
            public int PreUpdateCount;
            public int UpdateCount;
            public int FixedUpdateCount;
            public int LateUpdateCount;
            public int PostUpdateCount;
            public int DeinitializeCount;

            public void OnInitialize() => InitializeCount++;
            public void OnStart() => StartCount++;
            public void OnPreUpdate() => PreUpdateCount++;
            public void OnUpdate() => UpdateCount++;
            public void OnFixedUpdate() => FixedUpdateCount++;
            public void OnLateUpdate() => LateUpdateCount++;
            public void OnPostUpdate() => PostUpdateCount++;
            public void OnDeinitialize() => DeinitializeCount++;
        }

        private sealed class AppProbe : IAppEventAware {
            public int FocusCount;
            public int PauseCount;
            public int QuitCount;
            public bool LastFocus;
            public bool LastPause;

            public void OnAppFocus(bool hasFocus) {
                FocusCount++;
                LastFocus = hasFocus;
            }

            public void OnAppPause(bool isPause) {
                PauseCount++;
                LastPause = isPause;
            }

            public void OnAppQuit() => QuitCount++;
        }

        private sealed class OrderProbe : IDeinitializable {
            public static readonly List<string> DeinitOrder = new List<string>();

            private readonly string _name;

            public OrderProbe(string name) {
                _name = name;
            }

            public void OnDeinitialize() => DeinitOrder.Add(_name);
        }

        private sealed class RetryProbe : IInitializable, IStartable, IUpdatable, IDeinitializable {
            public bool InitializeThrows;
            public bool StartThrows;
            public int InitializeCount;
            public int StartCount;
            public int UpdateCount;
            public int DeinitializeCount;

            public void OnInitialize() {
                if (InitializeThrows) {
                    throw new InvalidOperationException("initialize failed");
                }

                InitializeCount++;
            }

            public void OnStart() {
                if (StartThrows) {
                    throw new InvalidOperationException("start failed");
                }

                StartCount++;
            }

            public void OnUpdate() => UpdateCount++;
            public void OnDeinitialize() => DeinitializeCount++;
        }

        private sealed class ThrowingDeinitializeProbe : IUpdatable, IDeinitializable {
            public int UpdateCount;

            public void OnUpdate() => UpdateCount++;

            public void OnDeinitialize() => throw new InvalidOperationException("deinitialize failed");
        }
    }
}
