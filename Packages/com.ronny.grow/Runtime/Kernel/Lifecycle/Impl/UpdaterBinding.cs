// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Events;
using Grow.Core.Log;

namespace Grow.Kernel.Lifecycle.Impl {
    /// <summary>Callbacks routed by <see cref="UpdaterBinding"/>.</summary>
    internal interface ILifecycleUpdateHooks {
        void OnFixedUpdate();
        void OnPreUpdate();
        void OnUpdate();
        void OnLateUpdate();
        void OnPostUpdate();
    }

    /// <summary>
    /// Maps an <see cref="UpdateFlag"/> to the matching lifecycle-manager events.
    /// </summary>
    /// <remarks>
    /// This is the single attach/detach implementation shared by the lifecycle base classes. Attach
    /// is incremental and retryable: each phase that subscribed successfully is remembered, so a
    /// later attempt only fills in the phases that were missing. <see cref="Attached"/> becomes true
    /// only when every declared phase is subscribed. Detach releases the whole token group and is
    /// idempotent.
    /// <para>
    /// The binding keeps delegates cached per phase, so repeated attach/detach does not allocate a
    /// new method-group delegate on every frame.
    /// </para>
    /// </remarks>
    internal sealed class UpdaterBinding {
        private const string LogCategory = "Lifecycle";

        private readonly UpdateFlag _flag;
        private readonly ILifecycleUpdateHooks _hooks;
        private readonly ILifecycleManager _manager;

        private SubscriptionBag _subscriptions;

        private Action _fixedUpdateAction;
        private Action _preUpdateAction;
        private Action _updateAction;
        private Action _lateUpdateAction;
        private Action _postUpdateAction;

        private bool _fixedUpdateAttached;
        private bool _preUpdateAttached;
        private bool _updateAttached;
        private bool _lateUpdateAttached;
        private bool _postUpdateAttached;
        private bool _unavailableWarned;
        private bool _attached;
        private int _attachedEpoch = -1;

        public UpdaterBinding(UpdateFlag flag, ILifecycleUpdateHooks hooks, ILifecycleManager manager) {
            _flag = flag;
            _hooks = hooks;
            _manager = manager;
        }

        /// <summary>True only when every declared phase is subscribed to the current host.</summary>
        public bool Attached {
            get => _attached && _attachedEpoch == CurrentEpoch;
            internal set {
                _attached = value;
                _attachedEpoch = value ? CurrentEpoch : -1;
            }
        }

        private int CurrentEpoch => _manager != null ? _manager.HostEpoch : 0;

        private Action FixedUpdateAction => _fixedUpdateAction ??= _hooks.OnFixedUpdate;
        private Action PreUpdateAction => _preUpdateAction ??= _hooks.OnPreUpdate;
        private Action UpdateAction => _updateAction ??= _hooks.OnUpdate;
        private Action LateUpdateAction => _lateUpdateAction ??= _hooks.OnLateUpdate;
        private Action PostUpdateAction => _postUpdateAction ??= _hooks.OnPostUpdate;

        /// <summary>Subscribes every declared phase that is not attached yet.</summary>
        public void Attach() {
            var epoch = CurrentEpoch;
            if (_attachedEpoch != epoch) Detach();
            if (Attached) return;

            var manager = _manager;
            if (manager == null || !manager.Valid) {
                WarnUnavailable();
                return;
            }

            if (Has(UpdateFlag.FixedUpdate) && !_fixedUpdateAttached) {
                if (!TryAttach(manager.FixedUpdate, FixedUpdateAction, ref _fixedUpdateAttached)) return;
            }

            if (Has(UpdateFlag.PreUpdate) && !_preUpdateAttached) {
                if (!TryAttach(manager.PreUpdate, PreUpdateAction, ref _preUpdateAttached)) return;
            }

            if (Has(UpdateFlag.Update) && !_updateAttached) {
                if (!TryAttach(manager.Update, UpdateAction, ref _updateAttached)) return;
            }

            if (Has(UpdateFlag.LateUpdate) && !_lateUpdateAttached) {
                if (!TryAttach(manager.LateUpdate, LateUpdateAction, ref _lateUpdateAttached)) return;
            }

            if (Has(UpdateFlag.PostUpdate) && !_postUpdateAttached) {
                if (!TryAttach(manager.PostUpdate, PostUpdateAction, ref _postUpdateAttached)) return;
            }

            _unavailableWarned = false;
            _attached = true;
            _attachedEpoch = epoch;
        }

        /// <summary>Releases every declared phase; safe to call more than once.</summary>
        public void Detach() {
            _attached = false;
            _attachedEpoch = -1;

            if (_subscriptions != null) {
                _subscriptions.Dispose();
                _subscriptions = null;
            }

            ResetAttachedFlags();
        }

        private bool TryAttach(GrowEvent executor, Action action, ref bool attached) {
            if (executor == null) {
                WarnUnavailable();
                return false;
            }

            _subscriptions ??= new SubscriptionBag();
            try {
                var subscription = executor.Subscribe(action);
                if (!subscription.IsActive) {
                    WarnUnavailable();
                    return false;
                }

                _subscriptions.Add(subscription);
            } catch (ObjectDisposedException) {
                WarnUnavailable();
                return false;
            }

            attached = true;
            return true;
        }

        private bool Has(UpdateFlag flag) {
            return (_flag & flag) != 0;
        }

        private void ResetAttachedFlags() {
            _fixedUpdateAttached = false;
            _preUpdateAttached = false;
            _updateAttached = false;
            _lateUpdateAttached = false;
            _postUpdateAttached = false;
        }

        private void WarnUnavailable() {
            if (_unavailableWarned) return;
            _unavailableWarned = true;
            GrowLog.Warning(
                "[UpdaterBinding] The lifecycle updater is unavailable; update callbacks were not " +
                "registered. Bind an executor, then attach the target again.",
                LogCategory);
        }
    }
}
