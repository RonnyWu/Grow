// SPDX-License-Identifier: MIT

using Grow.Core.Log;
using Grow.Kernel.Entry;
using Grow.Kernel.Ioc;
using Grow.Kernel.Modules;
using Grow.Scenes;
using Grow.Store.Contracts;
using NUnit.Framework;

namespace Grow.Framework.Tests.Editor {
    [Category("Grow.Integration")]
    public class GrowFrameworkTests {
        [SetUp]
        public void SetUp() {
            GrowLog.SetSink(new FrameworkNullLogSink());
            GrowApp.Shutdown();
            GrowApp.AutoInitialize = true;
            GrowModules.Reset();
            IOC.Clear();
        }

        [TearDown]
        public void TearDown() {
            GrowApp.Shutdown();
            GrowApp.AutoInitialize = true;
            GrowModules.Reset();
            IOC.Clear();
            GrowLog.Reset();
        }

        [Test]
        public void Create_ComposesTheDefaultCapabilities() {
            var host = GrowFramework.Create()
                .ConfigureBoot(options => options.AutoStart = false)
                .Build();

            try {
                host.Start();

                Assert.IsTrue(host.IsReady);
                Assert.IsNotNull(host.GetService<IStoreProvider>());
                Assert.IsNotNull(host.GetService<IStore>());
                Assert.IsNotNull(host.GetService<ISceneFlow>());
            } finally {
                host.Dispose();
            }
        }

        [Test]
        public void Create_StartsByDefault() {
            var host = GrowFramework.Create().Build();

            try {
                Assert.AreEqual(GrowState.Ready, host.State);
                Assert.AreEqual(1f, host.Progress);
            } finally {
                host.Dispose();
            }
        }

        [Test]
        public void Create_ReturnsABuilderThatStillAcceptsGameModules() {
            var module = new FrameworkProbeModule();

            var host = GrowFramework.Create()
                .ConfigureBoot(options => options.AutoStart = false)
                .AddModule(module)
                .Build();

            try {
                host.Start();
                Assert.AreEqual(1, module.StartCount);
            } finally {
                host.Dispose();
            }
        }

        private sealed class FrameworkProbeModule : IModule {
            public string Name => "Framework.Probe";

            public int Priority => 0;

            public System.Collections.Generic.IReadOnlyList<System.Type> Dependencies =>
                System.Array.Empty<System.Type>();

            public int StartCount { get; private set; }

            public void OnRegister(IModuleContext context) {
            }

            public void OnInitialize(IModuleContext context) {
            }

            public void OnStart(IModuleContext context) {
                StartCount++;
            }

            public void OnStop() {
            }

            public void OnDispose() {
            }
        }

        private sealed class FrameworkNullLogSink : ILogSink {
            public void Write(in LogEntry entry) {
            }
        }
    }
}
