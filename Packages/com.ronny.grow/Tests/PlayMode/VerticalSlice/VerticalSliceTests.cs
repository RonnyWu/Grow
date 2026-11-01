// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using Grow.Kernel.Entry;
using Grow.Kernel.Modules;
using Grow.Samples.VerticalSlice;
using Grow.Scenes;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Grow.Tests.PlayMode {
    /// <summary>
    /// The First Boot acceptance slice: a game assembled from public API only must reach Ready
    /// through the loading barrier and survive a restart with its saved session intact.
    /// </summary>
    /// <remarks>
    /// The slice lives in the test assembly on purpose. Unity does not import <c>Samples~</c>, so a
    /// sample there cannot be compiled or executed by CI; this copy is the executable acceptance and
    /// uses the same public surface a game would.
    /// </remarks>
    public class VerticalSliceTests {
        [UnityTest]
        public IEnumerator Slice_BootsThroughLoadingToReady() {
            ResetComposition();
            var host = SliceEntry.Start(UniqueStoreName());
            try {
                Assert.AreEqual(GrowState.Loading, host.State);
                Assert.IsFalse(host.IsReady);

                var previous = 0f;
                for (var i = 0; i < 120 && !host.IsReady; i++) {
                    Assert.GreaterOrEqual(host.Progress, previous, "progress must not regress");
                    previous = host.Progress;
                    yield return null;
                }

                Assert.IsTrue(host.IsReady, "the asynchronous barrier must finish");
                Assert.AreEqual(GrowState.Ready, host.State);
                Assert.AreEqual(1f, host.Progress, 0.0001f);
                Assert.IsTrue(host.GetRequiredService<SliceGameModule>().IsLoaded);
                Assert.AreEqual(0, host.GetRequiredService<SliceSessionModule>().Coins);
                Assert.IsNotNull(host.GetRequiredService<ISceneFlow>(),
                    "the scenes capability is composed but owns nothing until asked");
            } finally {
                host.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Slice_PersistsCoinsAcrossARestart() {
            ResetComposition();
            var storeName = UniqueStoreName();

            var first = SliceEntry.Start(storeName);
            try {
                yield return WaitUntilReady(first);

                var session = first.GetRequiredService<SliceSessionModule>();
                Assert.AreEqual(0, session.Coins);
                session.AddCoins(7);
                session.Save();
            } finally {
                first.Dispose();
            }

            GrowModules.Reset();

            var second = SliceEntry.Start(storeName);
            try {
                yield return WaitUntilReady(second);

                Assert.AreEqual(7, second.GetRequiredService<SliceSessionModule>().Coins,
                    "the session must come back from the store");
            } finally {
                second.Dispose();
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreTheDefaultComposition() {
            GrowApp.Shutdown();
            GrowModules.Reset();
            GrowApp.AutoInitialize = true;
            GrowApp.Initialize();
            yield return null;
        }

        private static void ResetComposition() {
            GrowApp.Shutdown();
            GrowModules.Reset();
        }

        private static IEnumerator WaitUntilReady(GrowHost host) {
            for (var i = 0; i < 120 && !host.IsReady; i++) {
                yield return null;
            }

            Assert.IsTrue(host.IsReady, "the boot barrier must finish");
        }

        private static string UniqueStoreName() {
            return "slice-" + Guid.NewGuid().ToString("N");
        }
    }
}
