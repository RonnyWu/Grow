// SPDX-License-Identifier: MIT

using System.Collections;
using Grow.Kernel.Entry;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Grow.Tests.Editor.Entry {
    /// <summary>
    /// Cross-session check for the static lifecycle event while Domain Reload is disabled.
    /// The test assembly is Editor-only, so <see cref="EnterPlayMode"/> can drive real Play sessions.
    /// </summary>
    public class GrowRuntimeDomainReloadTests {
        private static int s_calls;

        private static void OnStateChanged(StateChange change) {
            s_calls++;
        }

        [UnityTest]
        public IEnumerator StaticEvent_DoesNotSurvivePlayModeSessions() {
            s_calls = 0;
            var eventRef = GrowBoot.StateChanged;
            var first = GrowBoot.StateChanged.Subscribe(OnStateChanged);
            GrowBoot.Reset();
            GrowBoot.Install();
            GrowBoot.Boot();
            GrowBoot.Shutdown();
            Assert.Greater(s_calls, 0, "the first session must observe state changes");
            Assert.IsTrue(first.IsActive);

            yield return new EnterPlayMode(false);

            Assert.IsTrue(GrowApp.IsInitialized, "the composition root auto-initializes at BeforeSceneLoad");
            Assert.AreEqual(GrowState.Ready, GrowBoot.State, "the auto-initialized composition reaches Ready");
            Assert.IsFalse(first.IsActive, "the previous session's subscription must be cleared");
            Assert.AreSame(eventRef, GrowBoot.StateChanged, "the static event instance must stay stable across Clear");

            GrowApp.Shutdown();

            s_calls = 0;
            var second = GrowBoot.StateChanged.Subscribe(OnStateChanged);
            GrowBoot.Reset();
            GrowBoot.Install();
            GrowBoot.Boot();
            GrowBoot.Shutdown();
            Assert.Greater(s_calls, 0, "the second session must observe its own state changes");

            s_calls = 0;
            yield return new ExitPlayMode();

            yield return new EnterPlayMode(false);

            Assert.IsTrue(GrowApp.IsInitialized, "the next play session auto-initializes again");
            Assert.AreEqual(GrowState.Ready, GrowBoot.State, "the auto-initialized composition reaches Ready");
            Assert.IsFalse(second.IsActive, "the previous session's subscription must be cleared");

            GrowApp.Shutdown();
            GrowBoot.Reset();
            GrowBoot.Install();
            GrowBoot.Boot();
            GrowBoot.Shutdown();
            Assert.AreEqual(0, s_calls, "the previous session's static subscription must not survive");

            yield return new ExitPlayMode();
        }
    }
}
