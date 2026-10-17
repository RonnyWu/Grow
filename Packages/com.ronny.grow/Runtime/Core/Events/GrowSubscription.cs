// SPDX-License-Identifier: MIT

using System;

namespace Grow.Core.Events {
    /// <summary>
    /// Handle for one subscription created by <see cref="GrowEvent.Subscribe"/> or
    /// <see cref="GrowEvent{T}.Subscribe"/>.
    /// </summary>
    /// <remarks>
    /// A token identifies exactly one subscription, so the same handler may be subscribed more than
    /// once and each copy can be removed independently. Disposing a token twice, or disposing
    /// <c>default</c>, is safe. Disposing it on a thread other than the event's owner thread throws,
    /// because subscription storage is single-threaded by contract.
    /// <para>
    /// Keep the concrete <see cref="GrowSubscription"/> type (for example with <c>using var</c> or a
    /// field). Assigning it to <see cref="IDisposable"/> boxes the struct once per assignment.
    /// </para>
    /// </remarks>
    public readonly struct GrowSubscription : IDisposable {
        private readonly ISubscriptionOwner _owner;
        private readonly int _id;

        internal GrowSubscription(ISubscriptionOwner owner, int id) {
            _owner = owner;
            _id = id;
        }

        /// <summary>True while this subscription is active (or will be after the current pass).</summary>
        public bool IsActive => _owner != null && _owner.IsActive(_id);

        /// <summary>Removes this subscription. Safe to call more than once.</summary>
        public void Dispose() {
            _owner?.Unsubscribe(_id);
        }
    }
}
