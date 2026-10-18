// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Grow.Core.Events;
using Grow.Core.Log;
using Grow.Kernel.Lifecycle.Impl;

namespace Grow.Kernel.Lifecycle {
    /// <summary>
    /// Attaches plain C# objects to the lifecycle manager and releases them in reverse order.
    /// </summary>
    /// <remarks>
    /// Use this for objects that cannot or should not implement <see cref="ILifecycle"/> (the
    /// self-managed contract). The scope drives the facet interfaces instead:
    /// <see cref="IInitializable"/>, <see cref="IStartable"/>, the five update facets,
    /// <see cref="IAppEventAware"/> and <see cref="IDeinitializable"/>. It does not run an
    /// enabled/disabled state machine.
    /// <para>
    /// Each target is tracked by reference. Attach is incremental and retryable: initialization,
    /// start, updater subscriptions and app subscriptions keep independent completion flags, so a
    /// failed step does not repeat an earlier successful one. User hooks and subscription calls are
    /// isolated; one bad target never stops the rest of the scope.
    /// </para>
    /// <para>
    /// The scope owns the subscriptions. Dispose is mandatory (there is no finalizer); after dispose,
    /// attach calls throw <see cref="ObjectDisposedException"/> while detach remains safe. The scope
    /// has no static state.
    /// </para>
    /// </remarks>
    public sealed class LifecycleScope : IDisposable {
        private const string LogCategory = "Lifecycle";

        private readonly ILifecycleManager _manager;
        private readonly Dictionary<object, Entry> _entries =
            new Dictionary<object, Entry>(ReferenceComparer.Instance);
        private readonly List<Entry> _order = new List<Entry>();
        private bool _unavailableWarned;
        private bool _disposed;

        /// <summary>Creates a scope over <see cref="MonoManager.Instance"/>.</summary>
        public LifecycleScope() : this(MonoManager.Instance) {
        }

        /// <summary>Creates a scope over an explicit manager.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="manager"/> is <c>null</c>.</exception>
        public LifecycleScope(ILifecycleManager manager) {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        /// <summary>
        /// Attaches <paramref name="target"/>, invoking initialize/start once and subscribing its
        /// declared update facets.
        /// </summary>
        /// <returns><c>true</c> when the target is fully attached; <c>false</c> when a hook or host was unavailable.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is <c>null</c>.</exception>
        /// <exception cref="ObjectDisposedException">The scope has been disposed.</exception>
        public bool Attach(object target) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            ThrowIfDisposed(nameof(Attach));

            var entry = GetOrCreateEntry(target);
            EnsureHostEpoch(entry);
            if (entry.Attached) {
                return true;
            }

            entry.Touched = true;

            if (target is IInitializable initializable && !entry.Initialized) {
                try {
                    initializable.OnInitialize();
                    entry.Initialized = true;
                } catch (Exception exception) {
                    GrowLog.Error(
                        $"[LifecycleScope] {target.GetType().Name}.OnInitialize() threw; the target " +
                        "stays unattached and can be retried with Attach().",
                        LogCategory);
                    GrowLogDebug(exception);
                    return false;
                }
            }

            if (target is IStartable startable && !entry.Started) {
                try {
                    startable.OnStart();
                    entry.Started = true;
                } catch (Exception exception) {
                    GrowLog.Error(
                        $"[LifecycleScope] {target.GetType().Name}.OnStart() threw; the target stays " +
                        "unattached and can be retried with Attach().",
                        LogCategory);
                    GrowLogDebug(exception);
                    return false;
                }
            }

            try {
                entry.Updater.Attach();
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[LifecycleScope] {target.GetType().Name} update subscription threw; the target " +
                    "stays unattached and can be retried with Attach().",
                    LogCategory);
                GrowLogDebug(exception);
                return false;
            }

            if (!entry.Updater.Attached) {
                return false;
            }

            entry.Attached = true;
            return true;
        }

        /// <summary>Releases the target's update subscriptions and calls deinitialize once.</summary>
        public void Detach(object target) {
            if (target == null) {
                return;
            }

            if (!_entries.TryGetValue(target, out var entry)) {
                return;
            }

            DetachEntry(entry);
            TryRemoveEntry(entry);
        }

