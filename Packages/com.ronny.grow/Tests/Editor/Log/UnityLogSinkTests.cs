// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;
using Grow.Core.Log;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Tests.Editor.Log {
    public class UnityLogSinkTests {
        private static void Write(LogLevel level, string message, string category = null) {
            new UnityLogSink().Write(new LogEntry(level, message, category, default));
        }

        [Test]
        public void InfoLandsInTheConsole() {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Info\] hello$"));

            Write(LogLevel.Info, "hello");
        }

        [Test]
        public void CategoryIsPrintedWhenPresent() {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Debug\]\[kernel\] starting$"));

            Write(LogLevel.Debug, "starting", "kernel");
        }

        [Test]
        public void WarningBecomesALogWarning() {
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Warning\] careful$"));

            Write(LogLevel.Warning, "careful");
        }

        [Test]
        public void ErrorBecomesALogError() {
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Error\] broken$"));

            Write(LogLevel.Error, "broken");
        }

        [Test]
        public void AnUnmappedLevelIsReportedRatherThanDowngraded() {
            // A level added to LogLevel without teaching the sink about it must be loud, not
            // silently printed as an ordinary log.
            LogAssert.Expect(LogType.Error, new Regex("Unmapped"));

            Write((LogLevel)42, "odd");
        }
    }
}
