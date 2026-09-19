// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class SnapshotSetTests {
        [Test]
        public void AddRemoveContains_DelegateToStorage() {
            var set = new SnapshotSet<int>();
            Assert.IsTrue(set.Add(1));
            Assert.IsFalse(set.Add(1));
            Assert.AreEqual(1, set.Count);
            Assert.IsTrue(set.Contains(1));
            Assert.IsTrue(set.Remove(1));
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void BeginRead_ReturnsInsertionOrderSnapshot() {
            var set = new SnapshotSet<int>();
            set.Add(3);
            set.Add(1);
            set.Add(2);
            Assert.IsTrue(set.BeginRead(out var items, out var count));
            Assert.AreEqual(3, count);
            Assert.AreEqual(3, items[0]);
            Assert.AreEqual(1, items[1]);
            Assert.AreEqual(2, items[2]);
            set.EndRead();
        }

        [Test]
        public void BeginRead_Empty_ReturnsFalseWithoutEnteringDepth() {
            var set = new SnapshotSet<int>();
            Assert.IsFalse(set.BeginRead(out _, out var count));
            Assert.AreEqual(0, count);
            Assert.AreEqual(0, set.ReadDepth);
        }

        [Test]
        public void MutationDuringRead_DoesNotAffectCurrentSnapshot() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            Assert.IsTrue(set.BeginRead(out var items, out var count));
            set.Add(2);
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, items[0]);
            set.EndRead();
        }

        [Test]
        public void MutationAfterRead_VisibleOnNextRead() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            Assert.IsTrue(set.BeginRead(out _, out _));
            set.EndRead();
            set.Add(2);
            Assert.IsTrue(set.BeginRead(out var items, out var count));
            Assert.AreEqual(2, count);
            Assert.AreEqual(2, items[1]);
            set.EndRead();
        }

        [Test]
        public void BeginRead_SteadyState_ReusesSnapshotWithoutRebuild() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            Assert.IsTrue(set.BeginRead(out var first, out _));
            set.EndRead();
            Assert.IsTrue(set.BeginRead(out var second, out _));
            Assert.AreSame(first, second);
            set.EndRead();
        }
    }
}
