// SPDX-License-Identifier: MIT

using System.Collections;
using Grow.Core.Log;
using Grow.Kernel.Lifecycle;
using NUnit.Framework;

namespace Grow.Tests.Editor.Lifecycle {
    public class MonoManagerTests {
        private FakeMonoExecutor _executor;

        [SetUp]
        public void SetUp() {
            MonoManager.ResetStaticState();
            GrowLog.SetSink(new NullLogSink());
            _executor = new FakeMonoExecutor();
            MonoManager.Instance.Bind(_executor);
        }

        [TearDown]
        public void TearDown() {
            MonoManager.ResetStaticState();
            _executor?.Dispose();
            GrowLog.Reset();
        }

        [Test]
        public void Instance_IsSingleton() {
            Assert.AreSame(MonoManager.Instance, MonoManager.Instance);
        }

        [Test]
        public void Bind_ExposesTheSameEvents() {
            var manager = MonoManager.Instance;

            Assert.AreSame(_executor.AppQuit, manager.AppQuit);
            Assert.AreSame(_executor.PreUpdate, manager.PreUpdate);
            Assert.AreSame(_executor.FixedUpdate, manager.FixedUpdate);
            Assert.AreSame(_executor.Update, manager.Update);
            Assert.AreSame(_executor.PostUpdate, manager.PostUpdate);
            Assert.AreSame(_executor.LateUpdate, manager.LateUpdate);
            Assert.AreSame(_executor.AppFocus, manager.AppFocus);
            Assert.AreSame(_executor.AppPause, manager.AppPause);
        }

        [Test]
        public void Bind_Null_IsIgnored() {
            var bound = MonoManager.Instance.Update;

            Assert.DoesNotThrow(() => MonoManager.Instance.Bind(null));

            Assert.IsTrue(MonoManager.Instance.Valid);
            Assert.AreSame(bound, MonoManager.Instance.Update);
        }

        [Test]
        public void Bind_SameInstance_IsIdempotent() {
            var bound = MonoManager.Instance.Update;

            MonoManager.Instance.Bind(_executor);

            Assert.AreSame(bound, MonoManager.Instance.Update);
        }

        [Test]
        public void Unbind_InvalidatesTheManager() {
            MonoManager.Instance.Unbind();

            Assert.IsFalse(MonoManager.Instance.Valid);
            Assert.IsNull(MonoManager.Instance.Update);
            Assert.IsNull(MonoManager.Instance.AppQuit);
            Assert.IsNull(MonoManager.Instance.StartCoroutine(Empty()));
            Assert.DoesNotThrow(() => MonoManager.Instance.StopCoroutine(null));
        }

        [Test]
        public void InvalidExecutor_DegradesEveryAccessor() {
            _executor.Valid = false;

            Assert.IsFalse(MonoManager.Instance.Valid);
            Assert.IsNull(MonoManager.Instance.AppQuit);
            Assert.IsNull(MonoManager.Instance.PreUpdate);
            Assert.IsNull(MonoManager.Instance.FixedUpdate);
            Assert.IsNull(MonoManager.Instance.Update);
            Assert.IsNull(MonoManager.Instance.PostUpdate);
            Assert.IsNull(MonoManager.Instance.LateUpdate);
            Assert.IsNull(MonoManager.Instance.AppFocus);
            Assert.IsNull(MonoManager.Instance.AppPause);
            Assert.IsNull(MonoManager.Instance.StartCoroutine(Empty()));
            Assert.DoesNotThrow(() => MonoManager.Instance.StopCoroutine(null));
        }

        [Test]
        public void Rebind_AfterInvalid_Recovers() {
            _executor.Valid = false;
            Assert.IsNull(MonoManager.Instance.Update);

            var valid = new FakeMonoExecutor();
            MonoManager.Instance.Bind(valid);

            Assert.IsTrue(MonoManager.Instance.Valid);
            Assert.AreSame(valid.Update, MonoManager.Instance.Update);
            valid.Dispose();
        }

        [Test]
        public void AttachApp_SubscribesAndDeliversEveryAppEvent() {
            var target = new LifecycleProbe { LifecycleManager = MonoManager.Instance };

            ((ILifecycle)target).AttachApp();

            Assert.IsTrue(target.AttachedApp);
            Assert.AreEqual(1, _executor.AppQuit.Count);
            Assert.AreEqual(1, _executor.AppFocus.Count);
            Assert.AreEqual(1, _executor.AppPause.Count);

            _executor.PublishAppQuit();
            _executor.PublishAppFocus(true);
            _executor.PublishAppPause(true);

            Assert.AreEqual(1, target.AppQuitCount);
            Assert.AreEqual(1, target.AppFocusCount);
            Assert.IsTrue(target.LastFocus);
            Assert.AreEqual(1, target.AppPauseCount);
            Assert.IsTrue(target.LastPause);
        }

