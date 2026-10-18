// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using Grow.Core.Log;
using Grow.Kernel.Entry;
using Grow.Kernel.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Grow.Tests.Editor.Loop {
    public class GrowLoopTests {
        [SetUp]
        public void SetUp() {
            GrowLog.Reset();
            GrowLoop.ResetStaticState();
            GrowRuntime.ResetStaticState();
            PlayerLoopManager.ResetStaticState();
        }

        [TearDown]
        public void TearDown() {
            GrowLoop.Uninstall();
            GrowLoop.ResetStaticState();
            GrowRuntime.ResetStaticState();
            PlayerLoopManager.ResetStaticState();
            GrowLog.Reset();
        }

        [Test]
        public void Install_CreatesHostAndInstallsProfile() {
            GrowLoop.Install();

            Assert.IsTrue(GrowLoop.IsInstalled);
            Assert.IsNotNull(GrowLoop.Executor);
            Assert.IsNotNull(GrowLoop.Host);
            Assert.AreEqual("Grow", GrowLoop.Host.gameObject.name);
            Assert.IsTrue(PlayerLoopProfile.Installed);
            Assert.IsTrue(PlayerLoopManager.IsModified);
        }

        [Test]
        public void Install_RebindAfterUninstall_BindsCallbacksToTheNewHost() {
            GrowLoop.Install();
            var oldHost = GrowLoop.Host;
            GrowLoop.Uninstall();
            GrowLoop.Install();

            Assert.IsTrue(GrowLoop.IsInstalled);
            Assert.IsTrue(PlayerLoopProfile.Installed);
            Assert.AreNotSame(oldHost, GrowLoop.Host);
        }

        [Test]
        public void PlayerLoopProfile_StaleOwner_DoesNotUninstallReplacement() {
            var oldRoot = new GameObject("old");
            var newRoot = new GameObject("new");
            var oldExecutor = oldRoot.AddComponent<MonoExecutor>();
            var newExecutor = newRoot.AddComponent<MonoExecutor>();

            try {
                Assert.IsTrue(PlayerLoopProfile.Install(oldExecutor));
                Assert.IsTrue(PlayerLoopProfile.Install(newExecutor));
                Assert.IsTrue(PlayerLoopProfile.Installed);

                PlayerLoopProfile.Uninstall(oldExecutor);
                Assert.IsTrue(PlayerLoopProfile.Installed,
                    "a stale host's OnDestroy must not tear down the replacement profile");

                PlayerLoopProfile.Uninstall(newExecutor);
                Assert.IsFalse(PlayerLoopProfile.Installed);
            } finally {
                PlayerLoopProfile.Uninstall();
                UnityEngine.Object.DestroyImmediate(oldRoot);
                UnityEngine.Object.DestroyImmediate(newRoot);
            }
        }

        [Test]
        public void PlayerLoopProfile_UninstallWithoutOwner_RemovesPartialNodes() {
            var root = new GameObject("partial");
            var executor = root.AddComponent<MonoExecutor>();

            try {
                Assert.IsTrue(PlayerLoopProfile.Install(executor));
                Assert.IsTrue(PlayerLoopManager.IsModified);

                PlayerLoopProfile.ResetStaticState();
                Assert.IsFalse(PlayerLoopProfile.Installed);
                Assert.IsTrue(PlayerLoopManager.IsModified,
                    "the nodes are still in the loop after the owner was lost");

                PlayerLoopProfile.Uninstall();

                Assert.IsFalse(PlayerLoopManager.IsModified,
                    "cleanup must remove managed nodes even when the owner was lost");
            } finally {
                PlayerLoopProfile.Uninstall();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Install_IsIdempotent() {
            GrowLoop.Install();
            var host = GrowLoop.Host;

            GrowLoop.Install();

            Assert.AreSame(host, GrowLoop.Host);
            Assert.IsTrue(GrowLoop.IsInstalled);
        }

        [Test]
        public void Uninstall_RemovesHostAndProfile() {
            GrowLoop.Install();

            GrowLoop.Uninstall();

            Assert.IsFalse(GrowLoop.IsInstalled);
            Assert.IsNull(GrowLoop.Host);
            Assert.IsFalse(PlayerLoopProfile.Installed);
            Assert.IsFalse(PlayerLoopManager.IsModified);
        }

        [Test]
        public void PhaseEvents_RaiseTheirHandlers() {
            GrowLoop.Install();
            var pre = 0;
            var fixedUpdate = 0;
            var update = 0;
            var post = 0;
            var late = 0;
            _ = GrowLoop.Executor.PreUpdate.Subscribe(() => pre++);
            _ = GrowLoop.Executor.FixedUpdate.Subscribe(() => fixedUpdate++);
            _ = GrowLoop.Executor.Update.Subscribe(() => update++);
            _ = GrowLoop.Executor.PostUpdate.Subscribe(() => post++);
            _ = GrowLoop.Executor.LateUpdate.Subscribe(() => late++);

            GrowLoop.Host.RaisePreUpdate();
            GrowLoop.Host.RaiseFixedUpdate();
            GrowLoop.Host.RaiseUpdate();
            GrowLoop.Host.RaisePostUpdate();
            GrowLoop.Host.RaiseLateUpdate();

            Assert.AreEqual(1, pre);
            Assert.AreEqual(1, fixedUpdate);
            Assert.AreEqual(1, update);
            Assert.AreEqual(1, post);
            Assert.AreEqual(1, late);
        }

        [Test]
        public void AppEvents_RaiseTheirHandlers() {
            GrowLoop.Install();
            var focus = false;
            var pause = false;
            var quit = 0;
            _ = GrowLoop.Executor.AppFocus.Subscribe(value => focus = value);
            _ = GrowLoop.Executor.AppPause.Subscribe(value => pause = value);
            _ = GrowLoop.Executor.AppQuit.Subscribe(() => quit++);

            GrowLoop.Host.RaiseAppFocus(true);
            GrowLoop.Host.RaiseAppPause(true);
            GrowLoop.Host.QuitForPlayer();

            Assert.IsTrue(focus);
            Assert.IsTrue(pause);
            Assert.AreEqual(1, quit);
        }

        [Test]
        public void QuitForPlayer_WithoutCompositionRoot_OnlyUninstallsTheLoop() {
            GrowRuntime.Reset();
            GrowRuntime.Install();
            GrowRuntime.Boot();
            GrowLoop.Install();

            GrowLoop.Host.QuitForPlayer();

            Assert.AreEqual(GrowState.Ready, GrowBoot.State,
                "the loop must not drive the Entry layer on its own");
            Assert.IsFalse(GrowLoop.IsInstalled);
            Assert.IsFalse(PlayerLoopProfile.Installed);

            GrowRuntime.Shutdown();
        }

        [Test]
        public void StartCoroutine_RequiresInstall() {
            Assert.Throws<InvalidOperationException>(() => GrowLoop.StartCoroutine(Noop()));
        }

        [Test]
        public void ResetStaticState_RemovesHostAndProfile() {
            GrowLoop.Install();

            GrowLoop.ResetStaticState();

            Assert.IsFalse(GrowLoop.IsInstalled);
            Assert.IsNull(GrowLoop.Host);
            Assert.IsFalse(PlayerLoopProfile.Installed);
            Assert.IsFalse(PlayerLoopManager.IsModified);
        }

        private static IEnumerator Noop() {
            yield break;
        }
    }
}
