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
    public sealed class GrowEventExceptionTests {
        [Test]
        public void FirstListenerThrows_RestStillRun() {
            var e = new GrowEvent();
            var seen = new List<string>();
            e.Add(() => throw new InvalidOperationException("first"));
            e.Add(() => seen.Add("second"));

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "second" }, seen);
        }

        [Test]
        public void MiddleListenerThrows_RestStillRun() {
            var e = new GrowEvent();
            var seen = new List<string>();
            e.Add(() => seen.Add("first"));
            e.Add(() => throw new InvalidOperationException("middle"));
            e.Add(() => seen.Add("third"));

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "first", "third" }, seen);
        }

        [Test]
        public void LastListenerThrows_DoesNotAffectEarlierListeners() {
            var e = new GrowEvent();
            var seen = new List<string>();
            e.Add(() => seen.Add("first"));
            e.Add(() => throw new InvalidOperationException("last"));

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            Assert.DoesNotThrow(() => GrowEventOrderingTests.Raise(e));

            CollectionAssert.AreEqual(new[] { "first" }, seen);
        }

        [Test]
        public void ThrowingListener_StaysSubscribedAndRunsNextRound() {
            var e = new GrowEvent();
            var calls = 0;
            e.Add(() => {
                calls++;
                throw new InvalidOperationException("always");
            });

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);
            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);

            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, e.Count);
        }

        [Test]
        public void RepeatedThrows_EachReportedAndNeighborsSurvive() {
            var e = new GrowEvent();
            var seen = new List<string>();
            e.Add(() => throw new InvalidOperationException("one"));
            e.Add(() => throw new InvalidOperationException("two"));
            e.Add(() => seen.Add("survivor"));

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "survivor" }, seen);
        }

        [Test]
        public void FatalException_PropagatesAndSkipsRemainingListeners() {
            var e = new GrowEvent();
            var ran = false;
            e.Add(() => throw new StackOverflowException());
            e.Add(() => ran = true);

            Assert.Throws<StackOverflowException>(() => GrowEventOrderingTests.Raise(e));
            Assert.IsFalse(ran);
        }

        [Test]
        public void AfterFatalException_StateStaysUsable() {
            var e = new GrowEvent();
            var ran = false;
            e.Add(() => throw new OutOfMemoryException());
            Assert.Throws<OutOfMemoryException>(() => GrowEventOrderingTests.Raise(e));

            ((IGrowEventRaiser)e).Clear();
            e.Add(() => ran = true);
            GrowEventOrderingTests.Raise(e);
            Assert.IsTrue(ran);
        }

        [Test]
        public void ThrowingListenerWithReentrancy_StateStaysConsistent() {
            var e = new GrowEvent();
            var seen = new List<string>();
            var nested = false;
            Action second = () => seen.Add("second");
            Action first = null;
            first = () => {
                seen.Add("first");
                if (nested) throw new InvalidOperationException("nested");
                nested = true;
                GrowEventOrderingTests.Raise(e);
            };
            e.Add(first);
            e.Add(second);

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);

            Assert.AreEqual(2, e.Count);
            seen.Clear();
            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "first", "second" }, seen);
        }
    }
}
