// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Events;
using NUnit.Framework;

namespace Grow.Tests.Editor.Events {
    [Category("Events.Allocation")]
    public class GrowEventAllocationTests {
        [Test]
        public void EmptyEvent_Publish_DoesNotAllocate() {
            var publisher = new GrowEvent.Publisher();
            for (var i = 0; i < 100; i++) {
                publisher.Publish();
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            const int count = 10000;
            for (var i = 0; i < count; i++) {
                publisher.Publish();
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.LessOrEqual(allocated, 1024L, $"empty Publish allocated {allocated} B over {count} calls");
        }

        [Test]
        public void SteadyStatePublish_DoesNotAllocate() {
            var publisher = new GrowEvent.Publisher();
            var calls = 0;
            for (var i = 0; i < 10; i++) {
                _ = publisher.Event.Subscribe(() => calls++);
            }

            for (var i = 0; i < 100; i++) {
                publisher.Publish();
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            const int count = 10000;
            for (var i = 0; i < count; i++) {
                publisher.Publish();
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.LessOrEqual(allocated, 1024L, $"steady Publish allocated {allocated} B over {count} calls");
            Assert.AreEqual(101000, calls);
        }

        [Test]
        public void SubscribeAndDispose_DoesNotAllocateAfterWarmup() {
            var publisher = new GrowEvent.Publisher();
            var growEvent = publisher.Event;
            var handler = new Action(() => { });

            for (var i = 0; i < 100; i++) {
                var warmup = growEvent.Subscribe(handler);
                warmup.Dispose();
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            const int count = 10000;
            for (var i = 0; i < count; i++) {
                var subscription = growEvent.Subscribe(handler);
                subscription.Dispose();
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.LessOrEqual(allocated, 1024L, $"Subscribe/Dispose allocated {allocated} B over {count} cycles");
            Assert.AreEqual(0, growEvent.Count);
        }

        [Test]
        public void GenericSteadyStatePublish_DoesNotAllocate() {
            var publisher = new GrowEvent<int>.Publisher();
            var calls = 0;
            for (var i = 0; i < 10; i++) {
                _ = publisher.Event.Subscribe(_ => calls++);
            }

            for (var i = 0; i < 100; i++) {
                publisher.Publish(1);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            const int count = 10000;
            for (var i = 0; i < count; i++) {
                publisher.Publish(1);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.LessOrEqual(allocated, 1024L, $"steady generic Publish allocated {allocated} B over {count} calls");
            Assert.AreEqual(101000, calls);
        }
    }
}
