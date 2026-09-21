// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Grow.Core.Event;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class GrowEventReentrancyTests {
        [Test]
        public void NestedInvoke_RestartsFromHeadWithinSameSnapshot() {
            var e = new GrowEvent();
            var order = new List<string>();
            var nested = false;
            Action second = () => order.Add("second");
            Action first = null;
            first = () => {
                order.Add("first");
                if (nested) return;
                nested = true;
                GrowEventOrderingTests.Raise(e);
            };
            e.Add(first);
            e.Add(second);

            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "first", "first", "second", "second" }, order);
        }

        [Test]
        public void NestedInvoke_MutationAppliesNextOutermostRound() {
            var e = new GrowEvent();
            var seen = new List<string>();
            var nested = false;
            Action added = () => seen.Add("added");
            Action first = null;
            first = () => {
                seen.Add("first");
                e.Add(added);
                if (nested) return;
                nested = true;
                GrowEventOrderingTests.Raise(e);
            };
            e.Add(first);

            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "first", "first" }, seen);

            seen.Clear();
            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "first", "added" }, seen);
        }

        [Test]
        public void ClearAfterNestedInvoke_NextRoundIsEmpty() {
            var e = new GrowEvent();
            var nested = false;
            var calls = 0;
            Action first = null;
            first = () => {
                calls++;
                if (nested) return;
                nested = true;
                GrowEventOrderingTests.Raise(e);
            };
            e.Add(first);

            GrowEventOrderingTests.Raise(e);
            Assert.AreEqual(2, calls);

            ((IGrowEventRaiser)e).Clear();
            calls = 0;
            GrowEventOrderingTests.Raise(e);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void ClearDuringNestedInvoke_StillRunsOuterSnapshot() {
            var e = new GrowEvent();
            var seen = new List<string>();
            var nested = false;
            Action second = () => seen.Add("second");
            Action first = null;
            first = () => {
                seen.Add("first");
                if (nested) return;
                nested = true;
                ((IGrowEventRaiser)e).Clear();
                GrowEventOrderingTests.Raise(e);
            };
            e.Add(first);
            e.Add(second);

            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "first", "first", "second", "second" }, seen);
            Assert.AreEqual(0, e.Count);
        }
    }
}
