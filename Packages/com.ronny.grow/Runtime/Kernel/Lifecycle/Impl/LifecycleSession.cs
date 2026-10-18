// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Grow.Kernel.Lifecycle.Impl {
    /// <summary>
    /// Session generation and main-thread identity shared by lifecycle services.
    /// </summary>
    /// <remarks>
    /// A generic type cannot receive a single <c>RuntimeInitializeOnLoadMethod</c> callback for
    /// every closed construction, so <see cref="UnifiedClassSingleton{T}"/> compares this generation
    /// lazily on first access. <see cref="Entry.GrowSession"/> increments the counter at
    /// <c>SubsystemRegistration</c>, which runs even when the domain reload is disabled. The
    /// main-thread id is captured at the same boundary; the static constructor is a fallback for
    /// EditMode/test access.
    /// </remarks>
    internal static class LifecycleSession {
        /// <summary>Current play-session generation.</summary>
        public static int Generation { get; private set; }

        /// <summary>Thread id that owns the current session's main-thread services.</summary>
        public static int MainThreadId { get; private set; }

        /// <summary>True when the current thread is the captured session main thread.</summary>
        public static bool IsMainThread => Environment.CurrentManagedThreadId == MainThreadId;

        static LifecycleSession() {
            MainThreadId = Environment.CurrentManagedThreadId;
        }

        /// <summary>Advances the generation; called by <c>GrowSession</c> and by session tests.</summary>
        internal static void ResetForSession() {
            Generation++;
            MainThreadId = Environment.CurrentManagedThreadId;
        }


    }
}
