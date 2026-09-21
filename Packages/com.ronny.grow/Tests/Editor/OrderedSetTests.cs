// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class OrderedSetTests {
        [Test]
        public void Add_NewItem_ReturnsTrueAndIncrementsCount() {
            var set = new OrderedSet<int>();
            Assert.IsTrue(set.Add(1));
            Assert.IsTrue(set.Add(2));
            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void Add_Duplicate_ReturnsFalseAndKeepsCount() {
            var set = new OrderedSet<int>();
            set.Add(7);
            Assert.IsFalse(set.Add(7));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void Contains_ReflectsAdds() {
            var set = new OrderedSet<int>();
            set.Add(5);
            Assert.IsTrue(set.Contains(5));
            Assert.IsFalse(set.Contains(6));
        }

        [Test]
        public void Enumerate_YieldsInsertionOrder() {
            var set = new OrderedSet<int>();
            set.Add(3);
            set.Add(1);
            set.Add(2);
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, ToArray(set));
        }

        [Test]
        public void Add_WithCustomComparer_DeduplicatesByComparer() {
            var set = new OrderedSet<string>(System.StringComparer.OrdinalIgnoreCase, 4);
            Assert.IsTrue(set.Add("A"));
            Assert.IsFalse(set.Add("a"));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void Remove_ExistingItem_ReturnsTrueAndDecrementsCount() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            Assert.IsTrue(set.Remove(1));
            Assert.AreEqual(1, set.Count);
            Assert.IsFalse(set.Contains(1));
            Assert.IsTrue(set.Contains(2));
        }

        [Test]
        public void Remove_MissingItem_ReturnsFalseAndKeepsCount() {
            var set = new OrderedSet<int>();
            set.Add(1);
            Assert.IsFalse(set.Remove(9));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void Remove_PreservesOrderOfSurvivors() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Add(4);
            set.Remove(2);
            set.Remove(4);
            CollectionAssert.AreEqual(new[] { 1, 3 }, ToArray(set));
        }

        [Test]
        public void RemoveThenAdd_AppendsToTail() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Remove(1);
            set.Add(1);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, ToArray(set));
        }

        [Test]
        public void Enumerate_DuringMutation_Throws() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            Assert.Throws<System.InvalidOperationException>(() => {
                foreach (var _ in set) set.Add(99);
            });
        }

        [Test]
        public void Clear_RemovesAllItemsAndResetsStorage() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Remove(1);
            set.Clear();
            Assert.AreEqual(0, set.Count);
            Assert.AreEqual(0, set.RegistrationCount);
            Assert.AreEqual(0, set.TombstoneCount);
        }

        [Test]
        public void Clear_AllowsReuseInInsertionOrder() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            set.Clear();
            set.Add(3);
            set.Add(4);
            CollectionAssert.AreEqual(new[] { 3, 4 }, ToArray(set));
        }

        [Test]
        public void Version_IsMonotonicAndIgnoresDuplicates() {
            var set = new OrderedSet<int>();
            var v0 = set.Version;
            set.Add(1);
            var v1 = set.Version;
            set.Add(1);
            var v2 = set.Version;
            set.Remove(1);
            var v3 = set.Version;
            set.Clear();
            var v4 = set.Version;
            Assert.Greater(v1, v0);
            Assert.AreEqual(v1, v2);
            Assert.Greater(v3, v2);
            Assert.Greater(v4, v3);
        }

        private static T[] ToArray<T>(OrderedSet<T> set) {
            var list = new List<T>(set.Count);
            foreach (var item in set) list.Add(item);
            return list.ToArray();
        }
    }
}
