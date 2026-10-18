// SPDX-License-Identifier: MIT

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Grow.Core.Pool;
using NUnit.Framework;

namespace Grow.Tests.Editor.Pool {
    public class ConcurrentObjectPoolTests {
        private sealed class Item {
        }

        private static ConcurrentObjectPool<Item> CreatePool(int capacity, int prefill,
            Action<Item> onDestroy = null) {
            return new ConcurrentObjectPool<Item>(capacity, prefill, () => new Item(),
                destroy: onDestroy);
        }

        [Test]
        public void Constructor_PrefillsIdleObjects() {
            var pool = CreatePool(4, 3);

            Assert.AreEqual(4, pool.Capacity);
            Assert.AreEqual(3, pool.AvailableCount);
            Assert.AreEqual(0, pool.AllocatedCount);
            Assert.AreEqual(3, pool.Count);
        }

        [Test]
        public void Constructor_RejectsInvalidArguments() {
            Assert.Throws<ArgumentNullException>(() => new ConcurrentObjectPool<Item>(4, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ConcurrentObjectPool<Item>(0, 0, () => new Item()));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ConcurrentObjectPool<Item>(4, -1, () => new Item()));
        }

        [Test]
        public void Acquire_ThenRelease_ReusesTheSameInstance() {
            var pool = CreatePool(4, 0);
            var item = pool.Acquire();

            pool.Release(item);

            Assert.AreEqual(0, pool.AllocatedCount);
            Assert.AreEqual(1, pool.AvailableCount);
            Assert.AreSame(item, pool.Acquire());
        }

        [Test]
        public void Release_AboveCapacity_DestroysTheObject() {
            var destroyed = new List<Item>();
            var pool = CreatePool(2, 0, destroyed.Add);

            var first = pool.Acquire();
            var second = pool.Acquire();
            var third = pool.Acquire();

            pool.Release(first);
            pool.Release(second);
            pool.Release(third);

            Assert.AreEqual(2, pool.AvailableCount);
            Assert.AreEqual(0, pool.AllocatedCount);
            CollectionAssert.AreEqual(new[] { third }, destroyed);
        }

        [Test]
        public void Release_UnknownOrDuplicateObject_IsIgnored() {
            var pool = CreatePool(4, 0);
            var item = pool.Acquire();

            pool.Release(item);
            pool.Release(item);
            pool.Release(new Item());

            Assert.AreEqual(1, pool.AvailableCount);
            Assert.AreEqual(0, pool.AllocatedCount);
        }

        [Test]
        public void Clear_DestroysIdleObjectsAndKeepsLeases() {
            var destroyed = new List<Item>();
            var pool = CreatePool(4, 2, destroyed.Add);
            var leased = pool.Acquire();

            pool.Clear();

            Assert.AreEqual(0, pool.AvailableCount);
            Assert.AreEqual(1, pool.AllocatedCount);
            Assert.AreEqual(1, destroyed.Count);
        }

        [Test]
        public void Clear_WhenDestroyThrows_DoesNotCorruptTheIdleCount() {
            var pool = new ConcurrentObjectPool<Item>(4, 2, () => new Item(),
                destroy: _ => throw new InvalidOperationException("boom"));

            Assert.Throws<InvalidOperationException>(() => pool.Clear());
            Assert.AreEqual(0, pool.AvailableCount);
            Assert.NotNull(pool.Acquire());
        }

        [Test]
        public void Release_WhenDeactivateThrows_DestroysInsteadOfPooling() {
            var destroyed = new List<Item>();
            var pool = new ConcurrentObjectPool<Item>(4, 0, () => new Item(),
                deactivate: _ => throw new InvalidOperationException("boom"),
                destroy: destroyed.Add);
            var item = pool.Acquire();

            Assert.Throws<InvalidOperationException>(() => pool.Release(item));
            Assert.AreEqual(1, destroyed.Count);
            Assert.AreEqual(0, pool.AvailableCount);
            Assert.AreEqual(0, pool.AllocatedCount);
        }

