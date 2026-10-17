// SPDX-License-Identifier: MIT

using System.Collections;
using Grow.Kernel.Entry;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Loop;
using Grow.Kernel.Modules;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Grow.Tests.PlayMode {
    /// <summary>
    /// Real Play Mode coverage for auto-initialization and the same-frame restart path that the
    /// EditMode suite cannot reproduce because it uses <c>DestroyImmediate</c>.
    /// </summary>
    public class GrowAppPlayModeTests {
        [UnityTest]
        public IEnumerator AutoInitialize_StartsAWorkingFrameLoop() {
            yield return null;

            Assert.IsTrue(GrowApp.IsInitialized);
            Assert.IsTrue(GrowLoop.IsInstalled);
            Assert.IsTrue(MonoManager.Instance.Valid);

            var frames = 0;
            var token = GrowLoop.Executor.Update.Subscribe(() => frames++);
            yield return null;
            token.Dispose();

            Assert.Greater(frames, 0);
        }

        [UnityTest]
        public IEnumerator ShutdownThenInitialize_SameFrame_KeepsFramesFlowing() {
            GrowApp.Shutdown();
            GrowApp.Initialize();

            var frames = 0;
            var token = GrowLoop.Executor.Update.Subscribe(() => frames++);
            yield return null;
            token.Dispose();

            Assert.IsTrue(GrowLoop.IsInstalled);
            Assert.Greater(frames, 0, "the replacement host must survive the old host's deferred OnDestroy");
            GrowApp.Shutdown();
        }
    }
}
