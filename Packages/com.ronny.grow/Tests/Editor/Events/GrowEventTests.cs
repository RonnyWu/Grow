// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Threading;
using Grow.Core.Events;
using NUnit.Framework;

namespace Grow.Tests.Editor.Events {
    public class GrowEventTests {
        private GrowEvent.Publisher _publisher;
        private GrowEvent _event;
        private List<string> _log;

        [SetUp]
        public void SetUp() {
            _publisher = new GrowEvent.Publisher();
            _event = _publisher.Event;
            _log = new List<string>();
        }

        [TearDown]
        public void TearDown() {
            _publisher.Dispose();
            _publisher = null;
            _event = null;
            _log = null;
        }

        private Action Token(string name) {
            return () => _log.Add(name);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNothing() {
            Assert.DoesNotThrow(() => _publisher.Publish());
            Assert.AreEqual(0, _event.Count);
        }

        [Test]
        public void Subscribe_ThenPublish_InvokesHandler() {
            _ = _event.Subscribe(Token("A"));

            _publisher.Publish();

            CollectionAssert.AreEqual(new[] { "A" }, _log);
        }

        [Test]
        public void Subscribers_RunInRegistrationOrder() {
            _ = _event.Subscribe(Token("A"));
            _ = _event.Subscribe(Token("B"));
            _ = _event.Subscribe(Token("C"));

            _publisher.Publish();

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, _log);
        }

        [Test]
        public void Dispose_DuringPublish_FinishesCurrentPassAndKeepsEarlierFault() {
            var order = new List<string>();
            var publisher = new GrowEvent.Publisher();
            publisher.Event.Subscribe(() => {
                order.Add("A");
                throw new InvalidOperationException("listener failure");
            });
            publisher.Event.Subscribe(() => {
                order.Add("B");
                publisher.Dispose();
            });
            publisher.Event.Subscribe(() => order.Add("C"));

            Assert.Throws<InvalidOperationException>(() => publisher.Publish());
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, order);
            Assert.IsTrue(publisher.IsDisposed);
        }

