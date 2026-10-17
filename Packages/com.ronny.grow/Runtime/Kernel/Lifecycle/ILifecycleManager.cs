// SPDX-License-Identifier: MIT

using System.Collections;
using Grow.Core.Events;
using UnityEngine;

namespace Grow.Kernel.Lifecycle {
    /// <summary>
    /// Public lifecycle-facing host contract.
    /// </summary>
    /// <remarks>
    /// It exposes the phase events and coroutine entry points needed by lifecycle consumers and adds
    /// application-level attachment. The manager owns the subscriptions created by
    /// <see cref="AttachApp"/>, so implementers do not have to store
    /// <see cref="Grow.Core.Events.GrowSubscription"/> values.
    /// <para>
    /// <see cref="AttachApp"/> is all-or-nothing and retryable: it returns <c>false</c> while the
    /// host is unavailable, and a later call can complete the subscription. Repeating it is
    /// idempotent. <see cref="DetachApp"/> is idempotent and safe after the host is gone.
    /// </para>
    /// </remarks>
    public interface ILifecycleManager {
        /// <summary>Raised once when the application is quitting.</summary>
        GrowEvent AppQuit { get; }

        /// <summary>Raised before scene scripts update.</summary>
        GrowEvent PreUpdate { get; }

        /// <summary>Raised after the fixed-update group.</summary>
        GrowEvent FixedUpdate { get; }

        /// <summary>Raised after scene scripts update.</summary>
        GrowEvent Update { get; }

        /// <summary>Raised after the whole update group, before late update.</summary>
        GrowEvent PostUpdate { get; }

        /// <summary>Raised after scene scripts late-update.</summary>
        GrowEvent LateUpdate { get; }

        /// <summary>Raised when the application gains or loses focus.</summary>
        GrowEvent<bool> AppFocus { get; }

        /// <summary>Raised when the application pauses or resumes.</summary>
        GrowEvent<bool> AppPause { get; }

        /// <summary>True while the underlying host object is alive.</summary>
        bool Valid { get; }

        /// <summary>Starts a coroutine on the host.</summary>
        Coroutine StartCoroutine(IEnumerator routine);

        /// <summary>Stops a coroutine started by this host.</summary>
        void StopCoroutine(Coroutine coroutine);

        /// <summary>
        /// Host generation: incremented whenever a different loop host is bound or the host is
        /// unbound. Subscribers compare it to their captured generation so a stale subscription is
        /// rebuilt instead of silently continuing against the old host.
        /// </summary>
        int HostEpoch => 0;

        /// <summary>
        /// Subscribes <paramref name="target"/> to quit, focus and pause events.
        /// </summary>
        /// <returns>
        /// <c>true</c> when the target is attached; <c>false</c> when the host is unavailable. A
        /// failed attempt leaves no partial subscription behind.
        /// </returns>
        bool AttachApp(ILifecycle target);

        /// <summary>Releases the application subscriptions held for <paramref name="target"/>.</summary>
        void DetachApp(ILifecycle target);

        /// <summary>True while application subscriptions are attached for <paramref name="target"/>.</summary>
        bool IsAppAttached(ILifecycle target);
    }
}
