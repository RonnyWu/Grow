// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using UnityEngine;

namespace Grow.Kernel.Loop {
    /// <summary>
    /// Public facade for the kernel's player-loop host.
    /// </summary>
    /// <remarks>
    /// <see cref="Install"/> creates the single <c>Grow</c> host object (kept across scenes while
    /// playing), installs the five frame phases and exposes the host to kernel internals.
    /// <see cref="GrowHost"/> is intentionally not a separate type: <c>MonoExecutor</c> is the host,
    /// and this facade is the only entry point.
    /// <para>
    /// Install and uninstall are idempotent. The facade is static because the host is a singleton
    /// mechanism; the static host references are cleared at <c>SubsystemRegistration</c>, so a
    /// session without Domain Reload cannot keep a destroyed host alive.
    /// </para>
    /// </remarks>
    public static class GrowLoop {
        private static MonoExecutor _executor;
        private static GameObject _root;

        /// <summary>True while the host exists and the phase profile is installed.</summary>
        public static bool IsInstalled => _executor != null && _executor.Valid && PlayerLoopProfile.Installed;

        /// <summary>The host contract, or <c>null</c> when the loop is not installed.</summary>
        internal static IMonoExecutor Executor => _executor;

        /// <summary>The concrete host, for kernel internals and tests.</summary>
        internal static MonoExecutor Host => _executor;

        /// <summary>
        /// Set by the composition root. The loop calls it once after publishing <c>AppQuit</c>;
        /// it is the seam that lets the loop stay independent of the Entry layer.
        /// </summary>
        internal static Action QuitCommitted;

        /// <summary>Creates the host and installs the five frame phases.</summary>
        /// <exception cref="InvalidOperationException">A phase anchor is missing; the host was cleaned up.</exception>
        public static void Install() {
            if (IsInstalled) return;
            if (_executor != null) Uninstall();

            var root = new GameObject("Grow");
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(root);

            var executor = root.AddComponent<MonoExecutor>();
            _root = root;
            _executor = executor;

            if (PlayerLoopProfile.Install(executor)) return;

            Uninstall();
            throw new InvalidOperationException(
                "GrowLoop install failed: a PlayerLoop anchor is missing; the host was cleaned up.");
        }

        /// <summary>Removes the phase profile and destroys the host; safe to call repeatedly.</summary>
        public static void Uninstall() {
            var executor = _executor;
            PlayerLoopProfile.Uninstall(executor);

            if (_root != null) {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
                else UnityEngine.Object.DestroyImmediate(_root);
            }

            _root = null;
            _executor = null;
        }

        /// <summary>Runs the composition-root quit callback once, if one is installed.</summary>
        internal static void NotifyQuitCommitted() {
            var action = QuitCommitted;
            QuitCommitted = null;
            action?.Invoke();
        }

        /// <summary>Starts a coroutine on the host.</summary>
        /// <exception cref="InvalidOperationException">The loop is not installed.</exception>
        public static Coroutine StartCoroutine(IEnumerator routine) {
            if (_executor == null || !_executor.Valid)
                throw new InvalidOperationException("GrowLoop is not installed; call Install() first.");

            return _executor.StartCoroutine(routine);
        }

        /// <summary>Stops a coroutine started by <see cref="StartCoroutine"/>.</summary>
        public static void StopCoroutine(Coroutine coroutine) {
            if (_executor == null || !_executor.Valid) return;
            _executor.StopCoroutine(coroutine);
        }

        /// <summary>Clears the static host references at the start of every play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState() {
            PlayerLoopProfile.Uninstall();
            PlayerLoopProfile.ResetStaticState();
            QuitCommitted = null;

            if (_root != null) {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
                else UnityEngine.Object.DestroyImmediate(_root);
            }

            _root = null;
            _executor = null;
        }
    }
}