        [Test]
        public void SubscribeOnce_InvokesOnce() {
            var calls = 0;
            _ = _event.SubscribeOnce(() => calls++);

            _publisher.Publish();
            _publisher.Publish();

            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, _event.Count);
        }

        [Test]
        public void SubscribeOnce_NestedPublish_DoesNotInvokeAgain() {
            var calls = 0;
            Action handler = null;
            handler = () => {
                calls++;
                _publisher.Publish();
            };
            _ = _event.SubscribeOnce(handler);

            _publisher.Publish();

            Assert.AreEqual(1, calls, "the one-shot slot must be consumed before a nested publish");
        }

        [Test]
        public void SubscribeOnce_TokenIsInactiveAfterCall() {
            var subscription = _event.SubscribeOnce(() => { });

            _publisher.Publish();

            Assert.IsFalse(subscription.IsActive);
            Assert.DoesNotThrow(() => subscription.Dispose());
        }

        [Test]
        public void SubscribeOnce_DisposeBeforePublish_DoesNotInvoke() {
            var subscription = _event.SubscribeOnce(() => Assert.Fail("must not run"));
            subscription.Dispose();

            _publisher.Publish();
        }

        [Test]
        public void SubscribeOnce_DuringPublish_RunsOnTheNextPass() {
            var onceCalls = 0;
            var subscribed = false;
            Action first = () => {
                if (subscribed) {
                    return;
                }

                subscribed = true;
                _ = _event.SubscribeOnce(() => onceCalls++);
            };
            _ = _event.Subscribe(first);

            _publisher.Publish();
            Assert.AreEqual(0, onceCalls, "a subscription added during a pass is not visible to that pass");

            _publisher.Publish();
            Assert.AreEqual(1, onceCalls);

            _publisher.Publish();
            Assert.AreEqual(1, onceCalls);
        }

        [Test]
        public void SubscribeOnce_CountRemovesAfterPass() {
            _ = _event.SubscribeOnce(() => { });
            Assert.AreEqual(1, _event.Count);

            _publisher.Publish();

            Assert.AreEqual(0, _event.Count);
        }

        [Test]
        public void Dispose_RemovesOnlyItsOwnSubscription() {
            var handler = Token("A");
            var first = _event.Subscribe(handler);
            _ = _event.Subscribe(handler);

            first.Dispose();
            _publisher.Publish();

            CollectionAssert.AreEqual(new[] { "A" }, _log);
        }

        [Test]
        public void Dispose_Twice_IsIdempotent() {
            var subscription = _event.Subscribe(Token("A"));

            subscription.Dispose();
            Assert.DoesNotThrow(() => subscription.Dispose());

            _publisher.Publish();
            CollectionAssert.IsEmpty(_log);
        }

        [Test]
        public void DefaultSubscription_DisposeIsNoOp() {
            var subscription = default(GrowSubscription);

            Assert.DoesNotThrow(() => subscription.Dispose());
            Assert.IsFalse(subscription.IsActive);
        }

        [Test]
        public void Subscribe_Null_Throws() {
            Assert.Throws<ArgumentNullException>(() => _event.Subscribe(null));
        }

        [Test]
        public void ReAdd_AfterDispose_GoesToTail() {
            var a = Token("A");
            var b = Token("B");
            var c = Token("C");
            _ = _event.Subscribe(a);
            _ = _event.Subscribe(b);
            var cSubscription = _event.Subscribe(c);

            cSubscription.Dispose();
            _ = _event.Subscribe(c);

            _publisher.Publish();

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, _log);
        }

        [Test]
        public void Subscribe_DuringPublish_TakesEffectNextPass() {
            var added = false;
            Action a = () => {
                _log.Add("A");
                if (added) {
                    return;
                }

                added = true;
                _ = _event.Subscribe(Token("B"));
            };
            _ = _event.Subscribe(a);
            _ = _event.Subscribe(Token("C"));

            _publisher.Publish();

            CollectionAssert.AreEqual(new[] { "A", "C" }, _log, "B must not run in the pass that adds it");

            _publisher.Publish();

            CollectionAssert.AreEqual(new[] { "A", "C", "A", "C", "B" }, _log);
        }

        [Test]
        public void Dispose_DuringPublish_KeepsCurrentPass() {
            GrowSubscription subscription = default;
            var removed = false;
            Action a = () => {
                _log.Add("A");
                if (removed) {
                    return;
                }

                removed = true;
                subscription.Dispose();
            };
            _ = _event.Subscribe(a);
            subscription = _event.Subscribe(Token("B"));

            _publisher.Publish();

            CollectionAssert.AreEqual(new[] { "A", "B" }, _log, "B still runs in the current pass");

            _publisher.Publish();

            CollectionAssert.AreEqual(new[] { "A", "B", "A" }, _log);
        }

        [Test]
        public void Clear_DuringPublish_FinishesCurrentPassThenEmpty() {
            var cleared = false;
            Action a = () => {
                _log.Add("A");
                if (cleared) {
                    return;
                }

                cleared = true;
                _publisher.Clear();
            };
            _ = _event.Subscribe(a);
            _ = _event.Subscribe(Token("B"));

            _publisher.Publish();

            CollectionAssert.AreEqual(new[] { "A", "B" }, _log, "Clear does not interrupt the current pass");
            Assert.AreEqual(0, _event.Count);

            _publisher.Publish();

            CollectionAssert.AreEqual(new[] { "A", "B" }, _log);
        }

        [Test]
        public void Clear_OutsidePublish_ReleasesSubscriptions() {
            _ = _event.Subscribe(Token("A"));
            _ = _event.Subscribe(Token("B"));

            _publisher.Clear();

            Assert.AreEqual(0, _event.Count);
            _publisher.Publish();
            CollectionAssert.IsEmpty(_log);
        }

        [Test]
        public void Publish_Reentrant_LeavesEventUsable() {
            var calls = 0;
            Action a = () => {
                calls++;
                if (calls == 1) {
                    _publisher.Publish();
                }
            };
            _ = _event.Subscribe(a);

            _publisher.Publish();

            Assert.AreEqual(2, calls, "the nested publish runs synchronously");
            Assert.IsFalse(_publisher.IsDispatching, "the nesting depth must return to zero");

            _publisher.Publish();
            Assert.AreEqual(3, calls, "a publish after a reentrant publish must still dispatch");

            _ = _event.Subscribe(() => calls += 100);
            _publisher.Publish();
            Assert.AreEqual(104, calls, "subscriptions after a reentrant publish must still apply");
        }

        [Test]
        public void Publish_ExceedsNestingCap_Throws() {
            Action a = () => _publisher.Publish();
            _ = _event.Subscribe(a);

            var exception = Assert.Throws<InvalidOperationException>(() => _publisher.Publish());

            StringAssert.Contains("nesting cap", exception.Message);
            Assert.IsFalse(_publisher.IsDispatching);
        }

        [Test]
        public void Token_AfterClear_IsInactive() {
            var subscription = _event.Subscribe(Token("A"));

            _publisher.Clear();

            Assert.IsFalse(subscription.IsActive);
            Assert.DoesNotThrow(() => subscription.Dispose());
            _publisher.Publish();
            CollectionAssert.IsEmpty(_log);
        }

        [Test]
        public void OldToken_DoesNotRemoveNewSubscription() {
            var handler = Token("A");
            var oldSubscription = _event.Subscribe(handler);
            oldSubscription.Dispose();
            _ = _event.Subscribe(handler);

            oldSubscription.Dispose();

            Assert.AreEqual(1, _event.Count);
            _publisher.Publish();
            CollectionAssert.AreEqual(new[] { "A" }, _log);
        }

        [Test]
        public void Count_ReflectsAppliedSubscriptions() {
            _ = _event.Subscribe(Token("A"));
            _ = _event.Subscribe(Token("B"));

            Assert.AreEqual(2, _event.Count);

            _publisher.Clear();

            Assert.AreEqual(0, _event.Count);
        }

        [Test]
        public void IsDispatching_TrueDuringPublishFalseAfter() {
            var seen = false;
            Action a = () => seen = _publisher.IsDispatching;
            _ = _event.Subscribe(a);

            Assert.IsFalse(_publisher.IsDispatching);
            _publisher.Publish();
            Assert.IsTrue(seen);
            Assert.IsFalse(_publisher.IsDispatching);
        }

        [Test]
        public void Publisher_Dispose_IsTerminal() {
            _ = _event.Subscribe(Token("A"));

            _publisher.Dispose();

            Assert.IsTrue(_publisher.IsDisposed);
            Assert.AreEqual(0, _event.Count);
            _publisher.Publish();
            CollectionAssert.IsEmpty(_log);
            Assert.Throws<ObjectDisposedException>(() => _event.Subscribe(Token("B")));
            Assert.DoesNotThrow(() => _publisher.Dispose());
        }

        [Test]
        public void Publisher_Dispose_OldTokenIsInactive() {
            var subscription = _event.Subscribe(Token("A"));

            _publisher.Dispose();

            Assert.IsFalse(subscription.IsActive);
            Assert.DoesNotThrow(() => subscription.Dispose());
        }

        [Test]
        public void Subscribe_FromAnotherThread_Throws() {
            var captured = CaptureOnWorkerThread(() => _event.Subscribe(Token("A")));
            Assert.IsInstanceOf<InvalidOperationException>(captured);
        }

        [Test]
        public void Dispose_FromAnotherThread_Throws() {
            var subscription = _event.Subscribe(Token("A"));
            var captured = CaptureOnWorkerThread(subscription.Dispose);
            Assert.IsInstanceOf<InvalidOperationException>(captured);
        }

        [Test]
        public void Publish_FromAnotherThread_Throws() {
            var captured = CaptureOnWorkerThread(() => _publisher.Publish());
            Assert.IsInstanceOf<InvalidOperationException>(captured);
        }

        [Test]
        public void Count_FromAnotherThread_Throws() {
            var captured = CaptureOnWorkerThread(() => _ = _event.Count);
            Assert.IsInstanceOf<InvalidOperationException>(captured);
        }

        [Test]
        public void IsActive_FromAnotherThread_Throws() {
            var subscription = _event.Subscribe(Token("A"));
            var captured = CaptureOnWorkerThread(() => _ = subscription.IsActive);
            Assert.IsInstanceOf<InvalidOperationException>(captured);
        }

        [Test]
        public void Clear_FromAnotherThread_Throws() {
            var captured = CaptureOnWorkerThread(() => _publisher.Clear());
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
