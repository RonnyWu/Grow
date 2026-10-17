// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;
using Grow.Core.Events;
using NUnit.Framework;

namespace Grow.Tests.Editor.Events {
    // Temporary probe for the post-decision evaluation. Timing numbers are reported, not asserted.
    [Explicit("Manual performance probe; run only on demand.")]
    [Category("Events.PerfProbe")]
    public class GrowEventPerformanceProbe {
        private static readonly int[] Sizes = { 1, 4, 16, 64, 256, 1024 };

        [Test]
        public void Publish_Scaling() {
            foreach (var size in Sizes) {
                var publisher = new GrowEvent.Publisher();
                var sink = new int[size];
                for (var i = 0; i < size; i++) {
                    var index = i;
                    _ = publisher.Event.Subscribe(() => sink[index]++);
                }

                var iterations = IterationsFor(size);
                for (var i = 0; i < 200; i++) publisher.Publish();

                var stopwatch = Stopwatch.StartNew();
                for (var i = 0; i < iterations; i++) publisher.Publish();
                stopwatch.Stop();

                var nsPerPublish = stopwatch.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;
                var nsPerListener = nsPerPublish / size;
                UnityEngine.Debug.Log(
                    $"[PERF] Publish n={size} ns/op={nsPerPublish:F1} ns/listener={nsPerListener:F2} iterations={iterations}");
            }

            Assert.Pass();
        }

        [Test]
        public void SubscribeAndDispose_Scaling() {
            foreach (var size in Sizes) {
                var publisher = new GrowEvent.Publisher();
                var handlers = new Action[size];
                var sink = new int[size];
                for (var i = 0; i < size; i++) {
                    var index = i;
                    handlers[i] = () => sink[index]++;
                }

                var cycles = Math.Max(2000, 20000 / size);
                for (var i = 0; i < 200; i++) {
                    var warmup = publisher.Event.Subscribe(handlers[i % size]);
                    warmup.Dispose();
                }

                var stopwatch = Stopwatch.StartNew();
                for (var i = 0; i < cycles; i++) {
                    var subscription = publisher.Event.Subscribe(handlers[i % size]);
                    subscription.Dispose();
                }
                stopwatch.Stop();

                var nsPerCycle = stopwatch.Elapsed.TotalMilliseconds * 1_000_000.0 / cycles;
                UnityEngine.Debug.Log(
                    $"[PERF] Subscribe+Dispose n={size} ns/cycle={nsPerCycle:F1} cycles={cycles}");
            }

            Assert.Pass();
        }

        [Test]
        public void DisposeFromFront_Scaling() {
            foreach (var size in Sizes) {
                var publisher = new GrowEvent.Publisher();
                var handlers = new Action[size];
                var sink = new int[size];
                var subscriptions = new GrowSubscription[size];
                for (var i = 0; i < size; i++) {
                    var index = i;
                    handlers[i] = () => sink[index]++;
                    subscriptions[i] = publisher.Event.Subscribe(handlers[i]);
                }

                var cycles = size <= 16 ? 10000 : size <= 256 ? 2000 : 500;
                var stopwatch = Stopwatch.StartNew();
                for (var i = 0; i < cycles; i++) {
                    subscriptions[0].Dispose();
                    subscriptions[0] = publisher.Event.Subscribe(handlers[0]);
                }
                stopwatch.Stop();

                var nsPerCycle = stopwatch.Elapsed.TotalMilliseconds * 1_000_000.0 / cycles;
                UnityEngine.Debug.Log(
                    $"[PERF] DisposeFront+Subscribe n={size} ns/cycle={nsPerCycle:F1} cycles={cycles}");
            }

            Assert.Pass();
        }

        [Test]
        public void EmptyPublish_ThreadAssertOverhead() {
            var publisher = new GrowEvent.Publisher();
            for (var i = 0; i < 1000; i++) publisher.Publish();

            var stopwatch = Stopwatch.StartNew();
            const int iterations = 200000;
            for (var i = 0; i < iterations; i++) publisher.Publish();
            stopwatch.Stop();

            var nsPerPublish = stopwatch.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;
            UnityEngine.Debug.Log($"[PERF] Empty Publish ns/op={nsPerPublish:F1} iterations={iterations}");
            Assert.Pass();
        }

        [Test]
        public void Allocation_PerPublishAndSubscription() {
            const int size = 16;
            var publisher = new GrowEvent.Publisher();
            var handlers = new Action[size];
            var sink = new int[size];
            for (var i = 0; i < size; i++) {
                var index = i;
                handlers[i] = () => sink[index]++;
            }

            for (var i = 0; i < size; i++) _ = publisher.Event.Subscribe(handlers[i]);
            for (var i = 0; i < 100; i++) publisher.Publish();

            var before = GC.GetAllocatedBytesForCurrentThread();
            const int publishes = 10000;
            for (var i = 0; i < publishes; i++) publisher.Publish();
            var publishBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            for (var i = 0; i < size; i++) {
                var subscription = publisher.Event.Subscribe(handlers[i]);
                subscription.Dispose();
            }

            before = GC.GetAllocatedBytesForCurrentThread();
            const int cycles = 10000;
            for (var i = 0; i < cycles; i++) {
                var subscription = publisher.Event.Subscribe(handlers[i % size]);
                subscription.Dispose();
            }
            var subscribeBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            UnityEngine.Debug.Log(
                $"[PERF] Allocation publish={publishBytes}B/{publishes} subscribe={subscribeBytes}B/{cycles}");
            Assert.Pass();
        }

        private static int IterationsFor(int size) {
            if (size <= 4) return 200000;
            if (size <= 16) return 100000;
            if (size <= 64) return 20000;
            if (size <= 256) return 5000;
            return 1000;
        }
    }
}
