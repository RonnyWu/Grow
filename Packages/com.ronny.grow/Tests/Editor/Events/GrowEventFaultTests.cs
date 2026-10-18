// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Events;
using NUnit.Framework;

namespace Grow.Tests.Editor.Events {
    public class GrowEventFaultTests {
        [Test]
        public void ListenerThrows_NoSink_AllListenersRunThenFirstExceptionRethrown() {
            var calls = new List<string>();
            var publisher = new GrowEvent.Publisher();
            var growEvent = publisher.Event;
            _ = growEvent.Subscribe(() => calls.Add("A"));
            _ = growEvent.Subscribe(() => throw new InvalidOperationException("boom"));
            _ = growEvent.Subscribe(() => calls.Add("C"));

            var exception = Assert.Throws<InvalidOperationException>(() => publisher.Publish());

            Assert.AreEqual("boom", exception.Message);
            CollectionAssert.AreEqual(new[] { "A", "C" }, calls);
        }

        [Test]
        public void ListenerThrows_WithSink_SinkReceivesAndPublishDoesNotThrow() {
            var calls = new List<string>();
            var faults = new List<Exception>();
            var publisher = new GrowEvent.Publisher(faults.Add);
            var growEvent = publisher.Event;
            _ = growEvent.Subscribe(() => calls.Add("A"));
            _ = growEvent.Subscribe(() => throw new InvalidOperationException("boom"));
            _ = growEvent.Subscribe(() => calls.Add("C"));

            Assert.DoesNotThrow(() => publisher.Publish());

            CollectionAssert.AreEqual(new[] { "A", "C" }, calls);
            Assert.AreEqual(1, faults.Count);
            Assert.AreEqual("boom", faults[0].Message);
        }

        [Test]
        public void MultipleListenersThrow_NoSink_FirstExceptionRethrown() {
            var publisher = new GrowEvent.Publisher();
            var growEvent = publisher.Event;
            _ = growEvent.Subscribe(() => throw new InvalidOperationException("first"));
            _ = growEvent.Subscribe(() => throw new InvalidOperationException("second"));

            var exception = Assert.Throws<InvalidOperationException>(() => publisher.Publish());

            Assert.AreEqual("first", exception.Message);
        }

        [Test]
        public void MultipleListenersThrow_WithSink_AllReported() {
            var faults = new List<Exception>();
            var publisher = new GrowEvent.Publisher(faults.Add);
            var growEvent = publisher.Event;
            _ = growEvent.Subscribe(() => throw new InvalidOperationException("first"));
            _ = growEvent.Subscribe(() => throw new InvalidOperationException("second"));

            Assert.DoesNotThrow(() => publisher.Publish());

            Assert.AreEqual(2, faults.Count);
            Assert.AreEqual("first", faults[0].Message);
            Assert.AreEqual("second", faults[1].Message);
        }

        [Test]
        public void SinkThrows_IsContained() {
            var publisher = new GrowEvent.Publisher(_ => throw new ApplicationException("sink"));
            _ = publisher.Event.Subscribe(() => throw new InvalidOperationException("listener"));

            Assert.DoesNotThrow(() => publisher.Publish());
        }

        [Test]
        public void NestedPublish_FaultSurfacesAtOutermostPublish() {
            var calls = 0;
            var nested = false;
            var publisher = new GrowEvent.Publisher();
            _ = publisher.Event.Subscribe(() => {
                calls++;
                if (nested) {
                    throw new InvalidOperationException("nested");
                }

                nested = true;
                publisher.Publish();
            });

            var exception = Assert.Throws<InvalidOperationException>(() => publisher.Publish());

            Assert.AreEqual("nested", exception.Message);
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void Clear_DoesNotDropEarlierFault() {
            var publisher = new GrowEvent.Publisher();
            _ = publisher.Event.Subscribe(() => throw new InvalidOperationException("boom"));
            _ = publisher.Event.Subscribe(() => publisher.Clear());

            var exception = Assert.Throws<InvalidOperationException>(() => publisher.Publish());

            Assert.AreEqual("boom", exception.Message);
        }

        [Test]
        public void Generic_ListenerThrows_NoSink_AllListenersRunThenFirstExceptionRethrown() {
            var calls = new List<int>();
            var publisher = new GrowEvent<int>.Publisher();
            var growEvent = publisher.Event;
            _ = growEvent.Subscribe(value => calls.Add(value));
            _ = growEvent.Subscribe(_ => throw new InvalidOperationException("boom"));
            _ = growEvent.Subscribe(value => calls.Add(value + 10));

            var exception = Assert.Throws<InvalidOperationException>(() => publisher.Publish(1));

            Assert.AreEqual("boom", exception.Message);
            CollectionAssert.AreEqual(new[] { 1, 11 }, calls);
        }
    }
}
