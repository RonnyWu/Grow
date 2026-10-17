// SPDX-License-Identifier: MIT

using System.Collections;
using Grow.Core.Events;
using Grow.Core.Log;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Lifecycle.Impl;
using Grow.Kernel.Loop;
using UnityEngine;

namespace Grow.Tests.Editor.Lifecycle {
    internal sealed class FakeMonoExecutor : IMonoExecutor {
        private readonly GrowEvent.Publisher _appQuit = new GrowEvent.Publisher();
        private readonly GrowEvent.Publisher _preUpdate = new GrowEvent.Publisher();
        private readonly GrowEvent.Publisher _fixedUpdate = new GrowEvent.Publisher();
        private readonly GrowEvent.Publisher _update = new GrowEvent.Publisher();
        private readonly GrowEvent.Publisher _postUpdate = new GrowEvent.Publisher();
        private readonly GrowEvent.Publisher _lateUpdate = new GrowEvent.Publisher();
        private readonly GrowEvent<bool>.Publisher _appFocus = new GrowEvent<bool>.Publisher();
        private readonly GrowEvent<bool>.Publisher _appPause = new GrowEvent<bool>.Publisher();
        private bool _disposed;

        public FakeMonoExecutor() {
            Update = _update.Event;
        }

        public GrowEvent AppQuit => _appQuit.Event;
        public GrowEvent PreUpdate => _preUpdate.Event;
        public GrowEvent FixedUpdate => _fixedUpdate.Event;
        public GrowEvent Update { get; set; }
        public GrowEvent UpdateEvent => _update.Event;
        public GrowEvent PostUpdate => _postUpdate.Event;
        public GrowEvent LateUpdate => _lateUpdate.Event;
        public GrowEvent<bool> AppFocus => _appFocus.Event;
        public GrowEvent<bool> AppPause => _appPause.Event;

        public bool Valid { get; set; } = true;

        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public IEnumerator LastRoutine { get; private set; }
        public Coroutine LastStopped { get; private set; }

        public Coroutine StartCoroutine(IEnumerator routine) {
            StartCount++;
            LastRoutine = routine;
            return null;
        }

        public void StopCoroutine(Coroutine coroutine) {
            StopCount++;
            LastStopped = coroutine;
        }

        public void PublishAppQuit() => _appQuit.Publish();
        public void PublishPreUpdate() => _preUpdate.Publish();
        public void PublishFixedUpdate() => _fixedUpdate.Publish();
        public void PublishUpdate() => _update.Publish();
        public void PublishPostUpdate() => _postUpdate.Publish();
        public void PublishLateUpdate() => _lateUpdate.Publish();
        public void PublishAppFocus(bool hasFocus) => _appFocus.Publish(hasFocus);
        public void PublishAppPause(bool isPaused) => _appPause.Publish(isPaused);

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            Valid = false;

            _appQuit.Dispose();
            _preUpdate.Dispose();
            _fixedUpdate.Dispose();
            _update.Dispose();
            _postUpdate.Dispose();
            _lateUpdate.Dispose();
            _appFocus.Dispose();
            _appPause.Dispose();
        }
    }

    internal sealed class LifecycleProbe : ILifecycle {
        public ILifecycleManager LifecycleManager { get; set; }
        public bool AttachedApp => ((ILifecycle)this).AttachedApp;
        bool ILifecycle.AttachedApp =>
            LifecycleManager != null && LifecycleManager.IsAppAttached(this);
        public bool AttachedUpdater { get; set; }

        public int AwakeCount { get; private set; }
        public int EnabledCount { get; private set; }
        public int StartCount { get; private set; }
        public int FixedUpdateCount { get; private set; }
        public int PreUpdateCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int LateUpdateCount { get; private set; }
        public int PostUpdateCount { get; private set; }
        public int AppFocusCount { get; private set; }
        public int AppPauseCount { get; private set; }
        public int AppQuitCount { get; private set; }
        public int DisabledCount { get; private set; }
        public int DestroyedCount { get; private set; }
        public bool LastFocus { get; private set; }
        public bool LastPause { get; private set; }

        public void OnAwake() => AwakeCount++;
        public void OnEnabled() => EnabledCount++;
        public void OnStart() => StartCount++;
        public void OnFixedUpdate() => FixedUpdateCount++;
        public void OnPreUpdate() => PreUpdateCount++;
        public void OnUpdate() => UpdateCount++;
        public void OnLateUpdate() => LateUpdateCount++;
        public void OnPostUpdate() => PostUpdateCount++;

        public void OnAppFocus(bool hasFocus) {
            AppFocusCount++;
            LastFocus = hasFocus;
        }

        public void OnAppPause(bool isPause) {
            AppPauseCount++;
            LastPause = isPause;
        }

        public void OnAppQuit() => AppQuitCount++;
        public void OnDisabled() => DisabledCount++;
        public void OnDestroyed() => DestroyedCount++;

        public void ResetCounters() {
            AwakeCount = 0;
            EnabledCount = 0;
            StartCount = 0;
            FixedUpdateCount = 0;
            PreUpdateCount = 0;
            UpdateCount = 0;
            LateUpdateCount = 0;
            PostUpdateCount = 0;
            AppFocusCount = 0;
            AppPauseCount = 0;
            AppQuitCount = 0;
            DisabledCount = 0;
            DestroyedCount = 0;
        }
    }

    /// <summary>Lifecycle target that relies on the default empty <see cref="ILifecycle.OnPostUpdate"/>.</summary>
    internal sealed class MinimalLifecycle : ILifecycle {
        public ILifecycleManager LifecycleManager { get; set; }
        public bool AttachedApp => ((ILifecycle)this).AttachedApp;
        bool ILifecycle.AttachedApp =>
            LifecycleManager != null && LifecycleManager.IsAppAttached(this);
        public bool AttachedUpdater { get; set; }

        public void OnAwake() { }
        public void OnEnabled() { }
        public void OnStart() { }
        public void OnFixedUpdate() { }
        public void OnPreUpdate() { }
        public void OnUpdate() { }
        public void OnLateUpdate() { }
        public void OnAppFocus(bool hasFocus) { }
        public void OnAppPause(bool isPause) { }
        public void OnAppQuit() { }
        public void OnDisabled() { }
        public void OnDestroyed() { }
    }

    internal sealed class ProbeHooks : ILifecycleUpdateHooks {
        public int FixedUpdateCount { get; private set; }
        public int PreUpdateCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int LateUpdateCount { get; private set; }
        public int PostUpdateCount { get; private set; }

        public void OnFixedUpdate() => FixedUpdateCount++;
        public void OnPreUpdate() => PreUpdateCount++;
        public void OnUpdate() => UpdateCount++;
        public void OnLateUpdate() => LateUpdateCount++;
        public void OnPostUpdate() => PostUpdateCount++;
    }

    internal sealed class NullLogSink : ILogSink {
        public void Write(in LogEntry entry) { }
    }
}
