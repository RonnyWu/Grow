// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests.Editor.Collections {
    public class SortedBagTests {
        private sealed class Entry {
            public int Key;
            public int Serial;
        }

        private sealed class KeyComparer : IComparer<Entry> {
            public int Compare(Entry x, Entry y) {
                return x.Key.CompareTo(y.Key);
            }
        }

        [Test]
        public void Enqueue_KeepsItemsSorted() {
            var bag = new SortedBag<int>();
            foreach (var value in new[] { 5, 1, 9, 3, 7 }) {
                bag.Enqueue(value);
            }

            CollectionAssert.AreEqual(new[] { 1, 3, 5, 7, 9 }, Drain(bag));
        }

        [Test]
        public void Enqueue_DuplicateValues_KeepInsertionOrder() {
            var bag = new SortedBag<Entry>(new KeyComparer());
            bag.Enqueue(new Entry { Key = 2, Serial = 1 });
            bag.Enqueue(new Entry { Key = 1, Serial = 2 });
            bag.Enqueue(new Entry { Key = 1, Serial = 3 });
            bag.Enqueue(new Entry { Key = 1, Serial = 4 });

            CollectionAssert.AreEqual(new[] { 2, 3, 4, 1 }, Serials(bag));
        }

        [Test]
        public void Indexer_ReturnsItemsInAscendingOrder() {
            var bag = new SortedBag<int>();
            bag.Enqueue(3);
            bag.Enqueue(1);
            bag.Enqueue(2);

            Assert.AreEqual(1, bag[0]);
            Assert.AreEqual(2, bag[1]);
            Assert.AreEqual(3, bag[2]);
        }

        [Test]
        public void Indexer_OutsideTheBag_Throws() {
            var bag = new SortedBag<int>();
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = bag[0]);
        }

        [Test]
        public void Enumeration_IsSortedAndSurvivesMutationBetweenRuns() {
            var bag = new SortedBag<int>();
            bag.Enqueue(4);
            bag.Enqueue(1);
            bag.Enqueue(3);

            CollectionAssert.AreEqual(new[] { 1, 3, 4 }, new List<int>(bag));

            bag.Enqueue(2);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, new List<int>(bag));
        }

        [Test]
        public void ReadOnlyList_ContractWorks() {
            IReadOnlyList<int> bag = new SortedBag<int>();
            ((SortedBag<int>)bag).Enqueue(2);
            ((SortedBag<int>)bag).Enqueue(1);

            Assert.AreEqual(2, bag.Count);
            Assert.AreEqual(1, bag[0]);
            Assert.AreEqual(2, bag[1]);
        }

        [Test]
        public void IndexOf_ReturnsTheFirstEqualItem() {
            var bag = new SortedBag<Entry>(new KeyComparer());
            bag.Enqueue(new Entry { Key = 1, Serial = 10 });
            bag.Enqueue(new Entry { Key = 1, Serial = 11 });
            bag.Enqueue(new Entry { Key = 2, Serial = 12 });

            Assert.AreEqual(0, bag.IndexOf(new Entry { Key = 1 }));
            Assert.AreEqual(2, bag.IndexOf(new Entry { Key = 2 }));
            Assert.AreEqual(-1, bag.IndexOf(new Entry { Key = 3 }));
        }

        [Test]
        public void Remove_RemovesTheFirstEqualItemAndReportsWhetherAnythingHappened() {
            var bag = new SortedBag<Entry>(new KeyComparer());
            bag.Enqueue(new Entry { Key = 1, Serial = 10 });
            bag.Enqueue(new Entry { Key = 1, Serial = 11 });

            Assert.IsTrue(bag.Remove(new Entry { Key = 1 }));
            Assert.AreEqual(1, bag.Count);
            CollectionAssert.AreEqual(new[] { 11 }, Serials(bag));

            Assert.IsFalse(bag.Remove(new Entry { Key = 99 }));
            Assert.AreEqual(1, bag.Count);
        }

        [Test]
        public void RemoveAt_RemovesByIndex() {
            var bag = new SortedBag<int>();
            bag.Enqueue(1);
            bag.Enqueue(2);
            bag.Enqueue(3);

            bag.RemoveAt(1);

            CollectionAssert.AreEqual(new[] { 1, 3 }, Drain(bag));
            Assert.Throws<ArgumentOutOfRangeException>(() => bag.RemoveAt(5));
        }

        [Test]
        public void EnqueueRange_FullSort_AndIncremental_ProduceTheSameOrder() {
            var incremental = new SortedBag<int>();
            incremental.EnqueueRange(new[] { 4, 1, 3, 2 });

            var sorted = new SortedBag<int>();
            sorted.EnqueueRange(new[] { 4, 1, 3, 2 }, true);

            CollectionAssert.AreEqual(Drain(incremental), Drain(sorted));
        }

        [Test]
        public void EnqueueRange_FullSort_KeepsExistingItems() {
            var bag = new SortedBag<int>();
            bag.Enqueue(5);
            bag.EnqueueRange(new[] { 7, 6 }, true);

            CollectionAssert.AreEqual(new[] { 5, 6, 7 }, Drain(bag));
        }

        [Test]
        public void EnqueueRange_FullSort_KeepsEqualItemsInInsertionOrder() {
            var bag = new SortedBag<Entry>(new KeyComparer());
            bag.Enqueue(new Entry { Key = 1, Serial = 1 });
            bag.Enqueue(new Entry { Key = 1, Serial = 2 });

            bag.EnqueueRange(new[] {
                new Entry { Key = 1, Serial = 3 },
                new Entry { Key = 0, Serial = 4 },
                new Entry { Key = 1, Serial = 5 }
            }, fullSort: true);

            CollectionAssert.AreEqual(new[] { 4, 1, 2, 3, 5 }, Serials(bag));
        }

        [Test]
        public void EnqueueRange_NullValues_Throws() {
            var bag = new SortedBag<int>();
            Assert.Throws<ArgumentNullException>(() => bag.EnqueueRange(null));
        }

        [Test]
        public void Peek_DoesNotRemoveTheHead() {
            var bag = new SortedBag<int>();
            bag.Enqueue(4);
            bag.Enqueue(2);

            Assert.AreEqual(2, bag.Peek());
            Assert.AreEqual(2, bag.Count);
            Assert.AreEqual(2, bag.Dequeue());
        }

        [Test]
        public void EmptyBag_ReportsEmptyAndThrowsOnPeekAndDequeue() {
            var bag = new SortedBag<int>();

            Assert.IsTrue(bag.IsEmpty);
            Assert.AreEqual(0, bag.Count);
            Assert.Throws<InvalidOperationException>(() => bag.Peek());
            Assert.Throws<InvalidOperationException>(() => bag.Dequeue());
        }

        [Test]
        public void Find_ReturnsFirstMatchOrDefault() {
            var bag = new SortedBag<int>();
            bag.Enqueue(1);
            bag.Enqueue(2);
            bag.Enqueue(3);

            Assert.AreEqual(2, bag.Find(value => value == 2));
            Assert.AreEqual(0, bag.Find(value => value == 42));
            Assert.Throws<ArgumentNullException>(() => bag.Find(null));
        }

        [Test]
        public void TryFind_DistinguishesDefaultValueFromNoMatch() {
            var bag = new SortedBag<int>();
            bag.Enqueue(0);
            bag.Enqueue(1);

            Assert.IsTrue(bag.TryFind(value => value == 0, out var found));
            Assert.AreEqual(0, found);
            Assert.IsFalse(bag.TryFind(value => value == 42, out var missing));
            Assert.AreEqual(0, missing);
            Assert.Throws<ArgumentNullException>(() => bag.TryFind(null, out _));
        }

        [Test]
        public void Clear_RemovesEveryItem() {
            var bag = new SortedBag<int>();
            bag.Enqueue(1);
            bag.Enqueue(2);

            bag.Clear();

            Assert.IsTrue(bag.IsEmpty);
        }

        [Test]
        public void CustomComparer_CanOrderDescending() {
            var bag = new SortedBag<int>(
                Comparer<int>.Create((left, right) => right.CompareTo(left)));
            bag.Enqueue(3);
            bag.Enqueue(1);
            bag.Enqueue(2);

            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, Drain(bag));
        }

        private static List<int> Drain(SortedBag<int> bag) {
            var drained = new List<int>();
            while (!bag.IsEmpty) {
                drained.Add(bag.Dequeue());
            }

            return drained;
        }

        private static List<int> Serials(SortedBag<Entry> bag) {
            var serials = new List<int>();
            foreach (var entry in bag) {
                serials.Add(entry.Serial);
            }

            return serials;
        }
    }
}
