// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class OrderedSetCompactionTests {
        [Test]
        public void Remove_DoesNotCompactWhileTombstonesBelowActive() {
            var set = new OrderedSet<int>(8);
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Add(4);
            set.Add(5);
            set.Remove(1);
            set.Remove(2);
            Assert.AreEqual(5, set.RegistrationCount);
            Assert.AreEqual(2, set.TombstoneCount);
        }

        [Test]
        public void Remove_CompactsWhenTombstonesReachActive() {
            var set = new OrderedSet<int>(8);
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Add(4);
            set.Add(5);
            set.Remove(1);
            set.Remove(2);
            set.Remove(3);
            Assert.AreEqual(2, set.RegistrationCount);
            Assert.AreEqual(0, set.TombstoneCount);
            CollectionAssert.AreEqual(new[] { 4, 5 }, ToArray(set));
            Assert.IsTrue(set.Contains(4));
            Assert.IsTrue(set.Contains(5));
            Assert.IsFalse(set.Contains(3));
        }

        [Test]
        public void Churn_KeepsRegistrationWithinTwiceActive() {
            var set = new OrderedSet<int>(8);
            for (var i = 0; i < 128; i++) set.Add(i);
            for (var i = 0; i < 128; i++) {
                set.Remove(i);
                set.Add(1000 + i);
            }
            Assert.LessOrEqual(set.RegistrationCount, 2 * set.Count);
        }

        private static T[] ToArray<T>(OrderedSet<T> set) {
            var list = new List<T>(set.Count);
            foreach (var item in set) list.Add(item);
            return list.ToArray();
        }
    }
}