        [Test]
        public void ConcurrentAcquireRelease_NeverLeasesOneInstanceTwice() {
            const int workers = 4;
            const int iterations = 500;
            var pool = CreatePool(8, 0);
            var inUse = new ConcurrentDictionary<Item, byte>();
            var errors = new ConcurrentQueue<string>();
            var threads = new Thread[workers];

            for (var i = 0; i < workers; i++) {
                threads[i] = new Thread(() => {
                    for (var iteration = 0; iteration < iterations; iteration++) {
                        var item = pool.Acquire();
                        if (!inUse.TryAdd(item, 0)) {
                            errors.Enqueue("same instance leased twice");
                        }

                        Thread.SpinWait(20);
                        inUse.TryRemove(item, out _);
                        pool.Release(item);
                    }
                });
            }

            foreach (var thread in threads) {
                thread.Start();
            }

            foreach (var thread in threads) {
                thread.Join();
            }

            Assert.IsEmpty(errors);
            Assert.AreEqual(0, pool.AllocatedCount);
            Assert.LessOrEqual(pool.AvailableCount, pool.Capacity);
            Assert.AreEqual(pool.AvailableCount, pool.Count);
        }

        [Test]
        public void ConcurrentRelease_DoesNotExceedCapacity() {
            var pool = CreatePool(4, 0);
            var items = new Item[64];
            for (var i = 0; i < items.Length; i++) {
                items[i] = pool.Acquire();
            }

            var threads = new Thread[4];
            for (var threadIndex = 0; threadIndex < threads.Length; threadIndex++) {
                var start = threadIndex * 16;
                threads[threadIndex] = new Thread(() => {
                    for (var i = start; i < start + 16; i++) {
                        pool.Release(items[i]);
                    }
                });
            }

            foreach (var thread in threads) {
                thread.Start();
            }

            foreach (var thread in threads) {
                thread.Join();
            }

            Assert.LessOrEqual(pool.AvailableCount, pool.Capacity);
            Assert.AreEqual(0, pool.AllocatedCount);
        }

        [Test]
        public void ImplementsUnityPoolInterface() {
            UnityEngine.Pool.IObjectPool<Item> pool = CreatePool(4, 1);

            var item = pool.Get();
            Assert.NotNull(item);
            Assert.AreEqual(0, pool.CountInactive);

            pool.Release(item);
            Assert.AreEqual(1, pool.CountInactive);

            pool.Clear();
            Assert.AreEqual(0, pool.CountInactive);
        }

        [Test]
        public void UnityPoolInterface_GetOut_ReturnsATokenThatReleasesOnDispose() {
            var concrete = CreatePool(4, 0);
            UnityEngine.Pool.IObjectPool<Item> pool = concrete;

            Item item;
            using (pool.Get(out item)) {
                Assert.NotNull(item);
                Assert.AreEqual(1, concrete.AllocatedCount);
            }

            Assert.AreEqual(0, concrete.AllocatedCount);
            Assert.AreEqual(1, concrete.AvailableCount);
        }

        [Test]
        public void Acquire_WhenActivateThrows_RollsTheLeaseBack() {
            var pool = new ConcurrentObjectPool<Item>(2, 0, () => new Item(),
                _ => throw new InvalidOperationException("boom"));

            Assert.Throws<InvalidOperationException>(() => pool.Acquire());
            Assert.AreEqual(0, pool.AllocatedCount);
            Assert.AreEqual(1, pool.AvailableCount);
        }

        [Test]
        public void Acquire_WhenFactoryReusesALeasedInstance_Throws() {
            var shared = new Item();
            var pool = new ConcurrentObjectPool<Item>(2, 0, () => shared);

            Assert.NotNull(pool.Acquire());
            Assert.Throws<InvalidOperationException>(() => pool.Acquire());
        }
    }
}
