// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Log;
using Grow.Kernel.Lifecycle.Impl;

namespace Grow.Kernel.Lifecycle {
    /// <summary>
    /// Singleton base for self-managed lifecycle objects.
    /// </summary>
    /// <remarks>
    /// The parameterless constructor is intentionally required by the <c>new()</c> constraint:
    /// avoiding reflection keeps the singleton IL2CPP-friendly and makes the construction path
    /// explicit. Call <see cref="Instance"/> from a bootstrap hook; it is created lazily and is
    /// dropped by <see cref="UnifiedClass.Dispose"/>.
    /// <para>
    /// After the application has quit, <see cref="Instance"/> returns <c>null</c> and does not
    /// recreate the object. <see cref="ResetStaticState"/> exists for tests and hosts that reset
    /// statics manually; a generic type cannot receive a single domain-reload hook automatically.
    /// </para>
    /// </remarks>
    public abstract class UnifiedClassSingleton<T> : UnifiedClass
        where T : UnifiedClassSingleton<T>, new() {
        private const string LogCategory = "Lifecycle";

        private static readonly object Gate = new object();
        private static T _instance;
        private static bool _appQuitting;
        private static int _generation;

        /// <summary>The singleton, or <c>null</c> after the application has quit.</summary>
        public static T Instance {
            get {
                EnsureSession();
                if (_appQuitting) {
                    return null;
                }

                var instance = _instance;
                if (instance != null) {
                    return instance;
                }

                lock (Gate) {
                    if (_appQuitting) {
                        return null;
                    }

                    instance = _instance;
                    if (instance != null) {
                        return instance;
                    }

                    try {
                        instance = new T();
                        _instance = instance;
                        return instance;
                    } catch (Exception exception) {
                        GrowLog.Error(
                            $"[{typeof(T).Name}] Singleton construction failed: {exception}",
                            LogCategory);
                        return null;
                    }
                }
            }
        }

        /// <summary>True while an instance exists and the application has not quit.</summary>
        public static bool HasInstance {
            get {
                EnsureSession();
                return _instance != null && !_appQuitting;
            }
        }

        /// <summary>Explicitly creates the singleton if it does not exist.</summary>
        public static T Initialize() {
            return Instance;
        }

        /// <summary>Drops the cached instance and the quit latch; intended for tests and session resets.</summary>
        public static void ResetStaticState() {
            _generation = LifecycleSession.Generation;
            _instance = null;
            _appQuitting = false;
        }

        private static void EnsureSession() {
            var generation = LifecycleSession.Generation;
            if (_generation == generation) {
                return;
            }

            _generation = generation;
            _instance = null;
            _appQuitting = false;
        }

        protected override void OnDestroyed() {
            if (ReferenceEquals(_instance, this)) {
                _instance = null;
            }

            base.OnDestroyed();
        }

        protected override void OnAppQuit() {
            _appQuitting = true;
            base.OnAppQuit();
        }
    }
}
