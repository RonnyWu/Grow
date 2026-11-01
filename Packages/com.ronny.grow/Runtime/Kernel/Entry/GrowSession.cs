// SPDX-License-Identifier: MIT

using Grow.Core.Log;
using Grow.Core.Pool;
using Grow.Kernel.Ioc;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Lifecycle.Impl;
using Grow.Kernel.Loop;
using Grow.Kernel.Scheduling;
using UnityEngine;

namespace Grow.Kernel.Entry {
    /// <summary>
    /// The kernel-side <c>SubsystemRegistration</c> entry that clears static session state.
    /// </summary>
    /// <remarks>
    /// Unity runs every <c>SubsystemRegistration</c> callback in an unspecified order. Keeping the
    /// kernel's resets here makes their order explicit; Core primitives may keep their own hooks
    /// because they cannot depend on the kernel. The sequence clears composition flags, the IOC
    /// root, lifecycle bindings, the loop host and the player-loop registry before clearing logging
    /// and pooling statics. The scheduler is reset right after the loop host, because its frame
    /// subscriptions belong to that host.
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
            GrowScheduler.ResetStaticState();
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
