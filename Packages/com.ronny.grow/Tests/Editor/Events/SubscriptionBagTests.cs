// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Events;
using NUnit.Framework;

namespace Grow.Tests.Editor.Events {
    public class SubscriptionBagTests {
        [Test]
        public void Dispose_DisposesEveryToken() {
            var publisher = new GrowEvent.Publisher();
            var first = publisher.Event.Subscribe(() => { });
            var second = publisher.Event.Subscribe(() => { });
            var bag = new SubscriptionBag();
            bag.Add(first);
            bag.Add(second);

            Assert.AreEqual(2, bag.Count);

            bag.Dispose();

            Assert.AreEqual(0, bag.Count);
            Assert.IsFalse(first.IsActive);
            Assert.IsFalse(second.IsActive);
        }

        [Test]
        public void Dispose_IsIdempotent() {
            var publisher = new GrowEvent.Publisher();
            var token = publisher.Event.Subscribe(() => { });
            var bag = new SubscriptionBag();
            bag.Add(token);

            bag.Dispose();
            Assert.DoesNotThrow(() => bag.Dispose());

            Assert.IsFalse(token.IsActive);
        }

        [Test]
        public void Add_InactiveToken_IsIgnored() {
            var publisher = new GrowEvent.Publisher();
            var token = publisher.Event.Subscribe(() => { });
            token.Dispose();
            var bag = new SubscriptionBag();

            bag.Add(token);

            Assert.AreEqual(0, bag.Count);
        }

        [Test]
        public void Add_DefaultToken_IsIgnored() {
            var bag = new SubscriptionBag();

            bag.Add(default);

            Assert.AreEqual(0, bag.Count);
        }

        [Test]
        public void Add_AfterDispose_Throws() {
            var bag = new SubscriptionBag();
            bag.Dispose();

            Assert.Throws<ObjectDisposedException>(() => bag.Add(default));
        }

        [Test]
        public void UsingScope_DisposesTokens() {
            var publisher = new GrowEvent.Publisher();
            var token = publisher.Event.Subscribe(() => { });

            using (var bag = new SubscriptionBag()) {
                bag.Add(token);
                Assert.IsTrue(token.IsActive);
            }

            Assert.IsFalse(token.IsActive);
        }

        [Test]
        public void Add_AfterPublisherDispose_IsIgnored() {
            var publisher = new GrowEvent.Publisher();
            var token = publisher.Event.Subscribe(() => { });
            publisher.Dispose();
            var bag = new SubscriptionBag();

            bag.Add(token);

            Assert.AreEqual(0, bag.Count);
        }
    }
}
