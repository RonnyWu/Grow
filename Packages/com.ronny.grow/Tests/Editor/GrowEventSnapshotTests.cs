// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Grow.Core.Event;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class GrowEventSnapshotTests {
        [Test]
        public void AddDuringInvoke_TakesEffectNextRound() {
            var e = new GrowEvent();
            var seen = new List<string>();
            Action added = () => seen.Add("added");
            Action existing = () => {
                seen.Add("existing");
                e.Add(added);
            };
            e.Add(existing);

            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "existing" }, seen);

            seen.Clear();
            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "existing", "added" }, seen);
        }

        [Test]
        public void RemoveDuringInvoke_StillRunsThisRoundThenStops() {
            var e = new GrowEvent();
            var seen = new List<string>();
            Action second = () => seen.Add("second");
            Action first = null;
            first = () => {
                seen.Add("first");
                e.Remove(second);
            };
            e.Add(first);
            e.Add(second);

            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "first", "second" }, seen);
            Assert.AreEqual(1, e.Count);

            seen.Clear();
            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "first" }, seen);
        }

        [Test]
        public void ClearDuringInvoke_RunsRemainingThisRoundThenEmpty() {
            var e = new GrowEvent();
            var seen = new List<string>();
            Action first = () => {
                seen.Add("first");
                ((IGrowEventRaiser)e).Clear();
            };
            Action second = () => seen.Add("second");
            e.Add(first);
            e.Add(second);

            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "first", "second" }, seen);
            Assert.AreEqual(0, e.Count);

            seen.Clear();
            GrowEventOrderingTests.Raise(e);
            Assert.AreEqual(0, seen.Count);
        }

        [Test]
        public void Invoke_EmptyEvent_IsNoOp() {
            var e = new GrowEvent();
            Assert.DoesNotThrow(() => GrowEventOrderingTests.Raise(e));
        }

        [Test]
        public void Invoke_SteadyState_ReusesSnapshotBuffer() {
            var list = new InvocationList<Action>();
            list.Add(() => { });
            Assert.IsTrue(list.BeginDispatch(out var first, out _));
            list.EndDispatch();
            Assert.IsTrue(list.BeginDispatch(out var second, out _));
            Assert.AreSame(first, second);
            list.EndDispatch();
        }

        [Test]
        public void Invoke_AfterMutationOutsideRound_RebuildsSnapshot() {
            var e = new GrowEvent();
            var count = 0;
            e.Add(() => count++);

            GrowEventOrderingTests.Raise(e);
            e.Add(() => count++);
            GrowEventOrderingTests.Raise(e);

            Assert.AreEqual(3, count);
        }
    }
}
