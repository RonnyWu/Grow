// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Text.RegularExpressions;
using Grow.Core.Event;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Tests {
    [TestFixture]
    public sealed class EventFaultsTests {
        [Test]
        public void IsFatal_OrdinaryExceptions_ReturnsFalse() {
            Assert.IsFalse(EventFaults.IsFatal(new InvalidOperationException("x")));
            Assert.IsFalse(EventFaults.IsFatal(new ArgumentException("x")));
            Assert.IsFalse(EventFaults.IsFatal(new Exception("x")));
        }

        [Test]
        public void IsFatal_FatalExceptions_ReturnsTrue() {
            Assert.IsTrue(EventFaults.IsFatal(new OutOfMemoryException()));
            Assert.IsTrue(EventFaults.IsFatal(new StackOverflowException()));
            Assert.IsTrue(EventFaults.IsFatal(new AccessViolationException()));
            Assert.IsTrue(EventFaults.IsFatal(new AppDomainUnloadedException()));
            Assert.IsTrue(EventFaults.IsFatal(new BadImageFormatException()));
            Assert.IsTrue(EventFaults.IsFatal(new InvalidProgramException()));
            Assert.IsTrue(EventFaults.IsFatal(new ExitGUIException()));
        }

        [Test]
        public void Report_LogsSingleErrorWithListenerIdentity() {
            Action handler = OnEvent;
            LogAssert.Expect(LogType.Error, new Regex("EventFaultsTests\\.OnEvent"));
            EventFaults.Report(handler, new InvalidOperationException("boom"));
        }

        [Test]
        public void Report_NullHandler_DoesNotThrow() {
            LogAssert.Expect(LogType.Error, new Regex("<unknown>"));
            Assert.DoesNotThrow(() => EventFaults.Report(null, new Exception("boom")));
        }

        private static void OnEvent() { }
    }
}
