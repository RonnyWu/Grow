// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Grow.Core.Log;
using Grow.Kernel.Entry;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Tests.Editor.Entry {
    // Drives the process-wide lifecycle machine, so these tests must not run in parallel with each
    // other. Unity runs EditMode tests serially. (NUnit's [NonParallelizable] is not available in
    // the version Unity 2022.3 ships.)
    public class GrowRuntimeTests {
        private string _transitions = string.Empty;

        [SetUp]
        public void SetUp() {
            // Give every test a process-fresh machine — the same invalidation the engine performs
            // between play sessions — and then start a cycle, so tests begin at GrowState.Reset.
            GrowRuntime.ResetStaticState();
            GrowLog.Reset();
            GrowRuntime.Reset();

            _transitions = string.Empty;
            GrowRuntime.StateChanged += OnStateChanged;
        }

        [TearDown]
        public void TearDown() {
            // Leave the machine neutral: the state is process-wide, so a test must not decide where
            // the next one starts.
            GrowRuntime.ResetStaticState();
        }

        private void OnStateChanged(GrowState from, GrowState to) {
            _transitions += $"{from}>{to};";
        }

        [Test]
        public void Install_AdvancesToInstalled() {
            GrowRuntime.Install();

            Assert.AreEqual(GrowState.Installed, GrowRuntime.State);
            Assert.AreEqual("Reset>Installed;", _transitions);
        }

        [Test]
        public void Boot_WithoutInstall_ThrowsAVisibleError() {
            var error = Assert.Throws<InvalidOperationException>(() => GrowRuntime.Boot());

            StringAssert.Contains("Install", error.Message);
            Assert.AreEqual(GrowState.Reset, GrowRuntime.State);
        }

        [Test]
        public void Install_BeforeReset_Throws() {
            GrowRuntime.ResetStaticState();

            var error = Assert.Throws<InvalidOperationException>(() => GrowRuntime.Install());

            StringAssert.Contains("Reset", error.Message);
            Assert.AreEqual(GrowState.None, GrowRuntime.State);
        }

        [Test]
        public void Boot_ReachesReady() {
            GrowRuntime.Install();
            GrowRuntime.Boot();

            Assert.AreEqual(GrowState.Ready, GrowRuntime.State);
            Assert.IsTrue(GrowRuntime.IsReady);
        }

        [Test]
        public void Boot_ReportsEveryTransitionInOrder() {
            GrowRuntime.Install();
            GrowRuntime.Boot();

            Assert.AreEqual("Reset>Installed;Installed>Booted;Booted>Ready;", _transitions);
        }

        [Test]
        public void TransitionsAreVisibleAtTheDefaultLevel() {
            // The lifecycle's own log is Info, so the default filter lets it through; it used to be
            // Debug, which the default filter silently dropped.
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Info\]\[Kernel\] Reset -> Installed$"));

            GrowRuntime.Install();
        }

        [Test]
        public void Install_IsIdempotent() {
            GrowRuntime.Install();
            _transitions = string.Empty;

            GrowRuntime.Install();

            Assert.AreEqual(GrowState.Installed, GrowRuntime.State);
            Assert.AreEqual(string.Empty, _transitions);
        }

        [Test]
        public void Boot_IsIdempotent() {
            GrowRuntime.Install();
            GrowRuntime.Boot();
            _transitions = string.Empty;

            GrowRuntime.Boot();

            Assert.AreEqual(GrowState.Ready, GrowRuntime.State);
            Assert.AreEqual(string.Empty, _transitions);
        }

        [Test]
        public void Reset_DoesNotMoveBackwardsOnceStarted() {
            GrowRuntime.Install();

            GrowRuntime.Reset();

            Assert.AreEqual(GrowState.Installed, GrowRuntime.State);
        }

        [Test]
        public void IsReady_IsFalseBeforeBootAndAfterShutdown() {
            Assert.IsFalse(GrowRuntime.IsReady);

            GrowRuntime.Install();
            Assert.IsFalse(GrowRuntime.IsReady);

            GrowRuntime.Boot();
            Assert.IsTrue(GrowRuntime.IsReady);

            GrowRuntime.Shutdown();
            Assert.IsFalse(GrowRuntime.IsReady);
        }

        [Test]
        public void Shutdown_EndsTheCycleAndIsIdempotent() {
            GrowRuntime.Install();
            GrowRuntime.Boot();

            GrowRuntime.Shutdown();
            Assert.AreEqual(GrowState.Shutdown, GrowRuntime.State);

            GrowRuntime.Shutdown();
            Assert.AreEqual(GrowState.Shutdown, GrowRuntime.State);
        }

        [Test]
        public void Shutdown_FromInstalledAlsoEndsTheCycle() {
            GrowRuntime.Install();

            GrowRuntime.Shutdown();

            Assert.AreEqual(GrowState.Shutdown, GrowRuntime.State);
        }

        [Test]
        public void Shutdown_BeforeAnyCycle_DoesNothing() {
            GrowRuntime.ResetStaticState();

            GrowRuntime.Shutdown();

            Assert.AreEqual(GrowState.None, GrowRuntime.State);
        }

        [Test]
        public void Install_AfterShutdown_Throws() {
            GrowRuntime.Shutdown();

            Assert.Throws<InvalidOperationException>(() => GrowRuntime.Install());
        }

        [Test]
        public void Boot_AfterShutdown_ThrowsAndPointsAtReset() {
            GrowRuntime.Install();
            GrowRuntime.Boot();
            GrowRuntime.Shutdown();

            var error = Assert.Throws<InvalidOperationException>(() => GrowRuntime.Boot());

            StringAssert.Contains("Reset", error.Message);
        }

        [Test]
        public void Reset_AfterShutdown_StartsANewCycle() {
            GrowRuntime.Install();
            GrowRuntime.Boot();
            GrowRuntime.Shutdown();

            GrowRuntime.Reset();
            Assert.AreEqual(GrowState.Reset, GrowRuntime.State);

            GrowRuntime.Install();
            GrowRuntime.Boot();
            Assert.AreEqual(GrowState.Ready, GrowRuntime.State);
        }

        [Test]
        public void ResetStaticState_ClearsStateAndDropsHandlers() {
            var staleHandlerCalls = 0;
            GrowRuntime.Install();
            GrowRuntime.Boot();
            GrowRuntime.StateChanged += (_, __) => staleHandlerCalls++;

            // What the engine's SubsystemRegistration hook does at the start of the next session.
            GrowRuntime.ResetStaticState();

            Assert.AreEqual(GrowState.None, GrowRuntime.State, "a new session must not inherit Ready");
            Assert.IsFalse(GrowRuntime.IsReady);

            GrowRuntime.Reset();
            GrowRuntime.Install();

            Assert.AreEqual(0, staleHandlerCalls, "handlers from the previous session must be gone");
        }

        [Test]
        public void ThrowingHandlerIsIsolatedAndReported() {
            var laterHandlerRan = false;
            GrowRuntime.StateChanged += (_, __) => throw new InvalidOperationException("handler boom");
            GrowRuntime.StateChanged += (_, __) => laterHandlerRan = true;

            LogAssert.Expect(LogType.Error, new Regex("handler boom"));

            GrowRuntime.Install();

            Assert.AreEqual(GrowState.Installed, GrowRuntime.State);
            Assert.IsTrue(laterHandlerRan, "a throwing handler must not silence the ones after it");
        }

        [Test]
        public void GrowBoot_ForwardsToTheRuntime() {
            GrowBoot.Install();
            GrowBoot.Boot();

            Assert.AreEqual(GrowRuntime.State, GrowBoot.State);
            Assert.AreEqual(GrowState.Ready, GrowBoot.State);
            Assert.IsTrue(GrowBoot.IsReady);

            GrowBoot.Shutdown();
            Assert.IsFalse(GrowBoot.IsReady);
        }

        [Test]
        public void GrowBoot_ForwardsStateChanged() {
            var seen = new List<string>();
            Action<GrowState, GrowState> handler = (from, to) => seen.Add($"{from}>{to}");

            GrowBoot.StateChanged += handler;
            try {
                GrowBoot.Install();
            } finally {
                GrowBoot.StateChanged -= handler;
            }

            CollectionAssert.AreEqual(new[] { "Reset>Installed" }, seen);
        }
    }
}
