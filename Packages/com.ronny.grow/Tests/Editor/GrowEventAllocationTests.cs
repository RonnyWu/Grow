// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using Grow.Core.Event;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    [Category("Grow.Events.GC")]
    public sealed class GrowEventAllocationTests {
        [Test]
        public void SteadyState_Invoke_AllocatesLittle() {
            var e = new GrowEvent(64);
            var sink = 0;
            for (var i = 0; i < 64; i++) {
                var value = i;
                e.Add(() => sink += value);
            }
            for (var warmup = 0; warmup < 64; warmup++) GrowEventOrderingTests.Raise(e);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var sinkBefore = sink;
            var before = GC.GetTotalMemory(false);
            for (var round = 0; round < 4096; round++) GrowEventOrderingTests.Raise(e);
            var after = GC.GetTotalMemory(false);

            Assert.Greater(sink - sinkBefore, 0);
            Assert.Less(after - before, 4096, "steady-state invoke must allocate ~0 bytes");
        }

        [Test]
        public void MutationFrame_Invoke_AllocatesBounded() {
            var e = new GrowEvent(64);
            var sink = 0;
            Action stable = () => sink++;
            for (var warmup = 0; warmup < 64; warmup++) {
                GrowEventOrderingTests.Raise(e);
                e.Add(stable);
                e.Remove(stable);
            }
            e.Add(stable);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalMemory(false);
            for (var round = 0; round < 4096; round++) {
                e.Add(stable);
                e.Remove(stable);
                e.Add(stable);
                GrowEventOrderingTests.Raise(e);
            }
            var after = GC.GetTotalMemory(false);

            Assert.Greater(sink, 0);
            Assert.Less(after - before, 32768, "mutation-frame invoke must allocate bounded bytes");
        }
    }
}
