// SPDX-License-Identifier: MIT

using System;
using Grow.Kernel.Lifecycle.Impl;

namespace Grow.Kernel.Lifecycle {
    /// <summary>
    /// Base class for self-managed plain C# objects with a Unity-like enabled state machine.
    /// </summary>
    /// <remarks>
    /// The object owns its lifecycle: construction runs <c>OnAwake</c>, attaches app events and
    /// applies the initial enabled state; <c>Dispose</c> releases update subscriptions, calls
    /// <c>OnDestroyed</c> and detaches app events. Update phases are declared with
    /// <see cref="UpdateFlag"/> and routed through <see cref="UpdaterBinding"/>.
    /// <para>
    /// Enable / disable is edge-triggered: <c>OnEnabled</c> runs on the rising edge, <c>OnDisabled</c>
    /// on the falling edge, and <c>OnStart</c> runs only on the first enable. Disabling in
    /// <c>OnAwake</c> therefore produces no spurious disable callback during construction.
    /// </para>
    /// <para>
    /// There is no finalizer: while subscribed, the manager holds a reference to the object, so a
    /// finalizer could not run reliably and would violate the event owner-thread contract. Call
    /// <see cref="Dispose"/> explicitly.
    /// </para>
    /// </remarks>
    public abstract class UnifiedClass : ILifecycle, IDisposable, ILifecycleUpdateHooks {
        private readonly UpdaterBinding _updater;
        private readonly UpdateFlag _updateFlag;

        private bool _enabled = true;
        private bool _deliveredEnabled;
        private bool _started;
        private bool _destroyed;

        /// <summary>Creates the object, runs <c>OnAwake</c>, attaches app events and applies the state.</summary>
        protected UnifiedClass() {
            _updateFlag = UpdateFlag;
            _updater = new UpdaterBinding(_updateFlag, this, MonoManager.Instance);
            ((ILifecycle)this).OnAwake();
            ((ILifecycle)this).AttachApp();
            ApplyEnabledState();
        }

        /// <summary>Declares which update phases this object needs.</summary>
        protected virtual UpdateFlag UpdateFlag => UpdateFlag.None;

        /// <summary>The update phases captured during construction.</summary>
        protected UpdateFlag DeclaredUpdateFlag => _updateFlag;

        /// <summary>Gets or sets whether the object is enabled.</summary>
        public bool Enabled {
            get => _enabled;
            set {
                if (_enabled == value) return;
                _enabled = value;
                ApplyEnabledState();
            }
        }

        /// <summary>Whether app-level subscriptions are currently attached.</summary>
        public bool AttachedApp => ((ILifecycle)this).AttachedApp;
        bool ILifecycle.AttachedApp => MonoManager.Instance.IsAppAttached(this);

        /// <summary>Whether the updater subscriptions are currently attached.</summary>
        public bool AttachedUpdater => _updater.Attached;

        /// <summary>Detaches updates, runs <c>OnDestroyed</c> and detaches app events; idempotent.</summary>
        public void Dispose() {
            if (_destroyed) return;
            _destroyed = true;

            DetachUpdater();
            OnDestroyed();
            ((ILifecycle)this).DetachApp();
        }

        ILifecycleManager ILifecycle.LifecycleManager => MonoManager.Instance;

        void ILifecycle.OnAwake() => OnAwake();
        void ILifecycle.OnEnabled() => OnEnabled();
        void ILifecycle.OnStart() => OnStart();
        void ILifecycle.OnFixedUpdate() => OnFixedUpdate();
        void ILifecycle.OnPreUpdate() => OnPreUpdate();
        void ILifecycle.OnUpdate() => OnUpdate();
        void ILifecycle.OnLateUpdate() => OnLateUpdate();
        void ILifecycle.OnPostUpdate() => OnPostUpdate();
        void ILifecycle.OnDisabled() => OnDisabled();
        void ILifecycle.OnDestroyed() => OnDestroyed();

        void ILifecycle.OnAppFocus(bool hasFocus) {
            if (_deliveredEnabled) OnAppFocus(hasFocus);
        }

        void ILifecycle.OnAppPause(bool isPause) {
            if (_deliveredEnabled) OnAppPause(isPause);
        }

        void ILifecycle.OnAppQuit() {
            OnAppQuit();
            if (_deliveredEnabled) Enabled = false;
            Dispose();
        }

        /// <summary>Called once during construction, before the initial enabled state is applied.</summary>
        protected virtual void OnAwake() {
        }

        /// <summary>Called on the rising edge of <see cref="Enabled"/>.</summary>
        protected virtual void OnEnabled() {
        }

        /// <summary>Called once, on the first enable.</summary>
        protected virtual void OnStart() {
        }

        /// <summary>Called at the fixed interval when enabled and declared.</summary>
        protected virtual void OnFixedUpdate() {
        }

        /// <summary>Called before scene scripts update when enabled and declared.</summary>
        protected virtual void OnPreUpdate() {
        }

        /// <summary>Called each frame when enabled and declared.</summary>
        protected virtual void OnUpdate() {
        }

        /// <summary>Called after the update group when enabled and declared.</summary>
        protected virtual void OnLateUpdate() {
        }

        /// <summary>Called after the whole update group when enabled and declared.</summary>
        protected virtual void OnPostUpdate() {
        }

        /// <summary>Called while enabled when the application gains or loses focus.</summary>
        protected virtual void OnAppFocus(bool hasFocus) {
        }

        /// <summary>Called while enabled when the application pauses or resumes.</summary>
        protected virtual void OnAppPause(bool isPause) {
        }

        /// <summary>Called while the application is quitting, before the object is disposed.</summary>
        protected virtual void OnAppQuit() {
        }

        /// <summary>Called on the falling edge of <see cref="Enabled"/>.</summary>
        protected virtual void OnDisabled() {
        }

        /// <summary>Called once during <see cref="Dispose"/>.</summary>
        protected virtual void OnDestroyed() {
        }

        /// <summary>Subscribes the declared update phases; idempotent.</summary>
        protected void AttachUpdater() {
            _updater.Attach();
        }

        /// <summary>Releases the update subscriptions; idempotent.</summary>
        protected void DetachUpdater() {
            _updater.Detach();
        }

        void ILifecycleUpdateHooks.OnFixedUpdate() => OnFixedUpdate();
        void ILifecycleUpdateHooks.OnPreUpdate() => OnPreUpdate();
        void ILifecycleUpdateHooks.OnUpdate() => OnUpdate();
        void ILifecycleUpdateHooks.OnLateUpdate() => OnLateUpdate();
        void ILifecycleUpdateHooks.OnPostUpdate() => OnPostUpdate();

        private void ApplyEnabledState() {
            if (_destroyed) return;

            if (_enabled && !_deliveredEnabled) {
                _deliveredEnabled = true;
                OnEnabled();
                if (!_enabled || _destroyed) return;

                if (!_started) {
                    _started = true;
                    OnStart();
                    if (!_enabled || _destroyed) return;
                }

                AttachUpdater();
            } else if (!_enabled && _deliveredEnabled) {
                _deliveredEnabled = false;
                OnDisabled();
                DetachUpdater();
            }
        }
    }
}
