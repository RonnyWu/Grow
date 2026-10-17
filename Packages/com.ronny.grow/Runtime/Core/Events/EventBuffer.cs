// SPDX-License-Identifier: MIT

using System;
using System.Runtime.ExceptionServices;

namespace Grow.Core.Events {
    /// <summary>
    /// Subscription storage shared by <see cref="GrowEvent"/> and <see cref="GrowEvent{T}"/>.
    /// </summary>
    /// <remarks>
    /// One lazily allocated array of slots holds the subscriptions in registration order. There is no
    /// separate snapshot, dictionary, tombstone list or version number: changes made while a dispatch
    /// is in progress are queued and applied when the outermost pass ends, so the current pass always
    /// iterates a stable array.
    /// <para>
    /// The buffer is single-threaded by contract. It captures the thread that created it and every
    /// operation — including reading <see cref="Count"/> and <see cref="IsActive"/> — fails fast when
    /// called from another thread. Those reads are lock-free on the owner thread, but they must not
    /// race a mutation from another thread.
    /// </para>
    /// <para>
    /// Exceptions thrown by listeners are reported to the optional fault sink. When no sink is
    /// configured the first exception is captured and rethrown by the publisher after every listener
    /// has run; a listener can therefore never silence the listeners after it.
    /// </para>
    /// </remarks>
    internal sealed class EventBuffer<TDelegate> : ISubscriptionOwner where TDelegate : Delegate {
        internal struct Slot {
            public TDelegate Handler;
            public int Id;
            public bool Once;
            public bool Consumed;
        }

        private enum PendingKind : byte {
            Add,
            Remove,
            Clear,
            Dispose
        }

        private struct PendingOp {
            public TDelegate Handler;
            public int Id;
            public PendingKind Kind;
            public bool Once;

            public PendingOp(PendingKind kind, TDelegate handler, int id, bool once = false) {
                Kind = kind;
                Handler = handler;
                Id = id;
                Once = once;
            }
        }

        private readonly int _ownerThreadId;
        private readonly Action<Exception> _faultSink;
        private Slot[] _slots;
        private int _count;
        private PendingOp[] _pending;
        private int _pendingCount;
        private int _depth;
        private int _nextId = 1;
        private ExceptionDispatchInfo _fault;
        private bool _disposed;

        public EventBuffer(Action<Exception> faultSink) {
            _ownerThreadId = Environment.CurrentManagedThreadId;
            _faultSink = faultSink;
        }

        /// <summary>Number of applied subscriptions; changes queued during a dispatch are excluded.</summary>
        public int Count {
            get {
                AssertThread(nameof(Count));
                return _disposed ? 0 : _count;
            }
        }

        /// <summary>True after the publisher has been disposed.</summary>
        public bool IsDisposed => _disposed;

        /// <summary>True while a dispatch pass is running.</summary>
        public bool IsDispatching => _depth > 0;

        /// <summary>Current nesting depth; the publisher uses it for the recursion cap.</summary>
        public int Depth => _depth;

        /// <summary>Applied slots, stable for the duration of a dispatch pass.</summary>
        public Slot[] Slots => _slots;

        /// <summary>Number of applied slots to iterate in a dispatch pass.</summary>
        public int ActiveCount => _count;

        /// <summary>Registers a handler and returns its subscription id.</summary>
        public int Subscribe(TDelegate handler) {
            return Subscribe(handler, false);
        }

        /// <summary>Registers a handler; when <paramref name="once"/> is true it is consumed before its first call.</summary>
        public int Subscribe(TDelegate handler, bool once) {
            AssertThread(nameof(Subscribe));
            if (_disposed) throw new ObjectDisposedException(nameof(GrowEvent));

            var id = _nextId++;
            if (_depth > 0) {
                Pend(new PendingOp(PendingKind.Add, handler, id, once));
                return id;
            }

            AddSlot(handler, id, once);
            return id;
        }

        /// <summary>Removes the subscription with <paramref name="id"/>; unknown ids are ignored.</summary>
        public bool Unsubscribe(int id) {
            AssertThread(nameof(Unsubscribe));
            if (_disposed) return false;

            if (_depth > 0) {
                if (!IsActive(id)) return false;
                Pend(new PendingOp(PendingKind.Remove, default, id));
                return true;
            }

            return RemoveSlot(id);
        }

        /// <summary>True when the subscription is applied, or will be after the current pass.</summary>
        public bool IsActive(int id) {
            AssertThread(nameof(IsActive));
            if (_disposed || id <= 0) return false;

            var active = false;
            for (var i = 0; i < _count; i++) {
                if (_slots[i].Id != id) continue;
                active = !_slots[i].Consumed;
                break;
            }

            for (var i = 0; i < _pendingCount; i++) {
                var op = _pending[i];
                if (op.Kind == PendingKind.Clear) active = false;
                else if (op.Id == id) active = op.Kind == PendingKind.Add;
            }

            return active;
        }

        /// <summary>Drops every subscription and every queued change.</summary>
        public void Clear() {
            AssertThread(nameof(Clear));
            if (_disposed) return;

            if (_depth > 0) {
                Pend(new PendingOp(PendingKind.Clear, default, 0));
                return;
            }

            ClearSlots();
            ClearPending();
        }

        /// <summary>Makes the event terminal: clears subscriptions and rejects future subscriptions.</summary>
        public void Dispose() {
            AssertThread(nameof(Dispose));
            if (_disposed) return;

            _disposed = true;
            if (_depth > 0) {
                Pend(new PendingOp(PendingKind.Dispose, default, 0));
                return;
            }

            DisposeNow();
        }

