// SPDX-License-Identifier: MIT

using System;
using System.Collections.Concurrent;
using Grow.Core.Events;
using Grow.Core.Log;
using Grow.Kernel.Entry;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Scheduling.Impl;
using UnityEngine;

namespace Grow.Kernel.Scheduling {
    /// <summary>
    /// The kernel's single time mechanism: deferred actions, delays and repeats, driven by the loop.
    /// </summary>
    /// <remarks>
    /// The scheduler owns no tick of its own. <see cref="GrowApp"/> attaches it to the bound lifecycle
    /// manager, and from then on it is driven by two frame phases:
    /// <c>PreUpdate</c> drains <see cref="Post"/> and <c>Update</c> advances the timers. It never
    /// starts a thread, creates a <see cref="GameObject"/> or reads a clock outside that update, so a
    /// second frame source cannot appear by accident.
    /// <para>
    /// Everything except <see cref="Post"/> runs on the main thread. A timer callback that throws is
    /// logged and does not stop the remaining callbacks of the same tick. Scheduling and cancelling
    /// from inside a callback is allowed; the new entry runs at the earliest on the next tick.
    /// </para>
    /// <para>
    /// The scheduler does not own grouping or lifetime binding: attach a timer to a scope by
    /// cancelling its handle in that scope's teardown. Coroutines remain the way to express a
    /// multi-frame script; this type only answers "do this at that point in time".
    /// </para>
    /// </remarks>
    public static class GrowScheduler {
        private const string LogCategory = "Scheduler";

        private static readonly ConcurrentQueue<Action> Posted = new();
        private static TimerCore _core = new();
        private static GrowSubscription _preUpdateSubscription;
        private static GrowSubscription _updateSubscription;

        /// <summary>Runs <paramref name="action"/> on the next <c>PreUpdate</c>.</summary>
        /// <remarks>Thread-safe. Actions keep the order of their <see cref="Post"/> calls and a
        /// throwing action is logged without stopping the rest of the batch.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <c>null</c>.</exception>
        public static void Post(Action action) {
            if (action == null) {
                throw new ArgumentNullException(nameof(action));
            }

            Posted.Enqueue(action);
        }

        /// <summary>Runs <paramref name="action"/> on the next tick.</summary>
        public static TimerHandle NextFrame(Action action) {
            return _core.ScheduleFrames(action, 1, false);
        }

        /// <summary>Runs <paramref name="action"/> after <paramref name="frames"/> ticks.</summary>
        /// <remarks>
        /// The current tick does not count, so 0 and 1 both mean the next tick; negative values are
        /// clamped to 0.
        /// </remarks>
        public static TimerHandle DelayFrames(int frames, Action action) {
            return _core.ScheduleFrames(action, frames, false);
        }

        /// <summary>Runs <paramref name="action"/> after <paramref name="seconds"/> of the chosen clock.</summary>
        /// <remarks>A non-positive delay fires on the next tick.</remarks>
        public static TimerHandle Delay(
            float seconds, Action action, TimeKind kind = TimeKind.Scaled) {
            return _core.ScheduleTime(action, seconds, kind, false);
        }

        /// <summary>Runs <paramref name="action"/> on every <paramref name="interval"/> and returns its handle.</summary>
        /// <remarks>
        /// A non-positive interval fires once per tick. Repeats advance from their deadline instead of
        /// from the current frame, so the phase does not drift; a tick never runs the same repeat
        /// twice.
        /// </remarks>
        public static TimerHandle Repeat(
            float interval, Action action, TimeKind kind = TimeKind.Scaled) {
            return _core.ScheduleTime(action, interval, kind, true);
        }

        /// <summary>Runs <paramref name="action"/> every <paramref name="frames"/> ticks.</summary>
        /// <remarks>Values below 1 are clamped to 1, so a repeat fires at most once per tick.</remarks>
        public static TimerHandle RepeatFrames(int frames, Action action) {
            return _core.ScheduleFrames(action, frames, true);
        }

        /// <summary>
        /// Cancels a timer; false when it already fired or the handle is stale. Safe to repeat.
        /// </summary>
        public static bool Cancel(TimerHandle handle) {
            return _core.Cancel(handle);
        }

        /// <summary>Live timers that have not fired yet. Cancelled timers are already excluded.</summary>
        public static int PendingCount => _core.PendingCount;

        /// <summary>Ticks driven since the session started.</summary>
        public static long FrameCount => _core.FrameCount;

        /// <summary>Scaled seconds accumulated since the session started.</summary>
        /// <remarks>This is the scheduler's own sum of frame deltas, not <c>Time.time</c>.</remarks>
        public static float Time => (float)_core.ScaledTime;

        /// <summary>Unscaled seconds accumulated since the session started.</summary>
        public static float UnscaledTime => (float)_core.UnscaledTime;

        /// <summary>Binds the scheduler to the loop host and advances it with every frame.</summary>
        internal static void Attach(ILifecycleManager manager) {
            Detach();
            if (manager == null) {
                return;
            }

            var preUpdate = manager.PreUpdate;
            var update = manager.Update;
            if (preUpdate == null || update == null) {
                GrowLog.Warning(
                    "[Scheduler] The loop host is unavailable; timers and posted actions will not " +
                    "advance until it is bound.",
                    LogCategory);
                return;
            }

            _preUpdateSubscription = preUpdate.Subscribe(FlushPosted);
            _updateSubscription = update.Subscribe(OnUpdate);
        }

        /// <summary>Drops the frame subscriptions; queued timers and posted actions are kept.</summary>
        internal static void Detach() {
            _preUpdateSubscription.Dispose();
            _preUpdateSubscription = default;
            _updateSubscription.Dispose();
            _updateSubscription = default;
        }

        /// <summary>Drives one tick with explicit deltas; the loop adapter and tests use this.</summary>
        internal static void Tick(float scaledDelta, float unscaledDelta) {
            _core.Tick(scaledDelta, unscaledDelta);
        }

        /// <summary>Runs every posted action in order, isolating each failure.</summary>
        internal static void FlushPosted() {
            while (Posted.TryDequeue(out var action)) {
                try {
                    action();
                } catch (Exception exception) {
                    GrowLog.Error($"[Scheduler] A posted action threw: {exception}", LogCategory);
                }
            }
        }

        /// <summary>Drops the frame subscriptions, every timer and every posted action.</summary>
        internal static void ResetStaticState() {
            Detach();
            _core = new TimerCore();
            while (Posted.TryDequeue(out _)) {
            }
        }

        private static void OnUpdate() {
            _core.Tick(UnityEngine.Time.deltaTime, UnityEngine.Time.unscaledDeltaTime);
        }
    }
}
