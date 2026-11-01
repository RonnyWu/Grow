// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Collections;
using Grow.Core.Log;

namespace Grow.Kernel.Scheduling.Impl {
    /// <summary>Owner-thread timer store behind <see cref="GrowScheduler"/>.</summary>
    /// <remarks>
    /// Frame delays and time delays use separate queues: a frame delay is due at a tick count, a
    /// time delay at an accumulated clock reading, and the two scales cannot be ordered in one
    /// priority queue. Scaled and unscaled time also get a queue each, because those clocks advance
    /// at different rates.
    /// <para>
    /// Each queue is a <see cref="BinaryHeap{T}"/> of <see cref="SlotHandle"/> ordered by
    /// (deadline, insertion sequence); the node store is a <see cref="SlotMap{T}"/>. Cancellation is
    /// a tombstone flag, so <see cref="Cancel"/> is O(1) and the entry is dropped when it surfaces.
    /// The generation in the handle makes a stale cancel a no-op.
    /// </para>
    /// <para>
    /// Scheduling and cancelling from inside a callback is supported: new entries are collected in
    /// <see cref="_deferred"/> and merged after the current tick, so a zero-length repeat cannot
    /// starve the frame. Not thread-safe: only <see cref="GrowScheduler.Post"/> accepts another
    /// thread.
    /// </para>
    /// </remarks>
    internal sealed class TimerCore {
        private const string LogCategory = "Scheduler";

        private readonly SlotMap<TimerNode> _timers = new();
        private readonly BinaryHeap<SlotHandle> _frameQueue;
        private readonly BinaryHeap<SlotHandle> _scaledQueue;
        private readonly BinaryHeap<SlotHandle> _unscaledQueue;
        private readonly List<SlotHandle> _deferred = new();

        private long _sequence;
        private int _liveCount;
        private bool _ticking;

        internal TimerCore() {
            _frameQueue = new BinaryHeap<SlotHandle>(new FrameComparer(_timers));
            _scaledQueue = new BinaryHeap<SlotHandle>(new TimeComparer(_timers));
            _unscaledQueue = new BinaryHeap<SlotHandle>(new TimeComparer(_timers));
        }

        /// <summary>Number of ticks driven since the session started.</summary>
        internal long FrameCount { get; private set; }

        /// <summary>Seconds accumulated from scaled frame deltas.</summary>
        internal double ScaledTime { get; private set; }

        /// <summary>Seconds accumulated from unscaled frame deltas.</summary>
        internal double UnscaledTime { get; private set; }

        /// <summary>Live timers that have not fired yet; cancelled entries are excluded.</summary>
        internal int PendingCount => _liveCount;

        internal TimerHandle ScheduleFrames(Action action, int frames, bool repeating) {
            if (action == null) {
                throw new ArgumentNullException(nameof(action));
            }

            var delay = repeating ? Math.Max(frames, 1) : Math.Max(frames, 0);
            var node = new TimerNode {
                Action = action,
                IsFrame = true,
                Repeating = repeating,
                IntervalFrames = delay,
                DueFrame = FrameCount + delay
            };
            return Store(node);
        }

        internal TimerHandle ScheduleTime(
            Action action, float seconds, TimeKind kind, bool repeating) {
            if (action == null) {
                throw new ArgumentNullException(nameof(action));
            }

            var delay = Math.Max(seconds, 0f);
            var now = kind == TimeKind.Unscaled ? UnscaledTime : ScaledTime;
            var node = new TimerNode {
                Action = action,
                Kind = kind,
                Repeating = repeating,
                Interval = delay,
                DueTime = now + delay
            };
            return Store(node);
        }

        internal bool Cancel(TimerHandle handle) {
            if (!handle.IsValid || !_timers.TryGet(handle.Slot, out var node) || node.Cancelled) {
                return false;
            }

            // The node stays in its queue until it surfaces; removing it here would break the
            // comparer, which resolves every queued handle through this map.
            node.Cancelled = true;
            _liveCount--;
            return true;
        }