        /// <summary>Enters a dispatch pass. Every call must be paired with <see cref="ExitDispatch"/>.</summary>
        public void EnterDispatch() {
            _depth++;
        }

        /// <summary>Ends a dispatch pass and applies queued changes when the outermost pass ends.</summary>
        public bool ExitDispatch() {
            _depth--;
            if (_depth != 0) return false;
            return ApplyPending();
        }

        /// <summary>Marks a one-shot slot consumed before it is invoked and schedules its removal.</summary>
        public void ConsumeOnce(int index) {
            if (index < 0 || index >= _count) return;
            if (!_slots[index].Once || _slots[index].Consumed) return;

            _slots[index].Consumed = true;
            Pend(new PendingOp(PendingKind.Remove, default, _slots[index].Id));
        }

        /// <summary>Fails fast when a mutating operation runs on a different thread than the creator.</summary>
        public void AssertThread(string operation) {
            var current = Environment.CurrentManagedThreadId;
            if (current == _ownerThreadId) return;

            throw new InvalidOperationException(
                $"GrowEvent.{operation} must run on the thread that created the event " +
                $"({_ownerThreadId}), but was called on thread {current}.");
        }

        /// <summary>Reports a listener failure and captures the first one for the publisher to rethrow.</summary>
        public void Report(Exception exception) {
            var sink = _faultSink;
            if (sink == null) {
                Capture(exception);
                return;
            }

            try {
                sink(exception);
            } catch (Exception sinkException) when (!IsFatal(sinkException)) {
                // A sink is responsible for containing its own failures; it must not replace the
                // listener exception or turn a sink failure into a publish failure.
            }
        }

        /// <summary>Returns the first captured fault, if any, and clears it.</summary>
        public ExceptionDispatchInfo ConsumeFault() {
            var fault = _fault;
            _fault = null;
            return fault;
        }

        /// <summary>Drops a fault captured earlier in the current dispatch.</summary>
        public void ClearFault() {
            _fault = null;
        }

        /// <summary>True for exceptions that must never be swallowed by a listener loop.</summary>
        public static bool IsFatal(Exception exception) {
            var fullName = exception.GetType().FullName;
            return fullName == "System.OutOfMemoryException"
                || fullName == "System.StackOverflowException"
                || fullName == "System.AccessViolationException"
                || fullName == "System.InvalidProgramException"
                || fullName == "System.BadImageFormatException"
                || fullName == "System.Threading.ThreadAbortException"
                || fullName == "UnityEngine.ExitGUIException";
        }

        private void Pend(PendingOp op) {
            if (_pending == null) {
                _pending = new PendingOp[4];
            } else if (_pendingCount == _pending.Length) {
                Array.Resize(ref _pending, _pending.Length * 2);
            }

            _pending[_pendingCount++] = op;
        }

        private bool ApplyPending() {
            if (_pendingCount == 0) return false;

            var cleared = false;
            for (var i = 0; i < _pendingCount; i++) {
                var op = _pending[i];
                switch (op.Kind) {
                    case PendingKind.Add:
                        AddSlot(op.Handler, op.Id, op.Once);
                        break;
                    case PendingKind.Remove:
                        RemoveSlot(op.Id);
                        break;
                    case PendingKind.Clear:
                        ClearSlots();
                        cleared = true;
                        break;
                    case PendingKind.Dispose:
                        DisposeNow();
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown pending event operation {op.Kind}.");
                }
            }

            ClearPending();
            return cleared;
        }

        private void DisposeNow() {
            ClearSlots();
            ClearPending();
            ClearFault();
        }

        private void ClearPending() {
            if (_pending != null && _pendingCount > 0)
                Array.Clear(_pending, 0, _pendingCount);
            _pendingCount = 0;
        }

        private void AddSlot(TDelegate handler, int id, bool once = false) {
            if (_slots == null) {
                _slots = new Slot[4];
            } else if (_count == _slots.Length) {
                Array.Resize(ref _slots, _slots.Length * 2);
            }

            _slots[_count] = new Slot {
                Handler = handler,
                Id = id,
                Once = once,
                Consumed = false
            };
            _count++;
        }

        private bool RemoveSlot(int id) {
            if (_slots == null) return false;

            for (var i = 0; i < _count; i++) {
                if (_slots[i].Id != id) continue;

                var move = _count - i - 1;
                if (move > 0) Array.Copy(_slots, i + 1, _slots, i, move);
                _count--;
                _slots[_count] = default;
                return true;
            }

            return false;
        }

        private void ClearSlots() {
            if (_slots != null) {
                Array.Clear(_slots, 0, _count);
                // A burst of thousands of subscriptions should not pin the array for the whole session.
                if (_slots.Length > 256) _slots = null;
            }

            _count = 0;
        }

        private void Capture(Exception exception) {
            if (_fault == null) _fault = ExceptionDispatchInfo.Capture(exception);
        }

        bool ISubscriptionOwner.IsActive(int id) => IsActive(id);

        void ISubscriptionOwner.Unsubscribe(int id) => Unsubscribe(id);
    }

    /// <summary>Non-generic view used by <see cref="GrowSubscription"/>.</summary>
    internal interface ISubscriptionOwner {
        bool IsActive(int id);
        void Unsubscribe(int id);
    }
}
