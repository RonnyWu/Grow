// SPDX-License-Identifier: MIT

using System;
using System.Runtime.CompilerServices;
using Grow.Core.Events;
using NUnit.Framework;

namespace Grow.Tests.Editor.Events {
    [Category("Events.Leak")]
    public class GrowEventLeakTests {
        private sealed class Target {
            public int Calls;

            public void Handle() {
                Calls++;
            }
        }

        [Test]
        public void DisposedHandler_IsCollectable() {
            var publisher = new GrowEvent.Publisher();
            var weak = SubscribeAndDispose(publisher.Event);

            Collect();

            Assert.IsFalse(weak.IsAlive, "a disposed handler must not stay reachable through the event");
        }

        [Test]
        public void Clear_ReleasesAllHandlers() {
            var publisher = new GrowEvent.Publisher();
            var weak = SubscribeTwo(publisher.Event);

            publisher.Clear();
            Collect();

            Assert.IsFalse(weak.IsAlive, "Clear must release every handler reference");
        }

        [Test]
        public void RemoveDuringPublish_ReleasesAfterPass() {
            var publisher = new GrowEvent.Publisher();
            var weak = RemoveDuringPublish(publisher);

            Collect();

            Assert.IsFalse(weak.IsAlive, "a handler removed during dispatch must be released after the pass");
        }

        [Test]
        public void ClearDuringPublish_ReleasesAfterPass() {
            var publisher = new GrowEvent.Publisher();
            var weak = ClearDuringPublish(publisher);

            Collect();

            Assert.IsFalse(weak.IsAlive, "Clear during dispatch must release handlers after the pass");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference SubscribeAndDispose(GrowEvent growEvent) {
            var target = new Target();
            var subscription = growEvent.Subscribe(target.Handle);
            subscription.Dispose();
            return new WeakReference(target);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference SubscribeTwo(GrowEvent growEvent) {
            var target = new Target();
            _ = growEvent.Subscribe(target.Handle);
            _ = growEvent.Subscribe(() => { });
            return new WeakReference(target);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RemoveDuringPublish(GrowEvent.Publisher publisher) {
            var target = new Target();
            var subscription = publisher.Event.Subscribe(target.Handle);
            _ = publisher.Event.Subscribe(() => subscription.Dispose());

            publisher.Publish();

            return new WeakReference(target);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference ClearDuringPublish(GrowEvent.Publisher publisher) {
            var target = new Target();
            _ = publisher.Event.Subscribe(target.Handle);
            _ = publisher.Event.Subscribe(() => publisher.Clear());

            publisher.Publish();

            return new WeakReference(target);
        }

        private static void Collect() {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
