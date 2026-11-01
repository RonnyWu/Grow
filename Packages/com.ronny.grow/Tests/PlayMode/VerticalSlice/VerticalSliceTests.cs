// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using Grow.Kernel.Entry;
using Grow.Kernel.Modules;
using Grow.Samples.VerticalSlice;
using Grow.Scenes;
using Grow.Store;
using Grow.Store.Contracts;
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
        private readonly List<string> _storeNames = new List<string>();

        [UnityTest]
        public IEnumerator Slice_BootsThroughLoadingToReady() {
            ResetComposition();
            var host = SliceEntry.Start(NewStoreName());
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
            var storeName = NewStoreName();

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
            DeleteStores();
            GrowModules.Reset();
            GrowApp.AutoInitialize = true;
            GrowApp.Initialize();
            yield return null;
        }

        /// <summary>Removes the stores this test opened; the names are unique per run.</summary>
        private void DeleteStores() {
            if (_storeNames.Count == 0) {
                return;
            }

            GrowModules.Reset();
            GrowModules.Register(new StoreModule());
            GrowModules.Bootstrap();

            var provider = GrowModules.GetRequiredService<IStoreProvider>();
            for (var i = 0; i < _storeNames.Count; i++) {
                provider.DeleteStore(_storeNames[i], StoreType.Runtime);
            }

            _storeNames.Clear();
            GrowModules.Reset();
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

        private string NewStoreName() {
            var name = "slice-" + Guid.NewGuid().ToString("N");
            _storeNames.Add(name);
            return name;
        }
    }
}
