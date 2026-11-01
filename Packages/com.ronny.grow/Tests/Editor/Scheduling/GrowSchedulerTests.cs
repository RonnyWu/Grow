// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Threading;
using Grow.Core.Log;
using Grow.Kernel.Entry;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Scheduling;
using Grow.Tests.Editor.Lifecycle;
using NUnit.Framework;

namespace Grow.Tests.Editor.Scheduling {
    public class GrowSchedulerTests {
        private CapturingSink _sink;

        [SetUp]
        public void SetUp() {
            GrowScheduler.ResetStaticState();
            MonoManager.ResetStaticState();
            _sink = new CapturingSink();
            GrowLog.SetSink(_sink);
            GrowLog.MinimumLevel = LogLevel.Trace;
        }

        [TearDown]
        public void TearDown() {
            GrowScheduler.ResetStaticState();
            MonoManager.ResetStaticState();
            GrowLog.Reset();
        }

        [Test]
        public void NextFrame_FiresOnceOnTheNextTick() {
            var count = 0;
            GrowScheduler.NextFrame(() => count++);

            GrowScheduler.Tick(0.016f, 0.016f);
            GrowScheduler.Tick(0.016f, 0.016f);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Delay_WaitsForTheScaledDeadline() {
            var fired = false;
            GrowScheduler.Delay(1f, () => fired = true);

            GrowScheduler.Tick(0.5f, 0.5f);
            Assert.IsFalse(fired);

            GrowScheduler.Tick(0.5f, 0.5f);
            Assert.IsTrue(fired);
        }

        [Test]
        public void Delay_UsesTheUnscaledClockWhenAsked() {
            var fired = false;
            GrowScheduler.Delay(1f, () => fired = true, TimeKind.Unscaled);

            GrowScheduler.Tick(5f, 0.5f);
            Assert.IsFalse(fired, "the scaled delta must not move an unscaled timer");

            GrowScheduler.Tick(5f, 0.5f);
            Assert.IsTrue(fired);
        }

        [Test]
        public void DelayFrames_CountsTicksAfterTheCurrentOne() {
            var fired = false;
            GrowScheduler.DelayFrames(2, () => fired = true);

            GrowScheduler.Tick(0f, 0f);
            Assert.IsFalse(fired);

            GrowScheduler.Tick(0f, 0f);
            Assert.IsTrue(fired);
        }

        [Test]
        public void DelayFrames_ZeroAndNegativeFireOnTheNextTick() {
            var zero = 0;
            var negative = 0;
            GrowScheduler.DelayFrames(0, () => zero++);
            GrowScheduler.DelayFrames(-3, () => negative++);

            Assert.AreEqual(0, zero);
            GrowScheduler.Tick(0f, 0f);

            Assert.AreEqual(1, zero);
            Assert.AreEqual(1, negative);
        }

        [Test]
        public void Repeat_FiresOncePerInterval() {
            var count = 0;
            GrowScheduler.Repeat(0.5f, () => count++);

            GrowScheduler.Tick(0.5f, 0.5f);
            GrowScheduler.Tick(0.4f, 0.4f);
            Assert.AreEqual(1, count);

            GrowScheduler.Tick(0.1f, 0.1f);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void Repeat_WithZeroIntervalFiresAtMostOncePerTick() {
            var count = 0;
            GrowScheduler.Repeat(0f, () => count++);

            GrowScheduler.Tick(1f, 1f);

            Assert.AreEqual(1, count, "a repeat must never run twice in the same tick");
        }

        [Test]
        public void RepeatFrames_FiresEveryRequestedTicks() {
            var count = 0;
            GrowScheduler.RepeatFrames(2, () => count++);

            GrowScheduler.Tick(0f, 0f);
            Assert.AreEqual(0, count);

            GrowScheduler.Tick(0f, 0f);
            GrowScheduler.Tick(0f, 0f);
            Assert.AreEqual(1, count);

            GrowScheduler.Tick(0f, 0f);
            GrowScheduler.Tick(0f, 0f);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void Cancel_PreventsAPendingTimer() {
            var fired = false;
            var handle = GrowScheduler.Delay(1f, () => fired = true);

            Assert.IsTrue(handle.IsValid);
            Assert.IsTrue(GrowScheduler.Cancel(handle));

            GrowScheduler.Tick(2f, 2f);

            Assert.IsFalse(fired);
        }

        [Test]
        public void Cancel_StopsARepeat() {
            var count = 0;
            var handle = GrowScheduler.Repeat(1f, () => count++);

            GrowScheduler.Tick(1f, 1f);
            Assert.IsTrue(GrowScheduler.Cancel(handle));

            GrowScheduler.Tick(1f, 1f);
            GrowScheduler.Tick(1f, 1f);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Cancel_IsIdempotentAndRejectsStaleHandles() {
            var handle = GrowScheduler.Delay(1f, () => { });

            Assert.IsTrue(GrowScheduler.Cancel(handle));
            Assert.IsFalse(GrowScheduler.Cancel(handle), "the second cancel must be a no-op");
            Assert.IsFalse(GrowScheduler.Cancel(TimerHandle.None));
            Assert.IsFalse(TimerHandle.None.IsValid);

            GrowScheduler.Tick(2f, 2f);
            Assert.IsFalse(GrowScheduler.Cancel(handle), "a fired timer has no handle left to cancel");
        }

        [Test]
        public void Cancel_FromACallbackStopsAnotherTimerInTheSameTick() {
            var second = false;
            TimerHandle secondHandle = TimerHandle.None;
            GrowScheduler.Delay(0f, () => GrowScheduler.Cancel(secondHandle));
            secondHandle = GrowScheduler.Delay(0f, () => second = true);

            GrowScheduler.Tick(0f, 0f);

            Assert.IsFalse(second);
        }

        [Test]
        public void Delay_FromACallbackWaitsForTheNextTick() {
            var order = new List<string>();
            GrowScheduler.Delay(0f, () => {
                order.Add("first");
                GrowScheduler.Delay(0f, () => order.Add("second"));
            });

            GrowScheduler.Tick(0f, 0f);
            CollectionAssert.AreEqual(new[] { "first" }, order);

            GrowScheduler.Tick(0f, 0f);
            CollectionAssert.AreEqual(new[] { "first", "second" }, order);
        }

        [Test]
        public void SameDeadline_FiresInInsertionOrder() {
            var order = new List<string>();
            GrowScheduler.Delay(0f, () => order.Add("a"));
            GrowScheduler.Delay(0f, () => order.Add("b"));
            GrowScheduler.Delay(0f, () => order.Add("c"));

            GrowScheduler.Tick(0f, 0f);

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, order);
        }

        [Test]
        public void ThrowingCallback_IsLoggedAndDoesNotStopTheRest() {
            var second = false;
            GrowScheduler.Delay(0f, () => throw new InvalidOperationException("boom"));
            GrowScheduler.Delay(0f, () => second = true);

            Assert.DoesNotThrow(() => GrowScheduler.Tick(0f, 0f));

            Assert.IsTrue(second, "a faulting callback must not stop the batch");
            Assert.IsTrue(
                _sink.Entries.Exists(entry =>
                    entry.Level == LogLevel.Error && entry.Category == "Scheduler"),
                "the fault must be reported through GrowLog");
        }

        [Test]
        public void Tick_FromACallbackIsRejected() {
            GrowScheduler.DelayFrames(0, () => GrowScheduler.Tick(0f, 0f));

            Assert.DoesNotThrow(() => GrowScheduler.Tick(0f, 0f));

            Assert.IsTrue(
                _sink.Entries.Exists(entry => entry.Message.Contains("already running")),
                "a nested tick must be reported instead of corrupting the store");
        }

        [Test]
        public void Tick_AdvancesFrameCountAndBothClocks() {
            GrowScheduler.Tick(0.25f, 0.1f);

            Assert.AreEqual(1L, GrowScheduler.FrameCount);
            Assert.AreEqual(0.25f, GrowScheduler.Time, 0.0001f);
            Assert.AreEqual(0.1f, GrowScheduler.UnscaledTime, 0.0001f);

            GrowScheduler.Tick(0.75f, 0.2f);
            Assert.AreEqual(2L, GrowScheduler.FrameCount);
            Assert.AreEqual(1f, GrowScheduler.Time, 0.0001f);
            Assert.AreEqual(0.3f, GrowScheduler.UnscaledTime, 0.0001f);
        }

        [Test]
        public void PendingCount_ExcludesCancelledTimers() {
            var first = GrowScheduler.Delay(10f, () => { });
            GrowScheduler.Delay(10f, () => { });
            Assert.AreEqual(2, GrowScheduler.PendingCount);

            GrowScheduler.Cancel(first);
            Assert.AreEqual(1, GrowScheduler.PendingCount);

            GrowScheduler.Tick(0f, 0f);
            Assert.AreEqual(1, GrowScheduler.PendingCount, "a live timer stays pending until it fires");
        }

        [Test]
        public void Post_RunsInOrderOnFlush() {
            var order = new List<string>();
            GrowScheduler.Post(() => order.Add("a"));
            GrowScheduler.Post(() => order.Add("b"));

            CollectionAssert.IsEmpty(order);
            GrowScheduler.FlushPosted();

            CollectionAssert.AreEqual(new[] { "a", "b" }, order);
        }

        [Test]
        public void Post_FromAWorkerThreadIsDrained() {
            var order = new List<string>();
            var worker = new Thread(() => GrowScheduler.Post(() => order.Add("worker")));
            worker.Start();
            worker.Join();

            CollectionAssert.IsEmpty(order);
            GrowScheduler.FlushPosted();

            CollectionAssert.AreEqual(new[] { "worker" }, order);
        }

        [Test]
        public void Post_IsolatesAFaultingAction() {
            var second = false;
            GrowScheduler.Post(() => throw new InvalidOperationException("boom"));
            GrowScheduler.Post(() => second = true);

            Assert.DoesNotThrow(() => GrowScheduler.FlushPosted());

            Assert.IsTrue(second);
            Assert.IsTrue(
                _sink.Entries.Exists(entry => entry.Message.Contains("posted action")),
                "the fault must be reported through GrowLog");
        }

        [Test]
        public void Post_DoesNotCountAsATimer() {
            GrowScheduler.Post(() => { });

            Assert.AreEqual(0, GrowScheduler.PendingCount);
        }

        [Test]
        public void Post_RejectsNull() {
            Assert.Throws<ArgumentNullException>(() => GrowScheduler.Post(null));
        }

        [Test]
        public void ResetStaticState_ClearsTimersPostedActionsAndClocks() {
            var postedRan = false;
            GrowScheduler.DelayFrames(0, () => { });
            GrowScheduler.Tick(1f, 1f);
            GrowScheduler.Post(() => postedRan = true);

            GrowScheduler.ResetStaticState();

            Assert.AreEqual(0, GrowScheduler.PendingCount);
            Assert.AreEqual(0L, GrowScheduler.FrameCount);
            Assert.AreEqual(0f, GrowScheduler.Time);
            Assert.AreEqual(0f, GrowScheduler.UnscaledTime);

            GrowScheduler.FlushPosted();
            Assert.IsFalse(postedRan, "a posted action queued before the reset must be dropped");
        }

        [Test]
        public void SessionReset_ClearsTheScheduler() {
            GrowScheduler.DelayFrames(0, () => { });
            GrowScheduler.Post(() => { });

            GrowSession.ResetStaticState();

            Assert.AreEqual(0, GrowScheduler.PendingCount);
            Assert.AreEqual(0L, GrowScheduler.FrameCount);
        }

        [Test]
        public void Attach_DrainsPostOnPreUpdateAndTicksOnUpdate() {
            var executor = new FakeMonoExecutor();
            MonoManager.Instance.Bind(executor);
            GrowScheduler.Attach(MonoManager.Instance);

            var posted = false;
            var fired = false;
            try {
                GrowScheduler.Post(() => posted = true);
                executor.PublishPreUpdate();
                Assert.IsTrue(posted);

                GrowScheduler.DelayFrames(0, () => fired = true);
                executor.PublishUpdate();
                Assert.IsTrue(fired);
            } finally {
                GrowScheduler.Detach();
                executor.Dispose();
            }
        }

        [Test]
        public void Detach_StopsAdvancingButKeepsQueuedTimers() {
            var executor = new FakeMonoExecutor();
            MonoManager.Instance.Bind(executor);
            GrowScheduler.Attach(MonoManager.Instance);

            var fired = false;
            try {
                GrowScheduler.DelayFrames(0, () => fired = true);
                GrowScheduler.Detach();

                executor.PublishUpdate();
                Assert.IsFalse(fired, "a detached scheduler must not advance");
                Assert.AreEqual(1, GrowScheduler.PendingCount);

                GrowScheduler.Tick(0f, 0f);
                Assert.IsTrue(fired, "the timer survives detach and fires when driven again");
            } finally {
                executor.Dispose();
            }
        }

        private sealed class CapturingSink : ILogSink {
            internal List<LogEntry> Entries { get; } = new();

            public void Write(in LogEntry entry) {
                Entries.Add(entry);
            }
        }
    }
}
