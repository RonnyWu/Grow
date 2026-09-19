// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    [Category("Grow.Collections.GC")]
    public sealed class CollectionsAllocationTests {
        [Test]
        public void OrderedSet_Enumeration_SteadyState_AllocatesLittle() {
            var set = new OrderedSet<int>(64);
            for (var i = 0; i < 64; i++) set.Add(i);
            for (var warmup = 0; warmup < 64; warmup++) {
                var ignored = 0;
                foreach (var item in set) ignored += item;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalMemory(false);
            var sum = 0;
            for (var round = 0; round < 4096; round++) {
                foreach (var item in set) sum += item;
            }
            var after = GC.GetTotalMemory(false);

            Assert.Greater(sum, 0);
            Assert.Less(after - before, 4096, "steady-state enumeration must allocate ~0 bytes");
        }

        [Test]
        public void SnapshotSet_Read_SteadyState_AllocatesLittle() {
            var set = new SnapshotSet<int>(64);
            for (var i = 0; i < 64; i++) set.Add(i);
            for (var warmup = 0; warmup < 64; warmup++) {
                if (!set.BeginRead(out var items, out var count)) continue;
                var ignored = 0;
                for (var i = 0; i < count; i++) ignored += items[i];
                set.EndRead();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalMemory(false);
            var sum = 0;
            for (var round = 0; round < 4096; round++) {
                if (!set.BeginRead(out var items, out var count)) continue;
                for (var i = 0; i < count; i++) sum += items[i];
                set.EndRead();
            }
            var after = GC.GetTotalMemory(false);

            Assert.Greater(sum, 0);
            Assert.Less(after - before, 4096, "steady-state read must allocate ~0 bytes");
        }
    }
}
