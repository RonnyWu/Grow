// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Grow.Core.Events;
using Grow.Core.Log;
using Grow.Kernel.Loop;
using UnityEngine;

namespace Grow.Kernel.Lifecycle {
    /// <summary>
    /// Default <see cref="ILifecycleManager"/>: a process-wide facade over the engine host.
    /// </summary>
    /// <remarks>
    /// The manager does not create a host. Call <see cref="Bind"/> with the executor returned by
    /// <see cref="GrowLoop.Install"/> (or by a test double) once, after the loop is installed. A
    /// missing or destroyed host degrades gracefully: event access returns <c>null</c>, coroutine
    /// calls return <c>null</c>, and a single error is logged per manager instance.
    /// <para>
    /// Application subscriptions are owned here as <see cref="SubscriptionBag"/> values keyed by a
    /// weak reference to the target. That keeps <see cref="ILifecycle"/> free of subscription fields
    /// while still allowing idempotent attach/detach and retry after a late bind.
    /// </para>
    /// <para>
    /// The facade is a singleton because the host is a process-wide kernel mechanism. Static state is
    /// dropped at <c>SubsystemRegistration</c> so a Play session that disables domain reload cannot
    /// keep a destroyed host or stale subscriptions alive.
    /// </para>
    /// </remarks>
    public sealed class MonoManager : ILifecycleManager {
        private const string LogCategory = "Lifecycle";

        private static MonoManager _instance;

        private IMonoExecutor _executor;
        private bool _executorUnavailableLogged;
        private int _hostEpoch;

        // Weak keys: a target that is collected without DetachApp does not keep the manager alive.
        private ConditionalWeakTable<ILifecycle, SubscriptionBag> _appBindings =
            new ConditionalWeakTable<ILifecycle, SubscriptionBag>();

        private MonoManager() {
        }

        /// <summary>The process-wide manager.</summary>
        public static MonoManager Instance => _instance ??= new MonoManager();

        /// <summary>True while a host is bound and the host object is still alive.</summary>
        public bool Valid => _executor != null && _executor.Valid;

        /// <summary>Raised once when the application is quitting.</summary>
        public GrowEvent AppQuit => Get(executor => executor.AppQuit, nameof(AppQuit));

        /// <summary>Raised before scene scripts update.</summary>
        public GrowEvent PreUpdate => Get(executor => executor.PreUpdate, nameof(PreUpdate));

        /// <summary>Raised after the fixed-update group.</summary>
        public GrowEvent FixedUpdate => Get(executor => executor.FixedUpdate, nameof(FixedUpdate));

        /// <summary>Raised after scene scripts update.</summary>
        public GrowEvent Update => Get(executor => executor.Update, nameof(Update));

        /// <summary>Raised after the whole update group, before late update.</summary>
        public GrowEvent PostUpdate => Get(executor => executor.PostUpdate, nameof(PostUpdate));

        /// <summary>Raised after scene scripts late-update.</summary>
        public GrowEvent LateUpdate => Get(executor => executor.LateUpdate, nameof(LateUpdate));

        /// <summary>Raised when the application gains or loses focus.</summary>
        public GrowEvent<bool> AppFocus => Get(executor => executor.AppFocus, nameof(AppFocus));

        /// <summary>Raised when the application pauses or resumes.</summary>
        public GrowEvent<bool> AppPause => Get(executor => executor.AppPause, nameof(AppPause));

        /// <summary>Host generation; see <see cref="ILifecycleManager.HostEpoch"/>.</summary>
        public int HostEpoch => _hostEpoch;

        /// <summary>Binds the engine host; internal composition-root entry.</summary>
        internal void Bind(IMonoExecutor executor) {
            if (executor == null) {
                return;
            }

            if (ReferenceEquals(_executor, executor)) {
                return;
            }

            ClearAppBindings();
            _executor = executor;
            _hostEpoch++;
            _executorUnavailableLogged = false;
        }