        /// <summary>
        /// Subscribes an <see cref="IAppEventAware"/> target to quit, focus and pause. Targets that
        /// do not implement the facet are ignored.
        /// </summary>
        /// <returns><c>true</c> when attached (or nothing to attach); <c>false</c> when the host was unavailable.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is <c>null</c>.</exception>
        /// <exception cref="ObjectDisposedException">The scope has been disposed.</exception>
        public bool AttachApp(object target) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            ThrowIfDisposed(nameof(AttachApp));
            if (!(target is IAppEventAware aware)) {
                return true;
            }

            var entry = GetOrCreateEntry(target);
            EnsureHostEpoch(entry);
            if (entry.AppAttached) {
                return true;
            }

            entry.AppTouched = true;

            var appQuit = _manager.AppQuit;
            var appFocus = _manager.AppFocus;
            var appPause = _manager.AppPause;
            if (appQuit == null || appFocus == null || appPause == null) {
                WarnUnavailable();
                return false;
            }

            var subscriptions = new SubscriptionBag();
            try {
                subscriptions.Add(appQuit.Subscribe(aware.OnAppQuit));
                subscriptions.Add(appFocus.Subscribe(aware.OnAppFocus));
                subscriptions.Add(appPause.Subscribe(aware.OnAppPause));
            } catch (Exception exception) {
                subscriptions.Dispose();
                GrowLog.Error(
                    $"[LifecycleScope] {target.GetType().Name} app-event subscription threw; the " +
                    "target stays unattached and can be retried with AttachApp().",
                    LogCategory);
                GrowLogDebug(exception);
                return false;
            }

