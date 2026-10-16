// SPDX-License-Identifier: MIT

using System;
using System.Text.RegularExpressions;
using Grow.Core.Log;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Tests.Editor.Log {
    // Mutates the process-wide log facade, so these tests must not run in parallel with each other.
    public class GrowLogTests {
        private sealed class CapturingSink : ILogSink {
            public int Count;
            public LogEntry Last;

            public void Write(in LogEntry entry) {
                Count++;
                Last = entry;
            }
        }

        private sealed class ThrowingSink : ILogSink {
            public void Write(in LogEntry entry) {
                throw new InvalidOperationException("sink boom");
            }
        }

        [SetUp]
        public void SetUp() {
            GrowLog.Reset();
        }

        [TearDown]
        public void TearDown() {
            GrowLog.Reset();
        }

        [Test]
        public void DefaultsToInfoMinimumLevel() {
            Assert.AreEqual(LogLevel.Info, GrowLog.MinimumLevel);
        }

        [Test]
        public void EntriesBelowMinimumLevelAreDropped() {
            var sink = new CapturingSink();
            GrowLog.SetSink(sink);

            GrowLog.Debug("dropped");

            Assert.AreEqual(0, sink.Count);
        }

        [Test]
        public void ALevelExactlyAtTheMinimumIsWritten() {
            var sink = new CapturingSink();
            GrowLog.SetSink(sink);
            GrowLog.MinimumLevel = LogLevel.Warning;

            GrowLog.Warning("at the boundary");

            Assert.AreEqual(1, sink.Count, "the filter is strict: equal to the minimum passes");
        }

        [Test]
        public void WritesEntryWithLevelMessageAndCategory() {
            var sink = new CapturingSink();
            GrowLog.SetSink(sink);

            GrowLog.Warning("careful", "core");

            Assert.AreEqual(1, sink.Count);
            Assert.AreEqual(LogLevel.Warning, sink.Last.Level);
            Assert.AreEqual("careful", sink.Last.Message);
            Assert.AreEqual("core", sink.Last.Category);
        }

        [Test]
        public void ConvenienceMethodsReportTheirOwnLevel() {
            var sink = new CapturingSink();
            GrowLog.SetSink(sink);
            GrowLog.MinimumLevel = LogLevel.Trace;

            GrowLog.Trace("t");
            GrowLog.Debug("d");
            GrowLog.Info("i");
            GrowLog.Warning("w");
            GrowLog.Error("e");

            Assert.AreEqual(5, sink.Count);
            Assert.AreEqual(LogLevel.Error, sink.Last.Level);
        }

        [Test]
        public void SetSinkRejectsNull() {
            Assert.Throws<System.ArgumentNullException>(() => GrowLog.SetSink(null));
        }

        [Test]
        public void ResetRestoresTheDefaultSinkAndLevel() {
            GrowLog.SetSink(new CapturingSink());
            GrowLog.MinimumLevel = LogLevel.Error;

            GrowLog.Reset();

            Assert.AreEqual(LogLevel.Info, GrowLog.MinimumLevel);
            Assert.IsInstanceOf<UnityLogSink>(GrowLog.Sink);
        }

        [Test]
        public void AThrowingSinkIsContained() {
            // The kernel logs while it transitions, so a broken sink must not be able to abort a
            // lifecycle entry.
            GrowLog.SetSink(new ThrowingSink());
            LogAssert.Expect(LogType.Exception, new Regex("sink boom"));

            Assert.DoesNotThrow(() => GrowLog.Error("still delivered to nobody"));
        }
    }
}
