// SPDX-License-Identifier: MIT

using Grow.Core.Log;
using Grow.Kernel.Lifecycle;
using NUnit.Framework;

namespace Grow.Tests.Editor.Lifecycle {
    public class LifecycleContractTests {
        private FakeMonoExecutor _executor;

        [SetUp]
        public void SetUp() {
            MonoManager.ResetStaticState();
            GrowLog.SetSink(new NullLogSink());
            _executor = new FakeMonoExecutor();
        }

        [TearDown]
        public void TearDown() {
            MonoManager.ResetStaticState();
            _executor?.Dispose();
            GrowLog.Reset();
        }

        [Test]
        public void DefaultOnPostUpdate_DoesNotThrow() {
            var target = new MinimalLifecycle();

            Assert.DoesNotThrow(() => ((ILifecycle)target).OnPostUpdate());
        }

        [Test]
        public void AttachApp_WithNullManager_StaysDetached() {
            var target = new MinimalLifecycle { LifecycleManager = null };

            Assert.DoesNotThrow(() => ((ILifecycle)target).AttachApp());
            Assert.IsFalse(target.AttachedApp);
            Assert.DoesNotThrow(() => ((ILifecycle)target).DetachApp());
            Assert.IsFalse(target.AttachedApp);
        }

        [Test]
        public void AttachApp_WhenManagerIsNotBound_StaysDetached() {
            var target = new LifecycleProbe { LifecycleManager = MonoManager.Instance };

            ((ILifecycle)target).AttachApp();

            Assert.IsFalse(target.AttachedApp);
        }

        [Test]
        public void DetachApp_WithoutAttach_DoesNotThrow() {
            MonoManager.Instance.Bind(_executor);
            var target = new LifecycleProbe { LifecycleManager = MonoManager.Instance };

            Assert.DoesNotThrow(() => ((ILifecycle)target).DetachApp());
            Assert.IsFalse(target.AttachedApp);
        }

        [Test]
        public void Attach_Detach_Attach_WorksAgain() {
            MonoManager.Instance.Bind(_executor);
            var target = new LifecycleProbe { LifecycleManager = MonoManager.Instance };

            ((ILifecycle)target).AttachApp();
            ((ILifecycle)target).DetachApp();
            ((ILifecycle)target).AttachApp();

            Assert.IsTrue(target.AttachedApp);
            Assert.AreEqual(1, _executor.AppQuit.Count);
        }
    }
}
