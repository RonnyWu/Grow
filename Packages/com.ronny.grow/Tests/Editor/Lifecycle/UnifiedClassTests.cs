// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Text;
using Grow.Core.Log;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Lifecycle.Impl;
using NUnit.Framework;

namespace Grow.Tests.Editor.Lifecycle {
    public class UnifiedClassTests {
        private FakeMonoExecutor _executor;
        private UnifiedClass _live;

        [SetUp]
        public void SetUp() {
            MonoManager.ResetStaticState();
            GrowLog.SetSink(new NullLogSink());
            _executor = new FakeMonoExecutor();
            MonoManager.Instance.Bind(_executor);
        }

        [TearDown]
        public void TearDown() {
            if (_live != null) {
                _live.Dispose();
            }

            _live = null;
            MonoManager.ResetStaticState();
            _executor?.Dispose();
            GrowLog.Reset();
        }

        [Test]
        public void Constructor_DefaultEnabled_RunsAwakeEnableStartInOrder() {
            var probe = New(new UpdateProbe());

            Assert.IsTrue(probe.Enabled);
            Assert.AreEqual("awake,enable,start", probe.Join());
            Assert.AreEqual(1, _executor.Update.Count);
            Assert.AreEqual(1, _executor.AppQuit.Count);
            Assert.IsTrue(probe.AttachedUpdater);
        }

        [Test]
        public void Enabled_UpdateExecutor_RoutesToOnUpdate() {
            var probe = New(new UpdateProbe());
            probe.Clear();

            _executor.PublishUpdate();

            Assert.AreEqual("update", probe.Join());
        }

        [Test]
        public void Disable_StopsUpdates_ReEnable_FiresEnableNotStartAgain() {
            var probe = New(new UpdateProbe());
            probe.Clear();

            probe.Enabled = false;
            Assert.AreEqual("disable", probe.Join());
            Assert.AreEqual(0, _executor.Update.Count);

            probe.Clear();
            _executor.PublishUpdate();
            Assert.AreEqual(string.Empty, probe.Join());

            probe.Enabled = true;
            Assert.AreEqual("enable", probe.Join());
            Assert.AreEqual(1, _executor.Update.Count);
        }

        [Test]
        public void RepeatedEnableDisable_IsEdgeTriggered() {
            var probe = New(new UpdateProbe());
            probe.Clear();

            probe.Enabled = true;
            Assert.AreEqual(string.Empty, probe.Join());

            probe.Enabled = false;
            probe.Enabled = false;
            Assert.AreEqual("disable", probe.Join());
        }

        [Test]
        public void DeclaredFlags_SubscribeOnlyMatchingPhases() {
            var probe = New(new MultiUpdateProbe());

            Assert.AreEqual(1, _executor.FixedUpdate.Count);
            Assert.AreEqual(1, _executor.LateUpdate.Count);
            Assert.AreEqual(0, _executor.Update.Count);
            Assert.AreEqual(0, _executor.PreUpdate.Count);

            probe.Enabled = false;
            Assert.AreEqual(0, _executor.FixedUpdate.Count);
            Assert.AreEqual(0, _executor.LateUpdate.Count);
        }

        [Test]
        public void Dispose_UnsubscribesAndIsIdempotent() {
            var probe = New(new UpdateProbe());
            probe.Dispose();
            _live = null;

            Assert.AreEqual(0, _executor.Update.Count);
            Assert.AreEqual(0, _executor.AppQuit.Count);
            Assert.IsFalse(probe.AttachedUpdater);

            Assert.DoesNotThrow(() => probe.Dispose());
            Assert.DoesNotThrow(() => probe.Enabled = false);
            probe.Clear();
            _executor.PublishUpdate();
            Assert.AreEqual(string.Empty, probe.Join());
        }

        [Test]
        public void AppQuit_DisablesThenDestroys() {
            var probe = New(new UpdateProbe());
            probe.Clear();

            _executor.PublishAppQuit();

            Assert.AreEqual("quit,disable,destroy", probe.Join());
            Assert.AreEqual(0, _executor.Update.Count);
            Assert.AreEqual(0, _executor.AppQuit.Count);
        }

        [Test]
        public void AppFocusAndPause_RunWhenEnabled() {
            var probe = New(new AppProbe());
            probe.Clear();

            _executor.PublishAppFocus(true);
            _executor.PublishAppPause(true);

            Assert.AreEqual("focus,pause", probe.Join());
            Assert.IsTrue(probe.LastFocus);
            Assert.IsTrue(probe.LastPause);
        }

        [Test]
        public void AppFocusAndPause_SkipWhenDisabled() {
            var probe = New(new AppProbe());
            probe.Enabled = false;
            probe.Clear();

            _executor.PublishAppFocus(true);
            _executor.PublishAppPause(true);

            Assert.AreEqual(string.Empty, probe.Join());
        }

        [Test]
        public void Dispose_RemovesAppEventSubscriptions() {
            var probe = New(new AppProbe());
            probe.Dispose();
            _live = null;
            probe.Clear();

            _executor.PublishAppFocus(true);
            _executor.PublishAppPause(true);

            Assert.AreEqual(string.Empty, probe.Join());
        }