        [Test]
        public void AttachApp_IsIdempotent() {
            var target = new LifecycleProbe { LifecycleManager = MonoManager.Instance };

            ((ILifecycle)target).AttachApp();
            ((ILifecycle)target).AttachApp();

            Assert.AreEqual(1, _executor.AppQuit.Count);
            Assert.AreEqual(1, _executor.AppFocus.Count);
            Assert.AreEqual(1, _executor.AppPause.Count);

            _executor.PublishAppQuit();
            Assert.AreEqual(1, target.AppQuitCount);
        }

        [Test]
        public void AttachApp_WhenHostIsInvalid_StaysDetachedAndRetries() {
            _executor.Valid = false;
            var target = new LifecycleProbe { LifecycleManager = MonoManager.Instance };

            ((ILifecycle)target).AttachApp();

            Assert.IsFalse(target.AttachedApp);
            Assert.AreEqual(0, _executor.AppQuit.Count);

            _executor.Valid = true;
            ((ILifecycle)target).AttachApp();

            Assert.IsTrue(target.AttachedApp);
            Assert.AreEqual(1, _executor.AppQuit.Count);
            Assert.AreEqual(1, _executor.AppFocus.Count);
            Assert.AreEqual(1, _executor.AppPause.Count);

            _executor.PublishAppQuit();
            Assert.AreEqual(1, target.AppQuitCount);
        }

        [Test]
        public void AttachApp_Null_ReturnsFalse() {
            Assert.IsFalse(MonoManager.Instance.AttachApp(null));
        }

        [Test]
        public void HostEpoch_IncrementsOnBindAndUnbind() {
            var manager = MonoManager.Instance;
            var initial = manager.HostEpoch;

            var next = new FakeMonoExecutor();
            manager.Bind(next);
            Assert.Greater(manager.HostEpoch, initial);

            var afterBind = manager.HostEpoch;
            manager.Unbind();
            Assert.Greater(manager.HostEpoch, afterBind);

            next.Dispose();
        }

        [Test]
        public void AttachApp_NewHost_ReattachesToTheNewEvents() {
            var target = new LifecycleProbe { LifecycleManager = MonoManager.Instance };
            ((ILifecycle)target).AttachApp();
            Assert.AreEqual(1, _executor.AppQuit.Count);

            var next = new FakeMonoExecutor();
            MonoManager.Instance.Bind(next);

            Assert.AreEqual(0, _executor.AppQuit.Count, "rebinding releases subscriptions to the old host");
            Assert.IsFalse(target.AttachedApp, "the report must be reset when the manager drops the binding");
            Assert.AreEqual(0, next.AppQuit.Count);

            ((ILifecycle)target).AttachApp();

            Assert.IsTrue(target.AttachedApp);
            Assert.AreEqual(1, next.AppQuit.Count);

            next.PublishAppQuit();
            Assert.AreEqual(1, target.AppQuitCount);

            next.Dispose();
        }

        [Test]
        public void DetachApp_ReleasesSubscriptions() {
            var target = new LifecycleProbe { LifecycleManager = MonoManager.Instance };
            ((ILifecycle)target).AttachApp();

            ((ILifecycle)target).DetachApp();

            Assert.IsFalse(target.AttachedApp);
            Assert.AreEqual(0, _executor.AppQuit.Count);
            Assert.AreEqual(0, _executor.AppFocus.Count);
            Assert.AreEqual(0, _executor.AppPause.Count);

            _executor.PublishAppQuit();
            Assert.AreEqual(0, target.AppQuitCount);
        }

        [Test]
        public void ResetStaticState_DropsTheSingletonAndItsSubscriptions() {
            var old = MonoManager.Instance;
            var target = new LifecycleProbe { LifecycleManager = old };
            ((ILifecycle)target).AttachApp();
            Assert.AreEqual(1, _executor.AppQuit.Count);

            MonoManager.ResetStaticState();

            Assert.AreNotSame(old, MonoManager.Instance);
            Assert.IsFalse(MonoManager.Instance.Valid);
            Assert.AreEqual(0, _executor.AppQuit.Count);
        }

        [Test]
        public void StartCoroutine_ForwardsToTheHost() {
            var routine = Empty();

            var coroutine = MonoManager.Instance.StartCoroutine(routine);

            Assert.IsNull(coroutine, "the fake host has no real Coroutine to return");
            Assert.AreSame(routine, _executor.LastRoutine);
            Assert.AreEqual(1, _executor.StartCount);
        }

        [Test]
        public void StopCoroutine_ForwardsToTheHost() {
            MonoManager.Instance.StopCoroutine(null);

            Assert.AreEqual(1, _executor.StopCount);
        }

        private static IEnumerator Empty() {
            yield break;
        }
    }
}