        /// <summary>Drops the bound host and every application subscription; internal entry.</summary>
        internal void Unbind() {
            ClearAppBindings();
            _executor = null;
            _hostEpoch++;
            _executorUnavailableLogged = false;
        }

        /// <summary>Starts a coroutine on the host; returns <c>null</c> while it is unavailable.</summary>
        public Coroutine StartCoroutine(IEnumerator routine) {
            var executor = _executor;
            if (executor == null || !executor.Valid) {
                LogUnavailable(nameof(StartCoroutine));
                return null;
            }

            return executor.StartCoroutine(routine);
        }

        /// <summary>Stops a coroutine on the host; unavailable hosts are ignored.</summary>
        public void StopCoroutine(Coroutine coroutine) {
            var executor = _executor;
            if (executor == null || !executor.Valid) {
                LogUnavailable(nameof(StopCoroutine));
                return;
            }

            executor.StopCoroutine(coroutine);
        }

        /// <summary>
        /// Subscribes the target to quit, focus and pause. The call is all-or-nothing and retryable.
        /// </summary>
        public bool AttachApp(ILifecycle target) {
            if (target == null) {
                return false;
            }

            if (!Valid) {
                // Drop a stale binding left by a host that was destroyed before detaching.
                DetachApp(target);
                LogUnavailable(nameof(AttachApp));
                return false;
            }

            if (_appBindings.TryGetValue(target, out _)) {
                return true;
            }

            var appQuit = AppQuit;
            var appFocus = AppFocus;
            var appPause = AppPause;
            if (appQuit == null || appFocus == null || appPause == null) {
                return false;
            }

            var subscriptions = new SubscriptionBag();
            try {
                subscriptions.Add(appQuit.Subscribe(target.OnAppQuit));
                subscriptions.Add(appFocus.Subscribe(target.OnAppFocus));
                subscriptions.Add(appPause.Subscribe(target.OnAppPause));
            } catch (ObjectDisposedException) {
                // The host was quitting while we tried to attach; leave no partial subscription.
                subscriptions.Dispose();
                return false;
            } catch {
                // Any other failure must also roll back the phases that already subscribed.
                subscriptions.Dispose();
                throw;
            }

            _appBindings.Add(target, subscriptions);
            return true;
        }

        /// <summary>Releases the target's application subscriptions; safe to repeat.</summary>
        public void DetachApp(ILifecycle target) {
            if (target == null) {
                return;
            }

            if (_appBindings.TryGetValue(target, out var subscriptions)) {
                _appBindings.Remove(target);
                subscriptions.Dispose();
            }
        }

        private GrowEvent Get(Func<IMonoExecutor, GrowEvent> selector, string accessorName) {
            var executor = _executor;
            if (executor == null || !executor.Valid) {
                LogUnavailable(accessorName);
                return null;
            }

            return selector(executor);
        }

        private GrowEvent<bool> Get(Func<IMonoExecutor, GrowEvent<bool>> selector, string accessorName) {
            var executor = _executor;
            if (executor == null || !executor.Valid) {
                LogUnavailable(accessorName);
                return null;
            }

            return selector(executor);
        }

        private void LogUnavailable(string accessorName) {
            if (_executorUnavailableLogged) {
                return;
            }

            _executorUnavailableLogged = true;
            GrowLog.Error(
                $"[MonoManager] The loop executor is unavailable; cannot access {accessorName}. " +
                "Call Bind(IMonoExecutor) after GrowLoop.Install().",
                LogCategory);
        }

        private void ClearAppBindings() {
            foreach (var pair in _appBindings) {
                pair.Value.Dispose();
            }

            _appBindings = new ConditionalWeakTable<ILifecycle, SubscriptionBag>();
        }

        /// <summary>True while application subscriptions are attached for the target.</summary>
        public bool IsAppAttached(ILifecycle target) {
            return target != null && _appBindings.TryGetValue(target, out _);
        }

        /// <summary>Drops the singleton and its subscriptions at the start of a play session.</summary>
        internal static void ResetStaticState() {
            _instance?.ClearAppBindings();
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnLoad() {
            ResetStaticState();
        }
    }
}
