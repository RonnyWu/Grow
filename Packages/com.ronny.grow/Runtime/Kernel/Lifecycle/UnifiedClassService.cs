// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Log;
using Grow.Kernel.Lifecycle.Impl;
using Grow.Kernel.Scheduling;

namespace Grow.Kernel.Lifecycle {
    /// <summary>
    /// Lifecycle base for singleton managed services.
    /// </summary>
    /// <remarks>
    /// The service has a fixed order: business initialize → contract registration → business start →
    /// business updates → business teardown → static reset → base disposal. The Unity-style entries
    /// (<c>OnAwake</c>, <c>OnStart</c>, <c>OnUpdate</c>, <c>OnDestroyed</c>) are sealed so a subclass
    /// cannot forget a base call; subclasses override the <c>OnService*</c> hooks instead.
    /// <para>
    /// Each stage has its own success flag. A hook that throws is logged and does not set its flag,
    /// so the next entry can retry without repeating the stages that already succeeded. Cross-thread
    /// actions handed to <see cref="PostToMain"/> go through <see cref="GrowScheduler.Post"/> and are
    /// drained before <see cref="OnServiceUpdate"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The concrete service type.</typeparam>
    public abstract class UnifiedClassService<T> : UnifiedClassSingleton<T>, IManagedService
        where T : UnifiedClassService<T>, new() {
        private const string LogCategory = "Lifecycle";

        private bool _serviceInitialized;
        private bool _contractsRegistered;
        private bool _serviceStarted;
        private bool _serviceTornDown;
        private bool _staticsReset;

        /// <summary>True when the current thread is the captured session main thread.</summary>
        protected static bool IsMainThread => LifecycleSession.IsMainThread;

        /// <summary>Creates the singleton if needed; intended for a bootstrap hook.</summary>
        protected static T Bootstrap() {
            return Instance;
        }

        /// <summary>Business initialization, before contract registration.</summary>
        protected virtual void OnServiceInitialize() {
        }

        /// <summary>Registers the service's contracts after initialization.</summary>
        protected virtual void OnServiceContractsRegister() {
        }

        /// <summary>Business activation; runs once, after initialization and contract registration.</summary>
        protected virtual void OnServiceStart() {
        }

        /// <summary>Business update; runs after the main-thread queue is drained.</summary>
        protected virtual void OnServiceUpdate() {
        }

        /// <summary>Business teardown, before <see cref="ResetStatics"/> and base disposal.</summary>
        protected virtual void OnServiceTeardown() {
        }

        /// <summary>Drops static or long-lived state owned by the service.</summary>
        protected virtual void ResetStatics() {
        }

        /// <summary>
        /// Runs <paramref name="action"/> on the main thread: inline on the main thread, otherwise
        /// handed to the kernel scheduler.
        /// </summary>
        /// <remarks>
        /// Off the main thread the action goes into <see cref="GrowScheduler.Post"/>, which is the one
        /// cross-thread hand-off the kernel owns. It runs on the next <c>PreUpdate</c>, or from
        /// <see cref="OnUpdate"/> when no loop host is attached (see
        /// <see cref="FlushMainThreadQueue"/>).
        /// </remarks>
        protected void PostToMain(Action action) {
            if (action == null) {
                return;
            }

            if (IsMainThread) {
                action();
                return;
            }

            GrowScheduler.Post(action);
        }

        /// <summary>Drains actions queued by <see cref="PostToMain"/>; isolates each failure.</summary>
        protected void FlushMainThreadQueue() {
            GrowScheduler.FlushPosted();
        }

        private void AwakeService() {
            if (!_serviceInitialized) {
                try {
                    OnServiceInitialize();
                    _serviceInitialized = true;
                } catch (Exception exception) {
                    GrowLog.Error(
                        $"{typeof(T).Name} OnServiceInitialize failed: {exception}",
                        LogCategory);
                }
            }

            if (!_contractsRegistered) {
                try {
                    OnServiceContractsRegister();
                    _contractsRegistered = true;
                } catch (Exception exception) {
                    GrowLog.Error(
                        $"{typeof(T).Name} OnServiceContractsRegister failed: {exception}",
                        LogCategory);
                }
            }
        }

        private void StartService() {
            if (_serviceStarted || !_serviceInitialized) {
                return;
            }

            try {
                OnServiceStart();
                _serviceStarted = true;
            } catch (Exception exception) {
                GrowLog.Error($"{typeof(T).Name} OnServiceStart failed: {exception}", LogCategory);
            }
        }

        private void TeardownService() {
            if (!_serviceTornDown) {
                try {
                    OnServiceTeardown();
                    _serviceTornDown = true;
                } catch (Exception exception) {
                    GrowLog.Error(
                        $"{typeof(T).Name} OnServiceTeardown failed: {exception}",
                        LogCategory);
                }
            }

            if (!_staticsReset) {
                try {
                    ResetStatics();
                    _staticsReset = true;
                } catch (Exception exception) {
                    GrowLog.Error($"{typeof(T).Name} ResetStatics failed: {exception}", LogCategory);
                }
            }
        }

        void IInitializable.OnInitialize() {
            AwakeService();
        }

        void IStartable.OnStart() {
            StartService();
        }

        void IDeinitializable.OnDeinitialize() {
            TeardownService();
        }

        protected sealed override void OnAwake() {
            base.OnAwake();
            AwakeService();
        }

        protected sealed override void OnStart() {
            base.OnStart();
            StartService();
        }

        protected sealed override void OnUpdate() {
            base.OnUpdate();
            FlushMainThreadQueue();
            OnServiceUpdate();
        }

        protected sealed override void OnDestroyed() {
            TeardownService();
            base.OnDestroyed();
        }
    }
}