            entry.AppSubscriptions = subscriptions;
            entry.AppAttached = true;
            return true;
        }

        /// <summary>Releases the target's app-event subscriptions.</summary>
        public void DetachApp(object target) {
            if (target == null) {
                return;
            }

            if (!_entries.TryGetValue(target, out var entry)) {
                return;
            }

            DetachAppEntry(entry);
            TryRemoveEntry(entry);
        }

        /// <summary>Releases every target in reverse attach order; safe to call more than once.</summary>
        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;

            for (var i = _order.Count - 1; i >= 0; i--) {
                var entry = _order[i];
                try {
                    DetachAppEntry(entry);
                    DetachEntry(entry);
                } catch (Exception exception) {
                    GrowLog.Error(
                        "[LifecycleScope] An entry threw while the scope was being disposed; " +
                        "the remaining entries are still released.",
                        LogCategory);
                    GrowLogDebug(exception);
                }
            }

            _order.Clear();
            _entries.Clear();
        }

        private void ThrowIfDisposed(string api) {
            if (!_disposed) {
                return;
            }

            throw new ObjectDisposedException(
                nameof(LifecycleScope),
                $"The scope has been disposed; {api} is not allowed. Create a new scope.");
        }

        private Entry GetOrCreateEntry(object target) {
            if (_entries.TryGetValue(target, out var entry)) {
                return entry;
            }

            entry = new Entry(target, _manager);
            _entries.Add(target, entry);
            _order.Add(entry);
            return entry;
        }

        private void EnsureHostEpoch(Entry entry) {
            var epoch = _manager.HostEpoch;
            if (entry.Epoch == epoch) {
                return;
            }

            try {
                entry.Updater.Detach();
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[LifecycleScope] {entry.Target.GetType().Name} update unsubscribe during host " +
                    $"rebind threw: {exception}",
                    LogCategory);
            }

            if (entry.AppSubscriptions != null) {
                try {
                    entry.AppSubscriptions.Dispose();
                } catch (Exception exception) {
                    GrowLog.Error(
                        $"[LifecycleScope] {entry.Target.GetType().Name} app unsubscribe during host " +
                        $"rebind threw: {exception}",
                        LogCategory);
                }

                entry.AppSubscriptions = null;
            }

            entry.Attached = false;
            entry.AppAttached = false;
            entry.Epoch = epoch;
        }

        private void DetachEntry(Entry entry) {
            try {
                entry.Updater.Detach();
            } catch (Exception exception) {
                GrowLog.Error(
                    $"[LifecycleScope] {entry.Target.GetType().Name} update unsubscribe threw; " +
                    "the remaining callbacks are still released.",
                    LogCategory);
                GrowLogDebug(exception);
            }

            // Deinitialize only pairs with a successful initialization. A target without an
            // IInitializable facet has no initialization step, so its deinitialize remains valid.
            var shouldDeinitialize = entry.Initialized ||
                (!(entry.Target is IInitializable) && entry.Touched);

            entry.Initialized = false;
            entry.Started = false;
            entry.Attached = false;

            if (!entry.Touched) {
                return;
            }

            entry.Touched = false;
            if (!shouldDeinitialize) {
                return;
            }

            if (entry.Target is IDeinitializable deinitializable) {
                try {
                    deinitializable.OnDeinitialize();
                } catch (Exception exception) {
                    GrowLog.Error(
                        $"[LifecycleScope] {entry.Target.GetType().Name}.OnDeinitialize() threw; " +
                        "the remaining targets are still released.",
                        LogCategory);
                    GrowLogDebug(exception);
                }
            }
        }

        private void DetachAppEntry(Entry entry) {
            if (entry.AppSubscriptions != null) {
                try {
                    entry.AppSubscriptions.Dispose();
                } catch (Exception exception) {
                    GrowLog.Error(
                        $"[LifecycleScope] {entry.Target.GetType().Name} app unsubscribe threw; " +
                        "the remaining callbacks are still released.",
                        LogCategory);
                    GrowLogDebug(exception);
                }

                entry.AppSubscriptions = null;
            }

            entry.AppAttached = false;
            entry.AppTouched = false;
        }

        private void TryRemoveEntry(Entry entry) {
            if (entry.Touched || entry.AppTouched) {
                return;
            }

            _entries.Remove(entry.Target);
            _order.Remove(entry);
        }

        private void WarnUnavailable() {
            if (_unavailableWarned) {
                return;
            }

            _unavailableWarned = true;
            GrowLog.Warning(
                "[LifecycleScope] The lifecycle manager is unavailable; the target was not fully " +
                "attached. Bind the loop host, then call Attach/AttachApp again.",
                LogCategory);
        }

        private static void GrowLogDebug(Exception exception) {
            if (!GrowLog.IsEnabled(LogLevel.Debug)) {
                return;
            }

            GrowLog.Debug(exception.ToString(), LogCategory);
        }

        private static UpdateFlag GetFlag(object target) {
            var flag = UpdateFlag.None;
            if (target is IFixedUpdatable) {
                flag |= UpdateFlag.FixedUpdate;
            }

            if (target is IPreUpdatable) {
                flag |= UpdateFlag.PreUpdate;
            }

            if (target is IUpdatable) {
                flag |= UpdateFlag.Update;
            }

            if (target is ILateUpdatable) {
                flag |= UpdateFlag.LateUpdate;
            }

            if (target is IPostUpdatable) {
                flag |= UpdateFlag.PostUpdate;
            }

            return flag;
        }

        private sealed class Entry {
            public readonly object Target;
            public readonly UpdaterBinding Updater;

            public bool Touched;
            public bool AppTouched;
            public bool Initialized;
            public bool Started;
            public bool Attached;
            public bool AppAttached;
            public int Epoch;
            public SubscriptionBag AppSubscriptions;

            public Entry(object target, ILifecycleManager manager) {
                Target = target;
                Epoch = manager.HostEpoch;
                Updater = new UpdaterBinding(GetFlag(target), new Hooks(target), manager);
            }
        }

        private sealed class Hooks : ILifecycleUpdateHooks {
            private readonly object _target;

            public Hooks(object target) {
                _target = target;
            }

            public void OnFixedUpdate() => (_target as IFixedUpdatable)?.OnFixedUpdate();
            public void OnPreUpdate() => (_target as IPreUpdatable)?.OnPreUpdate();
            public void OnUpdate() => (_target as IUpdatable)?.OnUpdate();
            public void OnLateUpdate() => (_target as ILateUpdatable)?.OnLateUpdate();
            public void OnPostUpdate() => (_target as IPostUpdatable)?.OnPostUpdate();
        }

        private sealed class ReferenceComparer : IEqualityComparer<object> {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
