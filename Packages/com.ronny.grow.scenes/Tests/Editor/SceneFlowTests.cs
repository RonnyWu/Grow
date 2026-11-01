// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Log;
using Grow.Kernel.Entry;
using Grow.Kernel.Modules;
using Grow.Scenes.Impl;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace Grow.Scenes.Tests.Editor {
    public class SceneFlowTests {
        private FakeBackend _backend;
        private SceneFlow _flow;

        [SetUp]
        public void SetUp() {
            GrowLog.SetSink(new ScenesNullLogSink());
            GrowModules.Reset();
            _backend = new FakeBackend();
            _flow = new SceneFlow(_backend);
        }

        [TearDown]
        public void TearDown() {
            _flow.Stop();
            GrowModules.Reset();
            GrowLog.Reset();
        }

        [Test]
        public void Load_StartsTheOperationAndReportsProgress() {
            _backend.Handle.Progress = 0.25f;

            var request = _flow.Load("Menu");

            Assert.IsTrue(request.IsValid);
            Assert.AreEqual("Menu", request.Name);
            Assert.IsFalse(request.IsDone);
            Assert.IsTrue(_flow.IsBusy);
            Assert.AreEqual(0.25f, request.Progress, 0.0001f);
            CollectionAssert.AreEqual(new[] { "load:Menu:Single" }, _backend.Calls);
        }

        [Test]
        public void LoadAdditive_UsesTheAdditiveMode() {
            _flow.LoadAdditive("Hud");

            CollectionAssert.AreEqual(new[] { "load:Hud:Additive" }, _backend.Calls);
        }

        [Test]
        public void Load_WhileBusy_Throws() {
            _flow.Load("Menu");

            var error = Assert.Throws<InvalidOperationException>(() => _flow.Load("Gameplay"));

            StringAssert.Contains("one foreground load", error.Message);
            Assert.AreEqual(1, _backend.Calls.Count);
        }

        [Test]
        public void Load_AfterCompletion_IsAllowed() {
            var first = _flow.Load("Menu");
            CompleteLoad(first);

            Assert.DoesNotThrow(() => _flow.Load("Gameplay"));
        }

        [Test]
        public void Load_BackendFailure_ReturnsAFaultedRequest() {
            _backend.LoadFault = new InvalidOperationException("scene is not in the build settings");

            var request = _flow.Load("Missing");

            Assert.IsFalse(request.IsDone);
            Assert.IsTrue(request.IsFaulted);
            StringAssert.Contains("build settings", request.Error);
            Assert.IsFalse(_flow.IsBusy);
        }

        [Test]
        public void Load_RejectsAnEmptyName() {
            Assert.Throws<ArgumentNullException>(() => _flow.Load(" "));
        }

        [Test]
        public void Step_CompletesOnceTheHandleIsDone() {
            var request = _flow.Load("Menu");

            _flow.Step();
            Assert.IsFalse(request.IsDone);
            Assert.IsTrue(_flow.IsBusy);

            CompleteLoad(request);

            Assert.IsTrue(request.IsDone);
            Assert.IsFalse(request.IsFaulted);
            Assert.IsFalse(_flow.IsBusy);
        }

        [Test]
        public void SetActive_IsFalseWhileLoadingAndTrueAfterCompletion() {
            var request = _flow.Load("Hud", LoadSceneMode.Additive);

            Assert.IsFalse(_flow.SetActive(request));

            CompleteLoad(request);

            Assert.IsTrue(_flow.SetActive(request));
            Assert.AreEqual("Hud", _backend.Active);
        }

        [Test]
        public void SetActive_RejectsNoneAndUnloads() {
            Assert.IsFalse(_flow.SetActive(SceneRequest.None));

            var request = _flow.Load("Menu");
            CompleteLoad(request);
            var unload = _flow.Unload(request);

            Assert.IsFalse(_flow.SetActive(unload));
        }

        [Test]
        public void Unload_OfAFinishedLoad_StartsAnUnload() {
            var request = _flow.Load("Menu");
            CompleteLoad(request);
            _backend.Handle = new FakeHandle();

            var unload = _flow.Unload(request);

            Assert.IsTrue(unload.IsValid);
            Assert.IsFalse(unload.IsFaulted);
            Assert.IsTrue(_flow.IsBusy);
            CollectionAssert.Contains(_backend.Calls, "unload:Menu");
        }

        [Test]
        public void Unload_WhileLoading_ActivatesAndUnloadsWhenTheLoadCompletes() {
            var request = _flow.Load("Menu");

            var same = _flow.Unload(request);
            Assert.AreEqual(request, same, "an incomplete unload must return the same operation");

            CompleteLoad(request);

            CollectionAssert.AreEqual(
                new[] { "load:Menu:Single", "active:Menu", "unload:Menu" },
                _backend.Calls);
        }

        [Test]
        public void Unload_RejectsAnInvalidRequest() {
            Assert.Throws<ArgumentException>(() => _flow.Unload(SceneRequest.None));
        }

        [Test]
        public void Unload_RejectsAFaultedRequest() {
            _backend.LoadFault = new InvalidOperationException("missing");
            var request = _flow.Load("Missing");

            Assert.Throws<InvalidOperationException>(() => _flow.Unload(request));
        }

        [Test]
        public void ActiveScene_ComesFromTheBackend() {
            _backend.Active = "Gameplay";

            Assert.AreEqual("Gameplay", _flow.ActiveScene);
        }

        [Test]
        public void Stop_ForgetsTheOperationAndKeepsTheScene() {
            _flow.Load("Menu");

            _flow.Stop();

            Assert.IsFalse(_flow.IsBusy);
            Assert.DoesNotThrow(() => _flow.Step());
            Assert.AreEqual(1, _backend.Calls.Count, "Stop must not issue another backend call");
        }

        [Test]
        public void SceneModule_RegistersTheFlow() {
            var flow = new SceneFlow(_backend);
            var module = new SceneModule(null, flow);

            GrowModules.Register(module);
            GrowModules.Bootstrap();

            Assert.AreEqual(SceneModule.DefaultModuleName, module.Name);
            Assert.AreSame(flow, GrowModules.GetRequiredService<ISceneFlow>());
        }

        [Test]
        public void SceneModule_LoadsTheInitialSceneOnStart() {
            var module = new SceneModule("Boot", new SceneFlow(_backend));

            GrowModules.Register(module);
            GrowModules.Bootstrap();

            CollectionAssert.AreEqual(new[] { "load:Boot:Single" }, _backend.Calls);
        }

        [Test]
        public void UseScenes_AddsTheCapabilityToTheComposition() {
            var host = new GrowBuilder()
                .ConfigureBoot(options => options.AutoStart = false)
                .UseScenes()
                .Build();

            try {
                host.Start();

                Assert.IsNotNull(host.GetService<ISceneFlow>());
            } finally {
                host.Dispose();
            }
        }

        private void CompleteLoad(SceneRequest request) {
            _backend.Handle.IsDone = true;
            _flow.Step();
            Assert.IsTrue(request.IsDone, "the load must complete before the next assertion");
        }

        private sealed class FakeHandle : ISceneHandle {
            public bool IsDone { get; set; }

            public float Progress { get; set; }
        }

        private sealed class FakeBackend : ISceneBackend {
            internal FakeHandle Handle { get; set; } = new FakeHandle();

            internal List<string> Calls { get; } = new List<string>();

            internal Exception LoadFault { get; set; }

            internal string Active { get; set; } = "Boot";

            public string ActiveSceneName => Active;

            public ISceneHandle Load(string sceneName, LoadSceneMode mode) {
                Calls.Add($"load:{sceneName}:{mode}");
                if (LoadFault != null) {
                    throw LoadFault;
                }

                return Handle;
            }

            public ISceneHandle Unload(string sceneName) {
                Calls.Add($"unload:{sceneName}");
                return Handle;
            }

            public bool SetActive(string sceneName) {
                Calls.Add($"active:{sceneName}");
                Active = sceneName;
                return true;
            }
        }

        private sealed class ScenesNullLogSink : ILogSink {
            public void Write(in LogEntry entry) {
            }
        }
    }
}