        [Test]
        public void DisabledInOnAwake_HasNoLifecycleNoise() {
            var probe = New(new AwakeDisabledProbe());

            Assert.AreEqual("awake", probe.Join());
            Assert.IsFalse(probe.Enabled);
            Assert.AreEqual(0, _executor.Update.Count);
        }

        [Test]
        public void DisabledInOnAwake_FirstEnable_FiresStartOnce() {
            var probe = New(new AwakeDisabledProbe());
            probe.Clear();

            probe.Enabled = true;
            Assert.AreEqual("enable,start", probe.Join());

            probe.Clear();
            probe.Enabled = false;
            probe.Enabled = true;
            Assert.AreEqual("disable,enable", probe.Join());
            Assert.AreEqual(1, _executor.Update.Count);
        }

        [Test]
        public void DisableInsideOnEnabled_DoesNotSubscribeUpdates() {
            var probe = New(new DisableOnEnabledProbe());

            Assert.IsFalse(probe.Enabled);
            Assert.IsFalse(probe.AttachedUpdater);
            Assert.AreEqual(0, _executor.Update.Count);
        }

        [Test]
        public void DisableInsideOnStart_DoesNotSubscribeUpdates() {
            var probe = New(new DisableOnStartProbe());

            Assert.IsFalse(probe.Enabled);
            Assert.IsFalse(probe.AttachedUpdater);
            Assert.AreEqual(0, _executor.Update.Count);
        }

        [Test]
        public void Constructor_WhenManagerUnavailable_DoesNotThrow() {
            MonoManager.Instance.Bind(new FakeMonoExecutor { Valid = false });

            Assert.DoesNotThrow(() => _live = new UpdateProbe());
            Assert.IsNotNull(_live);
        }

        [Test]
        public void LateBind_RepairsUpdaterAndAppAttachments() {
            MonoManager.Instance.Unbind();

            var probe = New(new UpdateProbe());
            Assert.IsFalse(probe.AttachedUpdater);
            Assert.IsFalse(probe.AttachedApp);
            Assert.AreEqual(0, _executor.Update.Count);

            MonoManager.Instance.Bind(_executor);

            Assert.IsTrue(probe.AttachedUpdater);
            Assert.IsTrue(probe.AttachedApp);
            probe.Clear();
            _executor.PublishUpdate();
            Assert.AreEqual("update", probe.Join());
        }

        private T New<T>(T probe) where T : UnifiedClass {
            _live = probe;
            return probe;
        }

        private abstract class Probe : UnifiedClass {
            private readonly List<string> _events = new();

            public string Join() {
                if (_events.Count == 0) {
                    return string.Empty;
                }

                var builder = new StringBuilder();
                for (var i = 0; i < _events.Count; i++) {
                    if (i > 0) {
                        builder.Append(',');
                    }

                    builder.Append(_events[i]);
                }

                return builder.ToString();
            }

            public void Clear() {
                _events.Clear();
            }

            protected void Add(string entry) {
                _events.Add(entry);
            }
        }

        private sealed class UpdateProbe : Probe {
            protected override UpdateFlag UpdateFlag => UpdateFlag.Update;

            protected override void OnAwake() {
                Add("awake");
            }

            protected override void OnEnabled() {
                Add("enable");
            }

            protected override void OnStart() {
                Add("start");
            }

            protected override void OnUpdate() {
                Add("update");
            }

            protected override void OnDisabled() {
                Add("disable");
            }

            protected override void OnDestroyed() {
                Add("destroy");
            }

            protected override void OnAppQuit() {
                Add("quit");
            }
        }

        private sealed class AppProbe : Probe {
            public bool LastFocus { get; private set; }
            public bool LastPause { get; private set; }

            protected override void OnAppFocus(bool hasFocus) {
                LastFocus = hasFocus;
                Add("focus");
            }

            protected override void OnAppPause(bool isPause) {
                LastPause = isPause;
                Add("pause");
            }
        }

        private sealed class MultiUpdateProbe : Probe {
            protected override UpdateFlag UpdateFlag =>
                UpdateFlag.FixedUpdate | UpdateFlag.LateUpdate;
        }

        private sealed class AwakeDisabledProbe : Probe {
            protected override UpdateFlag UpdateFlag => UpdateFlag.Update;

            protected override void OnAwake() {
                Add("awake");
                Enabled = false;
            }

            protected override void OnEnabled() {
                Add("enable");
            }

            protected override void OnStart() {
                Add("start");
            }

            protected override void OnDisabled() {
                Add("disable");
            }
        }

        private sealed class DisableOnEnabledProbe : Probe {
            protected override UpdateFlag UpdateFlag => UpdateFlag.Update;

            protected override void OnEnabled() {
                Add("enable");
                Enabled = false;
            }

            protected override void OnDisabled() {
                Add("disable");
            }
        }

        private sealed class DisableOnStartProbe : Probe {
            protected override UpdateFlag UpdateFlag => UpdateFlag.Update;

            protected override void OnStart() {
                Add("start");
                Enabled = false;
            }

            protected override void OnDisabled() {
                Add("disable");
            }
        }
    }
}
