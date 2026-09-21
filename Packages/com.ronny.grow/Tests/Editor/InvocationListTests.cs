// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Grow.Core.Event;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class InvocationListTests {
        [Test]
        public void Add_Duplicate_IsDeduplicated() {
            var list = new InvocationList<Action>();
            Action handler = Handle;
            list.Add(handler);
            list.Add(handler);
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void Add_Null_IsIgnored() {
            var list = new InvocationList<Action>();
            list.Add(null);
            Assert.AreEqual(0, list.Count);
            Assert.IsFalse(list.BeginDispatch(out _, out _));
        }

        [Test]
        public void Remove_Null_IsIgnored() {
            var list = new InvocationList<Action>();
            list.Add(Handle);
            list.Remove(null);
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void Remove_Unknown_LeavesStateUnchanged() {
            var list = new InvocationList<Action>();
            list.Add(Handle);
            list.Remove(Other);
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void RemoveThenAdd_AppendsToTail() {
            var list = new InvocationList<Action>();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            list.Add(a);
            list.Add(b);
            list.Remove(a);
            list.Add(a);

            Dispatch(list);

            CollectionAssert.AreEqual(new[] { "b", "a" }, order);
        }

        [Test]
        public void BeginDispatch_Empty_ReturnsFalseAndStaysReusable() {
            var list = new InvocationList<Action>();
            Assert.IsFalse(list.BeginDispatch(out _, out var count));
            Assert.AreEqual(0, count);
            list.EndDispatch();

            list.Add(Handle);
            Assert.IsTrue(list.BeginDispatch(out var snapshot, out var nonEmpty));
            Assert.AreEqual(1, nonEmpty);
            Assert.IsNotNull(snapshot[0]);
            list.EndDispatch();
        }

        [Test]
        public void BeginDispatch_SteadyState_ReusesSameBuffer() {
            var list = new InvocationList<Action>();
            list.Add(Handle);
            Assert.IsTrue(list.BeginDispatch(out var first, out _));
            list.EndDispatch();
            Assert.IsTrue(list.BeginDispatch(out var second, out _));
            Assert.AreSame(first, second);
            list.EndDispatch();
        }

        [Test]
        public void Dispatch_MutationDuringRound_AppliesNextRound() {
            var list = new InvocationList<Action>();
            var seen = new List<string>();
            Action added = () => seen.Add("added");
            Action first = null;
            first = () => {
                seen.Add("first");
                list.Add(added);
                list.Remove(first);
            };
            list.Add(first);

            Dispatch(list);
            CollectionAssert.AreEqual(new[] { "first" }, seen);
            Assert.AreEqual(1, list.Count);

            seen.Clear();
            Dispatch(list);
            CollectionAssert.AreEqual(new[] { "added" }, seen);
        }

        [Test]
        public void ClearDuringDispatch_RunsCurrentRoundThenEmpty() {
            var list = new InvocationList<Action>();
            var seen = new List<string>();
            Action first = () => {
                seen.Add("first");
                list.Clear();
            };
            Action second = () => seen.Add("second");
            list.Add(first);
            list.Add(second);

            Dispatch(list);

            CollectionAssert.AreEqual(new[] { "first", "second" }, seen);
            Assert.AreEqual(0, list.Count);
            Assert.IsFalse(list.BeginDispatch(out _, out _));
        }

        [Test]
        public void NestedDispatch_ReusesSnapshotAndRestartsFromHead() {
            var list = new InvocationList<Action>();
            var order = new List<string>();
            var nested = false;
            Action second = () => order.Add("second");
            Action first = null;
            first = () => {
                order.Add("first");
                if (nested) return;
                nested = true;
                Dispatch(list);
            };
            list.Add(first);
            list.Add(second);

            Dispatch(list);

            CollectionAssert.AreEqual(new[] { "first", "first", "second", "second" }, order);
        }

        private static void Dispatch(InvocationList<Action> list) {
            if (!list.BeginDispatch(out var snapshot, out var count)) return;
            try {
                for (var i = 0; i < count; i++) snapshot[i]();
            } finally {
                list.EndDispatch();
            }
        }

        private static void Handle() { }

        private static void Other() { }
    }
}
