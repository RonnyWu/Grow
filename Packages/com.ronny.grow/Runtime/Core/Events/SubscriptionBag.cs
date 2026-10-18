// SPDX-License-Identifier: MIT

using System;

namespace Grow.Core.Events {
    /// <summary>
    /// A small collection of <see cref="GrowSubscription"/> handles that disposes them together.
    /// </summary>
    /// <remarks>
    /// Use it when one owner has several subscriptions and they should be released at the same
    /// boundary (for example a view's <c>OnDestroy</c>). Disposal is LIFO and idempotent; adding an
    /// already inactive token is ignored, so a stale token cannot keep the bag alive.
    /// <para>
    /// Like the events themselves, a bag is single-threaded: add and dispose it on the owner thread
    /// of the subscriptions. It has no static state and does not depend on Unity.
    /// </para>
    /// </remarks>
    public sealed class SubscriptionBag : IDisposable {
        private GrowSubscription[] _items;
        private int _count;
        private bool _disposed;

        /// <summary>Number of tokens currently held by the bag (including tokens released elsewhere).</summary>
        public int Count => _count;

        /// <summary>
        /// Adds a token. An inactive token (already disposed, or its publisher was disposed) is
        /// ignored.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The bag has already been disposed.</exception>
        public void Add(GrowSubscription subscription) {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(SubscriptionBag));
            }

            if (!subscription.IsActive) {
                return;
            }

            if (_items == null) {
                _items = new GrowSubscription[4];
            } else if (_count == _items.Length) {
                Array.Resize(ref _items, _items.Length * 2);
            }

            _items[_count++] = subscription;
        }

        /// <summary>Disposes every held token in reverse order; safe to call more than once.</summary>
        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;

            for (var i = _count - 1; i >= 0; i--) {
                _items[i].Dispose();
            }

            if (_items != null) {
                Array.Clear(_items, 0, _count);
            }

            _count = 0;
        }
    }
}