        internal void Tick(float scaledDelta, float unscaledDelta) {
            if (_ticking) {
                throw new InvalidOperationException(
                    "GrowScheduler.Tick is already running; a timer callback must not tick it again.");
            }

            _ticking = true;
            try {
                FrameCount++;
                ScaledTime += scaledDelta;
                UnscaledTime += unscaledDelta;
                Drain(_frameQueue, true);
                Drain(_scaledQueue, false);
                Drain(_unscaledQueue, false);
            } finally {
                _ticking = false;
                MergeDeferred();
            }
        }

        private TimerHandle Store(TimerNode node) {
            node.Sequence = ++_sequence;
            var handle = _timers.Add(node);
            _liveCount++;
            Enqueue(handle, node);
            return new TimerHandle(handle);
        }

        private void Enqueue(SlotHandle handle, TimerNode node) {
            if (_ticking) {
                _deferred.Add(handle);
                return;
            }

            QueueFor(node).Enqueue(handle);
        }

        private void MergeDeferred() {
            for (var i = 0; i < _deferred.Count; i++) {
                var handle = _deferred[i];
                if (_timers.TryGet(handle, out var node)) {
                    QueueFor(node).Enqueue(handle);
                }
            }

            _deferred.Clear();
        }

        private BinaryHeap<SlotHandle> QueueFor(TimerNode node) {
            if (node.IsFrame) {
                return _frameQueue;
            }

            return node.Kind == TimeKind.Unscaled ? _unscaledQueue : _scaledQueue;
        }

        private void Drain(BinaryHeap<SlotHandle> queue, bool byFrame) {
            while (queue.TryPeek(out var handle) && _timers.TryGet(handle, out var node) &&
                   IsDue(node, byFrame)) {
                queue.Dequeue();
                Fire(handle, node);
            }
        }

        private bool IsDue(TimerNode node, bool byFrame) {
            if (byFrame) {
                return node.DueFrame <= FrameCount;
            }

            return node.Kind == TimeKind.Unscaled
                ? node.DueTime <= UnscaledTime
                : node.DueTime <= ScaledTime;
        }

        private void Fire(SlotHandle handle, TimerNode node) {
            if (!node.Cancelled) {
                try {
                    node.Action();
                } catch (Exception exception) {
                    GrowLog.Error($"[Scheduler] A timer callback threw: {exception}", LogCategory);
                }
            }

            if (node.Cancelled) {
                _timers.Remove(handle);
                return;
            }

            if (!node.Repeating) {
                _timers.Remove(handle);
                _liveCount--;
                return;
            }

            // Repeats advance from their deadline, not from "now", so a slow frame does not drift the
            // phase; the deferred merge keeps the next pass out of this tick.
            node.Sequence = ++_sequence;
            if (node.IsFrame) {
                node.DueFrame += node.IntervalFrames;
            } else {
                node.DueTime += node.Interval;
            }

            Enqueue(handle, node);
        }

        private abstract class DueComparer : IComparer<SlotHandle> {
            private readonly SlotMap<TimerNode> _timers;

            protected DueComparer(SlotMap<TimerNode> timers) {
                _timers = timers;
            }

            public int Compare(SlotHandle left, SlotHandle right) {
                var hasLeft = _timers.TryGet(left, out var first);
                var hasRight = _timers.TryGet(right, out var second);
                if (!hasLeft || !hasRight) {
                    // A node leaves the map only after it leaves its queue, so this is unreachable
                    // while the invariant holds. Order a stale entry last so it cannot block a live
                    // one if the invariant is ever broken.
                    if (!hasLeft && !hasRight) {
                        return 0;
                    }

                    return hasLeft ? -1 : 1;
                }

                var byDeadline = CompareDeadline(first, second);
                return byDeadline != 0 ? byDeadline : first.Sequence.CompareTo(second.Sequence);
            }

            protected abstract int CompareDeadline(TimerNode left, TimerNode right);
        }

        private sealed class FrameComparer : DueComparer {
            internal FrameComparer(SlotMap<TimerNode> timers) : base(timers) {
            }

            protected override int CompareDeadline(TimerNode left, TimerNode right) {
                return left.DueFrame.CompareTo(right.DueFrame);
            }
        }

        private sealed class TimeComparer : DueComparer {
            internal TimeComparer(SlotMap<TimerNode> timers) : base(timers) {
            }

            protected override int CompareDeadline(TimerNode left, TimerNode right) {
                return left.DueTime.CompareTo(right.DueTime);
            }
        }
    }
}
