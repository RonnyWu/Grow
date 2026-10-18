// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests.Editor.Collections {
    public class SlotMapTests {
        [Test]
        public void Constructor_RejectsNonPositiveCapacity() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlotMap<int>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlotMap<int>(-1));
        }

        [Test]
        public void Add_ReturnsAValidHandle() {
            var map = new SlotMap<int>();

            var handle = map.Add(42);

            Assert.IsFalse(handle.IsDefault);
            Assert.AreEqual(1, map.Count);
            Assert.IsFalse(map.IsEmpty);
        }

        [Test]
        public void TryGet_ReturnsTheStoredValue() {
            var map = new SlotMap<string>();
            var handle = map.Add("value");

            Assert.IsTrue(map.TryGet(handle, out var value));
            Assert.AreEqual("value", value);
        }

        [Test]
        public void Get_OnStaleHandle_Throws() {
            var map = new SlotMap<int>();

            Assert.Throws<KeyNotFoundException>(() => map.Get(default));
        }

        [Test]
        public void Remove_InvalidatesTheHandle() {
            var map = new SlotMap<int>();
            var handle = map.Add(1);

            Assert.IsTrue(map.Remove(handle));
            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.TryGet(handle, out _));
            Assert.IsFalse(map.Remove(handle));
        }

        [Test]
        public void Remove_ThenAdd_OldHandleStaysInvalid() {
            var map = new SlotMap<string>(1);
            var first = map.Add("a");

            map.Remove(first);
            var second = map.Add("b");

            Assert.AreEqual(first.Index, second.Index);
            Assert.AreNotEqual(first.Generation, second.Generation);
            Assert.IsFalse(map.TryGet(first, out _));
            Assert.IsTrue(map.TryGet(second, out var value));
            Assert.AreEqual("b", value);
        }

        [Test]
        public void Remove_Middle_KeepsTheOtherHandlesValid() {
            var map = new SlotMap<int>();
            var first = map.Add(10);
            var middle = map.Add(20);
            var last = map.Add(30);

            Assert.IsTrue(map.Remove(middle));

            Assert.AreEqual(2, map.Count);
            Assert.IsTrue(map.TryGet(first, out var firstValue));
            Assert.AreEqual(10, firstValue);
            Assert.IsTrue(map.TryGet(last, out var lastValue));
            Assert.AreEqual(30, lastValue);
            Assert.IsFalse(map.TryGet(middle, out _));
        }

        [Test]
        public void ValuesAndHandles_AreDenseAndAligned() {
            var map = new SlotMap<int>();
            map.Add(10);
            map.Add(20);
            map.Add(30);

            Assert.AreEqual(3, map.Values.Length);
            Assert.AreEqual(3, map.Handles.Length);
            for (var i = 0; i < map.Count; i++) {
                Assert.IsTrue(map.TryGet(map.Handles[i], out var value));
                Assert.AreEqual(map.Values[i], value);
            }
        }

        [Test]
        public void Clear_InvalidatesEveryHandle() {
            var map = new SlotMap<int>();
            var first = map.Add(1);
            var second = map.Add(2);

            map.Clear();

            Assert.AreEqual(0, map.Count);
            Assert.IsTrue(map.IsEmpty);
            Assert.IsFalse(map.TryGet(first, out _));
            Assert.IsFalse(map.TryGet(second, out _));
        }

        [Test]
        public void Clear_KeepsCapacityAndAllowsReuse() {
            var map = new SlotMap<int>(2);
            map.Add(1);
            map.Add(2);
            map.Clear();

            var capacity = map.Capacity;
            var handle = map.Add(3);

            Assert.AreEqual(capacity, map.Capacity);
            Assert.AreEqual(1, map.Count);
            Assert.IsTrue(map.TryGet(handle, out var value));
            Assert.AreEqual(3, value);
        }

        [Test]
        public void GrowsBeyondInitialCapacity() {
            var map = new SlotMap<int>(1);
            for (var i = 0; i < 100; i++) {
                map.Add(i);
            }

            Assert.AreEqual(100, map.Count);
            Assert.AreEqual(100, map.Values.Length);
            for (var i = 0; i < 100; i++) {
                Assert.AreEqual(i, map.Values[i]);
            }
        }

        [Test]
        public void FreeSlotsAreReusedWithoutGrowing() {
            var map = new SlotMap<int>(2);
            var first = map.Add(1);
            map.Add(2);

            map.Remove(first);
            map.Add(3);

            Assert.AreEqual(2, map.Capacity);
            Assert.AreEqual(2, map.Count);
        }

        [Test]
        public void HandleEquality_ComparesIndexAndGeneration() {
            var first = new SlotHandle(1, 2);
            var same = new SlotHandle(1, 2);
            var otherGeneration = new SlotHandle(1, 3);
            var otherIndex = new SlotHandle(2, 2);

            Assert.IsTrue(first == same);
            Assert.IsFalse(first != same);
            Assert.IsTrue(first != otherGeneration);
            Assert.IsTrue(first != otherIndex);
            Assert.AreEqual(first.GetHashCode(), same.GetHashCode());
            Assert.IsTrue(default(SlotHandle).IsDefault);
            Assert.IsFalse(first.IsDefault);
        }

        [Test]
        public void ReferenceValues_AllowNull() {
            var map = new SlotMap<string>();
            var handle = map.Add(null);

            Assert.IsTrue(map.TryGet(handle, out var value));
            Assert.IsNull(value);
        }
    }
}
