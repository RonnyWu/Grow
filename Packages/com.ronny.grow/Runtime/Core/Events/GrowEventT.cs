// SPDX-License-Identifier: MIT

using System;
using System.Runtime.ExceptionServices;

namespace Grow.Core.Events {
    /// <summary>
    /// Subscription surface of an event that carries one payload value. Only the nested
    /// <see cref="Publisher"/> can publish or clear it.
    /// </summary>
    /// <remarks>
    /// Use a small value type for <typeparamref name="T"/> (for example a <c>readonly struct</c> that
    /// groups several fields). The payload is passed to <see cref="Action{T}"/> by value; use a
    /// reference type when the payload is large.
    /// <para>
    /// Each <see cref="Subscribe"/> call is an independent subscription; subscribing the same handler
    /// twice results in two calls. Keep the returned <see cref="GrowSubscription"/> and dispose it to
    /// unsubscribe. Subscriptions made during a dispatch run after the current pass; subscriptions
    /// removed during a dispatch still receive that pass.
    /// </para>
    /// <para>
    /// The event is owned by the thread that created it. All operations, including
    /// <see cref="Count"/>, must run on that thread; another thread fails fast with
    /// <see cref="InvalidOperationException"/>. Cross-thread delivery belongs to the scheduling
    /// mechanism, not to this primitive.
    /// </para>
    /// </remarks>
    public sealed class GrowEvent<T> {
        private readonly EventBuffer<Action<T>> _buffer;

        internal GrowEvent(EventBuffer<Action<T>> buffer) {
            _buffer = buffer;
        }

        /// <summary>Number of applied subscriptions.</summary>
        public int Count => _buffer.Count;

        /// <summary>Adds <paramref name="handler"/> and returns its subscription handle.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
        /// <exception cref="ObjectDisposedException">The publisher has been disposed.</exception>
        public GrowSubscription Subscribe(Action<T> handler) {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return new GrowSubscription(_buffer, _buffer.Subscribe(handler));
        }

        /// <summary>
        /// Adds a handler that is consumed before its first call. A nested publish of the same event
        /// cannot invoke it again; a disposed token removes it before it ever runs.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
        /// <exception cref="ObjectDisposedException">The publisher has been disposed.</exception>
        public GrowSubscription SubscribeOnce(Action<T> handler) {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return new GrowSubscription(_buffer, _buffer.Subscribe(handler, true));
        }

        /// <summary>
        /// Owner surface of an event that carries one payload value.
        /// </summary>
        /// <remarks>
        /// <see cref="Publish"/> is synchronous: when it returns, every listener registered for the
        /// pass has run. A listener that publishes the same event again re-enters the pass; a nesting
        /// cap turns runaway recursion into a readable exception instead of a process-ending stack
        /// overflow.
        /// <para>
        /// Listener mutations made during a pass are applied when the outermost pass ends. Clearing
        /// removes all subscriptions but keeps the publisher usable; disposing makes the publisher
        /// terminal. When no fault sink is supplied, the first listener exception is rethrown by the
        /// outermost publish after every listener has run. A nested publish never consumes the fault;
        /// it surfaces at the outermost publish.
        /// </para>
        /// </remarks>
        public sealed class Publisher : IDisposable {
            private const int MaxDepth = 64;

            private readonly EventBuffer<Action<T>> _buffer;

            /// <summary>Creates a publisher and its subscription surface.</summary>
            /// <param name="onListenerFault">
            /// Optional sink for listener exceptions. When omitted, the first exception is rethrown
            /// after every listener has run. A sink must not throw; a sink failure never replaces the
            /// listener exception.
            /// </param>
            public Publisher(Action<Exception> onListenerFault = null) {
                _buffer = new EventBuffer<Action<T>>(onListenerFault);
                Event = new GrowEvent<T>(_buffer);
            }

            /// <summary>The subscription surface to hand to listeners.</summary>
            public GrowEvent<T> Event { get; }

            /// <summary>True while a pass is running.</summary>
            public bool IsDispatching => _buffer.IsDispatching;

            /// <summary>True after <see cref="Dispose"/>; publishing becomes a no-op.</summary>
            public bool IsDisposed => _buffer.IsDisposed;

            /// <summary>Publishes synchronously, in subscription order.</summary>
            /// <param name="payload">Value passed to each listener.</param>
            /// <exception cref="InvalidOperationException">Called from a thread other than the owner thread, or the nesting cap was reached.</exception>
            public void Publish(in T payload) {
                if (_buffer.IsDisposed) return;

                _buffer.AssertThread(nameof(Publish));
                PublishCore(payload);
            }

            /// <summary>Removes every subscription but keeps the publisher usable.</summary>
            public void Clear() {
                if (_buffer.IsDisposed) return;

                _buffer.AssertThread(nameof(Clear));
                _buffer.Clear();
            }

            /// <summary>Makes the publisher terminal; safe to call more than once.</summary>
            public void Dispose() {
                _buffer.AssertThread(nameof(Dispose));
                _buffer.Dispose();
            }

            private void PublishCore(in T payload) {
                if (_buffer.Depth >= MaxDepth)
                    throw new InvalidOperationException(
                        $"GrowEvent.Publish exceeded the nesting cap ({MaxDepth}); " +
                        "a listener is probably publishing its own event without a stopping condition.");

                var outermost = _buffer.Depth == 0;
                ExceptionDispatchInfo fault = null;
                _buffer.EnterDispatch();
                try {
                    Dispatch(payload);
                    if (outermost) fault = _buffer.ConsumeFault();
                } finally {
                    _buffer.ExitDispatch();
                    if (outermost && fault == null) _buffer.ClearFault();
                }

                fault?.Throw();
            }

            private void Dispatch(in T payload) {
                var slots = _buffer.Slots;
                var count = _buffer.ActiveCount;

                for (var i = 0; i < count; i++) {
                    var slot = slots[i];
                    var handler = slot.Handler;
                    if (handler == null || slot.Consumed) continue;

                    if (slot.Once) _buffer.ConsumeOnce(i);

                    try {
                        handler(payload);
                    } catch (Exception exception) when (!EventBuffer<Action<T>>.IsFatal(exception)) {
                        _buffer.Report(exception);
                    }
                }
            }
        }
    }
}
