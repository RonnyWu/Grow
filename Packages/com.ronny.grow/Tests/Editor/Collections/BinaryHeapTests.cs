// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests.Editor.Collections {
    public class BinaryHeapTests {
        [Test]
        public void Dequeue_ReturnsItemsInAscendingOrder() {
            var heap = new BinaryHeap<int>();
            foreach (var value in new[] { 5, 1, 9, 3, 7, 2, 8, 6, 4 }) {
                heap.Enqueue(value);
            }

            var drained = new List<int>();
            while (!heap.IsEmpty) {
                drained.Add(heap.Dequeue());
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, drained);
        }

        [Test]
        public void Dequeue_RandomInput_MatchesFullySortedList() {
            var random = new Random(20261017);
            var values = new List<int>();
            var heap = new BinaryHeap<int>();
            for (var i = 0; i < 1000; i++) {
                var value = random.Next(-500, 500);
                values.Add(value);
                heap.Enqueue(value);
            }

            values.Sort();
            for (var i = 0; i < values.Count; i++) {
                Assert.AreEqual(values[i], heap.Dequeue());
            }

            Assert.IsTrue(heap.IsEmpty);
        }

        [Test]
        public void Enqueue_DuplicateValues_AreAllReturned() {
            var heap = new BinaryHeap<int>();
            heap.Enqueue(2);
            heap.Enqueue(1);
            heap.Enqueue(2);
            heap.Enqueue(1);

            Assert.AreEqual(1, heap.Dequeue());
            Assert.AreEqual(1, heap.Dequeue());
            Assert.AreEqual(2, heap.Dequeue());
            Assert.AreEqual(2, heap.Dequeue());
        }

        [Test]
        public void Peek_DoesNotRemoveTheHead() {
            var heap = new BinaryHeap<int>();
            heap.Enqueue(4);
            heap.Enqueue(2);

            Assert.AreEqual(2, heap.Peek());
            Assert.AreEqual(2, heap.Count);
            Assert.AreEqual(2, heap.Dequeue());
        }

        [Test]
        public void EmptyHeap_ReportsEmptyAndThrowsOnPeekAndDequeue() {
            var heap = new BinaryHeap<int>();

            Assert.IsTrue(heap.IsEmpty);
            Assert.AreEqual(0, heap.Count);
            Assert.Throws<InvalidOperationException>(() => heap.Peek());
            Assert.Throws<InvalidOperationException>(() => heap.Dequeue());
        }

        [Test]
        public void SingleItem_CanBePeekedAndDequeued() {
            var heap = new BinaryHeap<int>();
            heap.Enqueue(7);

            Assert.AreEqual(1, heap.Count);
            Assert.AreEqual(7, heap.Peek());
            Assert.AreEqual(7, heap.Dequeue());
            Assert.IsTrue(heap.IsEmpty);
        }

        [Test]
        public void Clear_RemovesEveryItem() {
            var heap = new BinaryHeap<int>();
            heap.Enqueue(1);
            heap.Enqueue(2);

            heap.Clear();

            Assert.IsTrue(heap.IsEmpty);
            Assert.AreEqual(0, heap.Count);
        }

        private sealed class OrderedItem : IComparable<OrderedItem> {
            public OrderedItem(int rank) {
                Rank = rank;
            }

            public int Rank { get; }

            public int CompareTo(OrderedItem other) {
                return Rank.CompareTo(other.Rank);
            }
        }

        [Test]
        public void ReferenceType_OrdersByCompareTo() {
            var heap = new BinaryHeap<OrderedItem>();
            heap.Enqueue(new OrderedItem(3));
            heap.Enqueue(new OrderedItem(1));
            heap.Enqueue(new OrderedItem(2));

            Assert.AreEqual(1, heap.Dequeue().Rank);
            Assert.AreEqual(2, heap.Dequeue().Rank);
            Assert.AreEqual(3, heap.Dequeue().Rank);
        }

        private sealed class PlainItem {
            public int Rank;
        }

        [Test]
        public void CustomComparer_CanOrderDescending() {
            var heap =
                new BinaryHeap<int>(Comparer<int>.Create((left, right) => right.CompareTo(left)));
            foreach (var value in new[] { 3, 1, 2 }) {
                heap.Enqueue(value);
            }

            Assert.AreEqual(3, heap.Dequeue());
            Assert.AreEqual(2, heap.Dequeue());
            Assert.AreEqual(1, heap.Dequeue());
        }

        [Test]
        public void CustomComparer_WorksForNonComparableTypes() {
            var heap = new BinaryHeap<PlainItem>(
                Comparer<PlainItem>.Create((left, right) => left.Rank.CompareTo(right.Rank)));
            heap.Enqueue(new PlainItem { Rank = 2 });
            heap.Enqueue(new PlainItem { Rank = 1 });

            Assert.AreEqual(1, heap.Dequeue().Rank);
            Assert.AreEqual(2, heap.Dequeue().Rank);
        }

        [Test]
        public void DefaultComparer_NonComparableType_ThrowsOnFirstComparison() {
            var heap = new BinaryHeap<PlainItem>();
            heap.Enqueue(new PlainItem { Rank = 1 });

            Assert.That(() => heap.Enqueue(new PlainItem { Rank = 2 }), Throws.Exception);
        }

        [Test]
        public void TryPeek_OnEmptyHeap_ReturnsFalseAndDefault() {
            var heap = new BinaryHeap<int>();

            Assert.IsFalse(heap.TryPeek(out var item));
            Assert.AreEqual(0, item);
        }

        [Test]
        public void TryPeek_OnPopulatedHeap_ReturnsTrueWithoutRemoving() {
            var heap = new BinaryHeap<int>();
            heap.Enqueue(5);

            Assert.IsTrue(heap.TryPeek(out var item));
            Assert.AreEqual(5, item);
            Assert.AreEqual(1, heap.Count);
        }

        [Test]
        public void TryDequeue_OnEmptyHeap_ReturnsFalseAndDefault() {
            var heap = new BinaryHeap<int>();

            Assert.IsFalse(heap.TryDequeue(out var item));
            Assert.AreEqual(0, item);
        }

        [Test]
        public void TryDequeue_OnPopulatedHeap_ReturnsTrueAndRemoves() {
            var heap = new BinaryHeap<int>();
            heap.Enqueue(5);
            heap.Enqueue(2);

            Assert.IsTrue(heap.TryDequeue(out var item));
            Assert.AreEqual(2, item);
            Assert.AreEqual(1, heap.Count);
        }
    }
}
