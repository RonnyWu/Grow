// SPDX-License-Identifier: MIT

using Grow.Core.Log;
using Grow.Core.Pool;
using Grow.Kernel.Ioc;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Lifecycle.Impl;
using Grow.Kernel.Loop;
using UnityEngine;

namespace Grow.Kernel.Entry {
    /// <summary>
    /// The single <c>SubsystemRegistration</c> entry that clears static session state.
    /// </summary>
    /// <remarks>
    /// Unity runs every <c>SubsystemRegistration</c> callback in an unspecified order. Keeping one
    /// callback here makes the order explicit and prevents a new kernel subsystem from adding a
    /// tenth competing hook. The sequence clears composition flags, the IOC root, lifecycle
    /// bindings, the loop host and the player-loop registry before clearing logging and pooling
    /// statics.
    /// <para>
    /// Capabilities are deliberately absent: a capability owns its runtime state in its module and
    /// releases it through <c>IModule.OnDispose</c>. The composition root settles the module registry
    /// at <c>ExitingPlayMode</c>; this hook must not run user module shutdown code during startup.
    /// </para>
    /// <para>
    /// The individual reset methods remain callable by tests; only the engine hook moved here.
    /// </para>
    /// </remarks>
    internal static class GrowSession {
        /// <summary>Resets every kernel/Core static that survives a disabled domain reload.</summary>
        internal static void ResetStaticState() {
            GrowApp.ResetStaticState();
            GrowRuntime.ResetStaticState();
            IOC.ResetStaticState();
            MonoManager.ResetStaticState();
            LifecycleSession.ResetForSession();
            GrowLoop.ResetStaticState();
            PlayerLoopManager.ResetStaticState();
            GrowLog.Reset();
            StringBuilderPool.ResetStaticState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() {
            ResetStaticState();
        }
    }
}
