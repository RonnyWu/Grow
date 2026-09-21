// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Grow.Core.Event;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class GrowEventOrderingTests {
        [Test]
        public void Invoke_RunsListenersInRegistrationOrder() {
            var e = new GrowEvent();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            Action c = () => order.Add("c");
            e.Add(a);
            e.Add(b);
            e.Add(c);

            Raise(e);

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, order);
        }

        [Test]
        public void Remove_FirstMiddleLast_KeepsSurvivorOrder() {
            var e = new GrowEvent();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            Action c = () => order.Add("c");
            Action d = () => order.Add("d");
            e.Add(a);
            e.Add(b);
            e.Add(c);
            e.Add(d);

            e.Remove(a);
            e.Remove(c);

            Raise(e);

            CollectionAssert.AreEqual(new[] { "b", "d" }, order);
            Assert.AreEqual(2, e.Count);
        }

        [Test]
        public void RemoveThenAdd_MovesHandlerToTail() {
            var e = new GrowEvent();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            e.Add(a);
            e.Add(b);
            e.Remove(a);
            e.Add(a);

            Raise(e);

            CollectionAssert.AreEqual(new[] { "b", "a" }, order);
        }

        [Test]
        public void Add_Duplicate_RunsOnceAndKeepFirstPosition() {
            var e = new GrowEvent();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            e.Add(a);
            e.Add(b);
            e.Add(a);

            Raise(e);

            Assert.AreEqual(2, e.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, order);
        }

        [Test]
        public void Add_Null_IsIgnored() {
            var e = new GrowEvent();
            Assert.DoesNotThrow(() => e.Add(null));
            Assert.AreEqual(0, e.Count);
        }

        [Test]
        public void Remove_Unknown_IsIgnored() {
            var e = new GrowEvent();
            e.Add(OnB);
            Assert.DoesNotThrow(() => e.Remove(OnA));
            Assert.AreEqual(1, e.Count);
        }

        [Test]
        public void InterleavedAddRemove_KeepsInsertionOrderOfSurvivors() {
            var e = new GrowEvent();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            Action c = () => order.Add("c");
            Action d = () => order.Add("d");
            e.Add(a);
            e.Add(b);
            e.Remove(a);
            e.Add(c);
            e.Remove(b);
            e.Add(d);

            Raise(e);

            CollectionAssert.AreEqual(new[] { "c", "d" }, order);
        }

        internal static void Raise(GrowEvent e) => ((IGrowEventRaiser)e).Invoke();

        private static void OnA() { }

        private static void OnB() { }
    }
}
