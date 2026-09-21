// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Grow.Core.Event;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Tests {
    [TestFixture]
    public sealed class GrowEventGenericTests {
        [Test]
        public void Invoke_PassesArgumentToEveryListener() {
            var e = new GrowEvent<string>();
            var seen = new List<string>();
            e.Add(v => seen.Add("a:" + v));
            e.Add(v => seen.Add("b:" + v));

            ((IGrowEventRaiser<string>)e).Invoke("x");

            CollectionAssert.AreEqual(new[] { "a:x", "b:x" }, seen);
        }

        [Test]
        public void Invoke_RunsListenersInRegistrationOrder() {
            var e = new GrowEvent<int>();
            var order = new List<int>();
            e.Add(v => order.Add(v + 1));
            e.Add(v => order.Add(v + 2));
            e.Add(v => order.Add(v + 3));

            ((IGrowEventRaiser<int>)e).Invoke(10);

            CollectionAssert.AreEqual(new[] { 11, 12, 13 }, order);
        }

        [Test]
        public void Invoke_MutationDuringRound_AppliesNextRound() {
            var e = new GrowEvent<int>();
            var seen = new List<int>();
            Action<int> added = v => seen.Add(v * 100);
            Action<int> first = v => {
                seen.Add(v);
                e.Add(added);
            };
            e.Add(first);

            ((IGrowEventRaiser<int>)e).Invoke(1);
            CollectionAssert.AreEqual(new[] { 1 }, seen);

            seen.Clear();
            ((IGrowEventRaiser<int>)e).Invoke(2);
            CollectionAssert.AreEqual(new[] { 2, 200 }, seen);
        }

        [Test]
        public void ListenerThrows_OthersStillRunAndErrorReported() {
            var e = new GrowEvent<int>();
            var seen = new List<int>();
            e.Add(v => throw new InvalidOperationException("bad"));
            e.Add(v => seen.Add(v));

            LogAssert.Expect(LogType.Error, new Regex("GrowEventGenericTests"));
            ((IGrowEventRaiser<int>)e).Invoke(7);

            CollectionAssert.AreEqual(new[] { 7 }, seen);
        }

        [Test]
        public void Clear_EmptiesEventAndLeavesItReusable() {
            var e = new GrowEvent<int>();
            e.Add(v => { });
            ((IGrowEventRaiser<int>)e).Clear();

            Assert.AreEqual(0, e.Count);
            var ran = false;
            e.Add(v => ran = true);
            ((IGrowEventRaiser<int>)e).Invoke(1);
            Assert.IsTrue(ran);
        }

        [Test]
        public void Raiser_IsContravariant() {
            var e = new GrowEvent<object>();
            IGrowEventRaiser<string> raiser = e;
            var seen = new List<object>();
            e.Add(v => seen.Add(v));

            raiser.Invoke("contravariant");

            CollectionAssert.AreEqual(new object[] { "contravariant" }, seen);
        }
    }
}
