// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Threading;
using Grow.Core.Events;
using NUnit.Framework;

namespace Grow.Tests.Editor.Events {
    public class GrowEventTTests {
        private GrowEvent<int>.Publisher _publisher;
        private GrowEvent<int> _event;

        [SetUp]
        public void SetUp() {
            _publisher = new GrowEvent<int>.Publisher();
            _event = _publisher.Event;
        }

        [TearDown]
        public void TearDown() {
            _publisher.Dispose();
            _publisher = null;
            _event = null;
        }

        [Test]
        public void Publish_PassesPayloadToEverySubscriber() {
            var received = new List<int>();
            _ = _event.Subscribe(value => received.Add(value));
            _ = _event.Subscribe(value => received.Add(value * 10));

            _publisher.Publish(3);

            CollectionAssert.AreEqual(new[] { 3, 30 }, received);
        }

        [Test]
        public void Subscribers_RunInRegistrationOrder() {
            var received = new List<string>();
            _ = _event.Subscribe(_ => received.Add("A"));
            _ = _event.Subscribe(_ => received.Add("B"));
            _ = _event.Subscribe(_ => received.Add("C"));

            _publisher.Publish(0);

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, received);
        }

        [Test]
        public void SubscribeOnce_InvokesOnceWithPayload() {
            var received = new List<int>();
            _ = _event.SubscribeOnce(value => received.Add(value));

            _publisher.Publish(1);
            _publisher.Publish(2);

            CollectionAssert.AreEqual(new[] { 1 }, received);
            Assert.AreEqual(0, _event.Count);
        }

        [Test]
        public void SubscribeOnce_NestedPublish_DoesNotInvokeAgain() {
            var calls = 0;
            Action<int> handler = null;
            handler = value => {
                calls++;
                _publisher.Publish(value + 1);
            };
            _ = _event.SubscribeOnce(handler);

            _publisher.Publish(0);

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void SubscribeOnce_DuringPublish_RunsOnTheNextPass() {
            var onceCalls = 0;
            var subscribed = false;
            Action<int> first = value => {
                if (subscribed) return;
                subscribed = true;
                _ = _event.SubscribeOnce(_ => onceCalls++);
            };
            _ = _event.Subscribe(first);

            _publisher.Publish(0);
            Assert.AreEqual(0, onceCalls);

            _publisher.Publish(0);
            Assert.AreEqual(1, onceCalls);

            _publisher.Publish(0);
            Assert.AreEqual(1, onceCalls);
        }

        [Test]
        public void SubscribeOnce_CountRemovesAfterPass() {
            _ = _event.SubscribeOnce(_ => { });
            Assert.AreEqual(1, _event.Count);

            _publisher.Publish(0);

            Assert.AreEqual(0, _event.Count);
        }

        [Test]
        public void Dispose_RemovesOnlyItsOwnSubscription() {
            var calls = 0;
            Action<int> handler = _ => calls++;
            var first = _event.Subscribe(handler);
            _ = _event.Subscribe(handler);

            first.Dispose();
            _publisher.Publish(0);

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Publish_Reentrant_LeavesEventUsable() {
            var received = new List<int>();
            Action<int> handler = value => {
                received.Add(value);
                if (value == 1) _publisher.Publish(2);
            };
            _ = _event.Subscribe(handler);

            _publisher.Publish(1);

            CollectionAssert.AreEqual(new[] { 1, 2 }, received);
            Assert.IsFalse(_publisher.IsDispatching);

            _publisher.Publish(3);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, received);
        }

        [Test]
        public void Publish_ExceedsNestingCap_Throws() {
            _ = _event.Subscribe(value => _publisher.Publish(value));

            var exception = Assert.Throws<InvalidOperationException>(() => _publisher.Publish(1));

            StringAssert.Contains("nesting cap", exception.Message);
            Assert.IsFalse(_publisher.IsDispatching);
        }

        [Test]
        public void Payload_ReferenceType_IsPassed() {
            var referenceEvent = new GrowEvent<string>.Publisher();
            string received = null;
            _ = referenceEvent.Event.Subscribe(value => received = value);

            var payload = "hello";
            referenceEvent.Publish(payload);

            Assert.AreSame(payload, received);
        }

        [Test]
        public void Publisher_Dispose_IsTerminal() {
            _ = _event.Subscribe(_ => { });

            _publisher.Dispose();

            Assert.IsTrue(_publisher.IsDisposed);
            Assert.AreEqual(0, _event.Count);
            _publisher.Publish(1);
            Assert.Throws<ObjectDisposedException>(() => _event.Subscribe(_ => { }));
            Assert.DoesNotThrow(() => _publisher.Dispose());
        }

        [Test]
        public void Subscribe_FromAnotherThread_Throws() {
            var captured = CaptureOnWorkerThread(() => _event.Subscribe(_ => { }));
            Assert.IsInstanceOf<InvalidOperationException>(captured);
        }

        [Test]
        public void Publish_FromAnotherThread_Throws() {
            var captured = CaptureOnWorkerThread(() => _publisher.Publish(0));
            Assert.IsInstanceOf<InvalidOperationException>(captured);
        }

        [Test]
        public void Count_FromAnotherThread_Throws() {
            var captured = CaptureOnWorkerThread(() => _ = _event.Count);
            Assert.IsInstanceOf<InvalidOperationException>(captured);
        }

        private static Exception CaptureOnWorkerThread(Action action) {
            Exception captured = null;
            var thread = new Thread(() => {
                try {
                    action();
                } catch (Exception exception) {
                    captured = exception;
                }
            });
            thread.Start();
            thread.Join();
            return captured;
        }
    }
}
