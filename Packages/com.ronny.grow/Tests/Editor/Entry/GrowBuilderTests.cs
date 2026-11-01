// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Grow.Core.Log;
using Grow.Kernel.Entry;
using Grow.Kernel.Ioc;
using Grow.Kernel.Modules;
using NUnit.Framework;

namespace Grow.Tests.Editor.Entry {
    [Category("Grow.Integration")]
    public class GrowBuilderTests {
        [SetUp]
        public void SetUp() {
            GrowLog.SetSink(new BuilderNullLogSink());
            GrowSession.ResetStaticState();
            GrowModules.ResetStaticState();
            IOC.Clear();
            GrowBoot.Shutdown();
            GrowApp.AutoInitialize = true;
        }

        [TearDown]
        public void TearDown() {
            GrowApp.Shutdown();
            GrowSession.ResetStaticState();
            GrowModules.ResetStaticState();
            IOC.Clear();
            GrowBoot.Shutdown();
            GrowApp.AutoInitialize = true;
            GrowLog.Reset();
        }

        [Test]
        public void Build_WithAutoStartFalse_WaitsForStart() {
            var module = new BuilderProbeModule("Game");
            var host = new GrowBuilder()
                .ConfigureBoot(options => options.AutoStart = false)
                .AddModule(module)
                .Build();

            Assert.AreEqual(GrowState.None, host.State);
            Assert.IsFalse(host.IsReady);
            Assert.AreEqual(0, module.InitializeCount);

            host.Start();

            Assert.AreEqual(GrowState.Ready, host.State);
            Assert.IsTrue(host.IsReady);
            Assert.AreEqual(1, module.StartCount);
        }

        [Test]
        public void Build_WithDefaults_StartsImmediately() {
            var module = new BuilderProbeModule("Game");
            var host = new GrowBuilder().AddModule(module).Build();

            Assert.IsTrue(host.IsActive);
            Assert.AreEqual(GrowState.Ready, host.State);
            Assert.IsTrue(GrowApp.IsInitialized);
            Assert.AreEqual(1, module.RegisterCount);
            Assert.AreEqual(1, module.StartCount);
        }

        [Test]
        public void Build_SecondTime_ThrowsUntilTheHostIsDisposed() {
            var first = new GrowBuilder().Build();

            Assert.Throws<InvalidOperationException>(() => new GrowBuilder().Build());

            first.Dispose();

            Assert.DoesNotThrow(() => new GrowBuilder().Build().Dispose());
        }

        [Test]
        public void Build_CapturesAutoInitializeAndDisposeRestoresIt() {
            Assert.IsTrue(GrowApp.AutoInitialize);

            var host = new GrowBuilder().Build();

            Assert.IsFalse(GrowApp.AutoInitialize, "the builder owns the startup entry point");

            host.Dispose();

            Assert.IsTrue(GrowApp.AutoInitialize);
        }

        [Test]
        public void Build_WithCaptureDisabled_LeavesAutoInitializeAlone() {
            var host = new GrowBuilder()
                .ConfigureBoot(options => {
                    options.AutoStart = false;
                    options.CaptureAutoInitialize = false;
                })
                .Build();

            Assert.IsTrue(GrowApp.AutoInitialize);

            host.Dispose();

            Assert.IsTrue(GrowApp.AutoInitialize);
        }

        [Test]
        public void Host_ResolvesServicesRegisteredByAModule() {
            var host = new GrowBuilder()
                .AddModule(new BuilderProbeModule("Provider"))
                .Build();

            Assert.IsNotNull(host.GetService<BuilderProbeService>());
            Assert.AreSame(
                host.GetService<BuilderProbeService>(),
                host.GetRequiredService<BuilderProbeService>());
            Assert.IsNull(host.GetService<string>());
        }

        [Test]
        public void Host_ExposesTheBootState() {
            var host = new GrowBuilder().Build();

            Assert.AreEqual(GrowBoot.State, host.State);
            Assert.AreEqual(1f, host.Progress);

            var seen = new List<GrowState>();
            var subscription = host.StateChanged.Subscribe(change => seen.Add(change.Next));

            try {
                host.Shutdown();
            } finally {
                subscription.Dispose();
            }

            CollectionAssert.Contains(seen, GrowState.Shutdown);
        }

        [Test]
        public void Dispose_ShutsDownTheCompositionAndIsIdempotent() {
            var module = new BuilderProbeModule("Game");
            var host = new GrowBuilder().AddModule(module).Build();

            host.Dispose();

            Assert.AreEqual(GrowState.Shutdown, host.State);
            Assert.IsFalse(host.IsActive);
            Assert.AreEqual(1, module.StopCount);
            Assert.AreEqual(1, module.DisposeCount);

            Assert.DoesNotThrow(() => host.Dispose());
            Assert.AreEqual(1, module.StopCount);
        }

        [Test]
        public void Start_IsIdempotentAndThrowsAfterDispose() {
            var module = new BuilderProbeModule("Game");
            var host = new GrowBuilder()
                .ConfigureBoot(options => options.AutoStart = false)
                .AddModule(module)
                .Build();

            host.Start();
            host.Start();

            Assert.AreEqual(1, module.StartCount);
            Assert.AreEqual(1, module.InitializeCount);

            host.Dispose();

            Assert.Throws<ObjectDisposedException>(() => host.Start());
        }

        [Test]
        public void AddModuleAndConfigureBoot_RejectUseAfterBuild() {
            var builder = new GrowBuilder();
            builder.Build();

            Assert.Throws<InvalidOperationException>(
                () => builder.AddModule(new BuilderProbeModule("X")));
            Assert.Throws<InvalidOperationException>(() => builder.ConfigureBoot(_ => { }));
        }

        [Test]
        public void AddModule_RejectsNull() {
            var builder = new GrowBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.AddModule(null));
        }

        [Test]
        public void SessionReset_ReleasesTheSlotAndRestoresAutoInitialize() {
            new GrowBuilder().Build();
            Assert.IsFalse(GrowApp.AutoInitialize);

            GrowSession.ResetStaticState();

            Assert.IsTrue(GrowApp.AutoInitialize);
            Assert.DoesNotThrow(() => new GrowBuilder().Build().Dispose());
        }

        private sealed class BuilderProbeService {
        }

        private sealed class BuilderProbeModule : IModule {
            internal BuilderProbeModule(string name) {
                Name = name;
            }

            public string Name { get; }

            public int Priority => 0;

            public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

            public int RegisterCount { get; private set; }

            public int InitializeCount { get; private set; }

            public int StartCount { get; private set; }

            public int StopCount { get; private set; }

            public int DisposeCount { get; private set; }

            public void OnRegister(IModuleContext context) {
                RegisterCount++;
                context.Register(new BuilderProbeService());
            }

            public void OnInitialize(IModuleContext context) {
                InitializeCount++;
            }

            public void OnStart(IModuleContext context) {
                StartCount++;
            }

            public void OnStop() {
                StopCount++;
            }

            public void OnDispose() {
                DisposeCount++;
            }
        }

        private sealed class BuilderNullLogSink : ILogSink {
            public void Write(in LogEntry entry) {
            }
        }
    }
}
