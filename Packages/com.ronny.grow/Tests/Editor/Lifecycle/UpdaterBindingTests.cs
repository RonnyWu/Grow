// SPDX-License-Identifier: MIT

using Grow.Core.Log;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Lifecycle.Impl;
using NUnit.Framework;

namespace Grow.Tests.Editor.Lifecycle {
    public class UpdaterBindingTests {
        private FakeMonoExecutor _executor;
        private ProbeHooks _hooks;

        [SetUp]
        public void SetUp() {
            MonoManager.ResetStaticState();
            GrowLog.SetSink(new NullLogSink());
            _executor = new FakeMonoExecutor();
            MonoManager.Instance.Bind(_executor);
            _hooks = new ProbeHooks();
        }

        [TearDown]
        public void TearDown() {
            MonoManager.ResetStaticState();
            _executor?.Dispose();
            GrowLog.Reset();
        }

        [Test]
        public void Attach_SubscribesOnlyDeclaredPhases() {
            var binding = new UpdaterBinding(
                UpdateFlag.FixedUpdate | UpdateFlag.PostUpdate,
                _hooks,
                MonoManager.Instance);

            binding.Attach();

            Assert.IsTrue(binding.Attached);
            Assert.AreEqual(1, _executor.FixedUpdate.Count);
            Assert.AreEqual(1, _executor.PostUpdate.Count);
            Assert.AreEqual(0, _executor.PreUpdate.Count);
            Assert.AreEqual(0, _executor.Update.Count);
            Assert.AreEqual(0, _executor.LateUpdate.Count);
        }

        [Test]
        public void Attach_AllPhases_RoutesToMatchingHooks() {
            var binding = new UpdaterBinding(
                UpdateFlag.FixedUpdate | UpdateFlag.PreUpdate | UpdateFlag.Update |
                UpdateFlag.LateUpdate | UpdateFlag.PostUpdate,
                _hooks,
                MonoManager.Instance);

            binding.Attach();

            _executor.PublishFixedUpdate();
            _executor.PublishPreUpdate();
            _executor.PublishUpdate();
            _executor.PublishLateUpdate();
            _executor.PublishPostUpdate();

            Assert.AreEqual(1, _hooks.FixedUpdateCount);
            Assert.AreEqual(1, _hooks.PreUpdateCount);
            Assert.AreEqual(1, _hooks.UpdateCount);
            Assert.AreEqual(1, _hooks.LateUpdateCount);
            Assert.AreEqual(1, _hooks.PostUpdateCount);
        }

        [Test]
        public void Attach_IsIdempotent() {
            var binding = new UpdaterBinding(UpdateFlag.Update, _hooks, MonoManager.Instance);

            binding.Attach();
            binding.Attach();

            Assert.AreEqual(1, _executor.Update.Count);

            _executor.PublishUpdate();
            Assert.AreEqual(1, _hooks.UpdateCount);
        }

        [Test]
        public void Detach_ReleasesSubscriptions_AndIsIdempotent() {
            var binding = new UpdaterBinding(UpdateFlag.Update, _hooks, MonoManager.Instance);
            binding.Attach();

            binding.Detach();
            binding.Detach();

            Assert.IsFalse(binding.Attached);
            Assert.AreEqual(0, _executor.Update.Count);

            _executor.PublishUpdate();
            Assert.AreEqual(0, _hooks.UpdateCount);
        }

        [Test]
        public void Attach_WhenHostIsInvalid_StaysUnattached_AndRetries() {
            _executor.Valid = false;
            var binding = new UpdaterBinding(UpdateFlag.Update, _hooks, MonoManager.Instance);

            binding.Attach();

            Assert.IsFalse(binding.Attached);
            Assert.AreEqual(0, _executor.Update.Count);

            _executor.Valid = true;
            binding.Attach();

            Assert.IsTrue(binding.Attached);
            Assert.AreEqual(1, _executor.Update.Count);

            _executor.PublishUpdate();
            Assert.AreEqual(1, _hooks.UpdateCount);
        }

        [Test]
        public void Attach_WithNullManager_StaysUnattached() {
            var binding = new UpdaterBinding(UpdateFlag.Update, _hooks, null);

            Assert.DoesNotThrow(() => binding.Attach());

            Assert.IsFalse(binding.Attached);
            Assert.AreEqual(0, _executor.Update.Count);
        }

        [Test]
        public void Attach_PartialFailure_RetriesOnlyMissingPhases() {
            _executor.Update = null;
            var binding = new UpdaterBinding(
                UpdateFlag.FixedUpdate | UpdateFlag.Update,
                _hooks,
                MonoManager.Instance);

            binding.Attach();

            Assert.IsFalse(binding.Attached);
            Assert.AreEqual(1, _executor.FixedUpdate.Count, "the available phase stays subscribed");
            Assert.IsNull(_executor.Update);

            _executor.Update = _executor.UpdateEvent;
            binding.Attach();

            Assert.IsTrue(binding.Attached);
            Assert.AreEqual(1, _executor.FixedUpdate.Count,
                "the retry must not duplicate the fixed phase");
            Assert.AreEqual(1, _executor.Update.Count);

            _executor.PublishFixedUpdate();
            _executor.PublishUpdate();
            Assert.AreEqual(1, _hooks.FixedUpdateCount);
            Assert.AreEqual(1, _hooks.UpdateCount);
        }

        [Test]
        public void Attach_None_AttachesWithoutSubscriptions() {
            var binding = new UpdaterBinding(UpdateFlag.None, _hooks, MonoManager.Instance);

            binding.Attach();

            Assert.IsTrue(binding.Attached);
            Assert.AreEqual(0, _executor.PreUpdate.Count);
            Assert.AreEqual(0, _executor.FixedUpdate.Count);
            Assert.AreEqual(0, _executor.Update.Count);
            Assert.AreEqual(0, _executor.PostUpdate.Count);
            Assert.AreEqual(0, _executor.LateUpdate.Count);
        }

        [Test]
        public void Detach_AfterHostWasDisposed_DoesNotThrow() {
            var binding = new UpdaterBinding(UpdateFlag.Update, _hooks, MonoManager.Instance);
            binding.Attach();
            _executor.Dispose();

            Assert.DoesNotThrow(() => binding.Detach());
            Assert.IsFalse(binding.Attached);
        }

        [Test]
        public void Attach_AfterHostRebind_ResubscribesToTheNewHost() {
            var binding = new UpdaterBinding(UpdateFlag.Update, _hooks, MonoManager.Instance);
            binding.Attach();
            _executor.PublishUpdate();
            Assert.AreEqual(1, _hooks.UpdateCount);

            var next = new FakeMonoExecutor();
            MonoManager.Instance.Bind(next);
            Assert.IsTrue(binding.Attached,
                "a host rebind reattaches to the new host");
            Assert.AreEqual(1, next.Update.Count);

            binding.Attach();
            next.PublishUpdate();

            Assert.AreEqual(2, _hooks.UpdateCount);
            Assert.AreEqual(0, _executor.Update.Count, "the old host must have been released");

            next.Dispose();
        }

        [Test]
        public void Reattach_DoesNotDuplicateSubscriptions() {
            var binding = new UpdaterBinding(UpdateFlag.Update, _hooks, MonoManager.Instance);

            binding.Attach();
            binding.Detach();
            binding.Attach();

            Assert.AreEqual(1, _executor.Update.Count);

            _executor.PublishUpdate();
            Assert.AreEqual(1, _hooks.UpdateCount);
        }
    }
}
