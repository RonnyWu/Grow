// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests.Editor.Collections {
    public class RingBufferTests {
        [Test]
        public void Constructor_RejectsNonPositiveCapacity() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(-1));
        }

        [Test]
        public void PushPop_ReturnsOldestFirst() {
            var buffer = new RingBuffer<int>(4);
            buffer.Push(1);
            buffer.Push(2);
            buffer.Push(3);

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer.Pop());
            Assert.AreEqual(2, buffer.Pop());
            Assert.AreEqual(3, buffer.Pop());
            Assert.IsTrue(buffer.IsEmpty);
        }

        [Test]
        public void Indexer_IsOldestFirst() {
            var buffer = new RingBuffer<int>(4);
            buffer.Push(10);
            buffer.Push(20);
            buffer.Push(30);

            Assert.AreEqual(10, buffer[0]);
            Assert.AreEqual(20, buffer[1]);
            Assert.AreEqual(30, buffer[2]);
        }

        [Test]
        public void Push_WhenFull_OverwritesOldest() {
            var buffer = new RingBuffer<int>(3);
            foreach (var value in new[] { 1, 2, 3, 4 }) {
                buffer.Push(value);
            }

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(2, buffer[0]);
            Assert.AreEqual(4, buffer[2]);
            Assert.AreEqual(2, buffer.Pop());
        }

        [Test]
        public void TryPush_WhenFull_ReturnsFalseAndKeepsContent() {
            var buffer = new RingBuffer<int>(2);
            buffer.Push(1);
            buffer.Push(2);

            Assert.IsFalse(buffer.TryPush(3));
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(1, buffer[0]);
            Assert.AreEqual(2, buffer[1]);
        }

        [Test]
        public void WrapAround_KeepsTheOrder() {
            var buffer = new RingBuffer<int>(3);
            foreach (var value in new[] { 1, 2, 3, 4, 5, 6 }) {
                buffer.Push(value);
            }

            CollectionAssert.AreEqual(new[] { 4, 5, 6 }, buffer.ToArray());
        }

        [Test]
        public void Peek_DoesNotRemoveTheHead() {
            var buffer = new RingBuffer<int>(3);
            buffer.Push(7);

            Assert.AreEqual(7, buffer.Peek());
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(7, buffer.Pop());
        }

        [Test]
        public void EmptyBuffer_ReportsEmptyAndThrowsOnPeekAndPop() {
            var buffer = new RingBuffer<int>(2);

            Assert.IsTrue(buffer.IsEmpty);
            Assert.IsFalse(buffer.IsFull);
            Assert.Throws<InvalidOperationException>(() => buffer.Peek());
            Assert.Throws<InvalidOperationException>(() => buffer.Pop());
        }

        [Test]
        public void TryPop_OnEmpty_ReturnsFalseAndDefault() {
            var buffer = new RingBuffer<int>(2);

            Assert.IsFalse(buffer.TryPop(out var item));
            Assert.AreEqual(0, item);
        }

        [Test]
        public void TryPeek_OnEmpty_ReturnsFalseAndDefault() {
            var buffer = new RingBuffer<int>(2);

            Assert.IsFalse(buffer.TryPeek(out var item));
            Assert.AreEqual(0, item);
        }

        [Test]
        public void Clear_ResetsTheBuffer() {
            var buffer = new RingBuffer<int>(3);
            buffer.Push(1);
            buffer.Push(2);

            buffer.Clear();

            Assert.IsTrue(buffer.IsEmpty);
            Assert.AreEqual(0, buffer.Count);
            buffer.Push(9);
            Assert.AreEqual(9, buffer.Pop());
        }

        [Test]
        public void ToArray_ReturnsOldestFirst() {
            var buffer = new RingBuffer<int>(4);
            buffer.Push(4);
            buffer.Push(5);
            buffer.Push(6);

            CollectionAssert.AreEqual(new[] { 4, 5, 6 }, buffer.ToArray());
        }

        [Test]
        public void CapacityOne_KeepsOnlyTheNewest() {
            var buffer = new RingBuffer<int>(1);
            buffer.Push(1);
            buffer.Push(2);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(2, buffer[0]);
            Assert.AreEqual(2, buffer.Pop());
        }

        [Test]
        public void Indexer_OutsideTheBuffer_Throws() {
            var buffer = new RingBuffer<int>(2);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[0]);

            buffer.Push(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[1]);
        }

        [Test]
        public void ReferenceType_RoundTrips() {
            var buffer = new RingBuffer<string>(2);
            buffer.Push("a");
            buffer.Push("b");

            Assert.AreEqual("a", buffer.Pop());
            Assert.AreEqual("b", buffer.Pop());
            Assert.IsTrue(buffer.IsEmpty);
        }
    }
}
