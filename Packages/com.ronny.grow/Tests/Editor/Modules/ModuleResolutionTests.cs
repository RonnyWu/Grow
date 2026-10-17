// SPDX-License-Identifier: MIT

using System;
using Grow.Kernel.Ioc;
using Grow.Kernel.Ioc.Common;
using Grow.Kernel.Modules;
using Grow.Kernel.Modules.Impl;
using NUnit.Framework;

namespace Grow.Tests.Editor.Modules {
    [Category("Grow.Modules")]
    public class ModuleResolutionTests : ModulesTestBase {
        [Test]
        public void Resolve_EagerService_ReturnsTheRegisteredInstance() {
            var log = new OrderLog();
            var service = new ResolveService { Value = "ready" };
            var registry = new ModuleRegistry();
            registry.Register(new EagerServiceModule(log, service));

            registry.Bootstrap();

            Assert.AreSame(service, registry.GetService<ResolveService>());
            Assert.AreSame(service, registry.GetService<ResolveService>());
        }

        [Test]
        public void GetService_Miss_ReturnsNull_AndGetRequiredServiceThrows() {
            var registry = new ModuleRegistry();
            registry.Bootstrap();

            Assert.IsNull(registry.GetService<AbsentService>());
            Assert.Throws<ServiceNotFoundException>(() => registry.GetRequiredService<AbsentService>());
        }

        [Test]
        public void LazyService_IsCreatedOnce_OnFirstResolve() {
            var log = new OrderLog();
            var counter = new LazyFactoryCounter();
            var registry = new ModuleRegistry();
            registry.Register(new LazyServiceModule(log, counter));

            registry.Bootstrap();
            Assert.AreEqual(0, counter.Count);

            var first = registry.GetService<LazyService>();
            var second = registry.GetService<LazyService>();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, counter.Count);
        }

        [Test]
        public void ManagedServices_AreInitializedAndStarted_AfterModulePhases() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            var module = new ManagedServiceModule(log);
            var service = new RecordingManagedService(log, "A");
            module.Services.Add(service);
            registry.Register(module);

            registry.Bootstrap();

            Assert.AreEqual(1, service.InitializeCount);
            Assert.AreEqual(1, service.StartCount);
            Assert.AreEqual(0, service.DeinitializeCount);
            Assert.Less(log.IndexOf("SvcInit:A"), log.IndexOf("Start:Managed"));
            Assert.Less(log.IndexOf("Start:Managed"), log.IndexOf("SvcStart:A"));
        }

        [Test]
        public void ManagedServiceHookFailure_IsRecorded_AndDoesNotAbortOthers() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            var module = new MixedManagedServiceModule(log);
            var failing = new RecordingManagedService(log, "Failing") { ThrowOnInitialize = true };
            var survivor = new RecordingManagedServiceB(log, "Survivor");
            module.First = failing;
            module.Second = survivor;
            registry.Register(module);

            registry.Bootstrap();

            Assert.IsTrue(registry.IsReady);
            Assert.IsFalse(registry.Healthy);
            CollectionAssert.Contains(registry.FailedModules, nameof(RecordingManagedService));
            Assert.AreEqual(1, survivor.InitializeCount);
            Assert.AreEqual(1, survivor.StartCount);
            Assert.AreEqual(0, failing.StartCount, "a failed initialize must not be followed by start");
        }

        [Test]
        public void ManagedServices_AreDeinitializedInReverseOrder_AfterModulesStop() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            var module = new MixedManagedServiceModule(log);
            var first = new RecordingManagedService(log, "First");
            var second = new RecordingManagedServiceB(log, "Second");
            module.First = first;
            module.Second = second;
            registry.Register(module);

            registry.Bootstrap();
            log.Clear();
            registry.Shutdown();

            Assert.Greater(log.IndexOf("SvcDeinit:Second"), log.IndexOf("Stop:Managed"));
            Assert.Greater(log.IndexOf("SvcDeinit:Second"), log.IndexOf("Dispose:Managed"));
            Assert.Less(log.IndexOf("SvcDeinit:Second"), log.IndexOf("SvcDeinit:First"));
            Assert.AreEqual(1, first.DeinitializeCount);
            Assert.AreEqual(1, second.DeinitializeCount);
        }

        [Test]
        public void ManagedServiceRegisteredDuringStart_IsAlsoDriven() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            var late = new RecordingManagedService(log, "Late");
            var module = new LateRegistrationModule(log, late);
            registry.Register(module);

            registry.Bootstrap();

            Assert.IsNotNull(module.First);
            Assert.AreEqual(1, module.First.StartCount);
            Assert.AreEqual(1, late.InitializeCount);
            Assert.AreEqual(1, late.StartCount);
        }

        [Test]
        public void LateManagedServiceFailure_IsRecordedInFailures() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            var late = new RecordingManagedService(log, "Late") { ThrowOnInitialize = true };
            var module = new LateRegistrationModule(log, late);
            registry.Register(module);

            registry.Bootstrap();

            Assert.IsFalse(registry.Healthy);
            Assert.AreEqual(0, late.StartCount);
            Assert.AreEqual(ModuleFailureKind.ManagedServiceHookException, registry.Failures[0].Kind);
        }

        [Test]
        public void Shutdown_ClearsExplicitServiceInstances() {
            var log = new OrderLog();
            var service = new ResolveService { Value = "before" };
            var registry = new ModuleRegistry();
            registry.Register(new EagerServiceModule(log, service));

            registry.Bootstrap();
            Assert.AreSame(service, registry.GetService<ResolveService>());

            registry.Shutdown();

            Assert.IsNull(registry.GetService<ResolveService>());
        }

        [Test]
        public void Facade_PublishesModuleServicesToTheIocRoot_AndRemovesThemOnShutdown() {
            var log = new OrderLog();
            var service = new ResolveService { Value = "shared" };
            GrowModules.Register(new EagerServiceModule(log, service));

            GrowModules.Bootstrap();

            Assert.AreSame(service, IOC.GetService<ResolveService>());

            GrowModules.Shutdown();

            Assert.IsFalse(IOC.Contains<ResolveService>());
        }

        [Test]
        public void ModuleScope_IsShared_AndDisposedOnShutdown() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            var module = new ScopeModule(log);
            registry.Register(module);

            registry.Bootstrap();

            Assert.AreSame(module.FirstScope, module.SecondScope);
            Assert.AreEqual(1, module.Target.InitializeCount);

            registry.Shutdown();

            Assert.AreEqual(1, module.Target.DeinitializeCount);
        }

        [Test]
        public void Facade_CanRegisterBootstrapResolveAndReset() {
            var log = new OrderLog();
            var service = new ResolveService { Value = "facade" };
            GrowModules.Register(new EagerServiceModule(log, service));

            GrowModules.Bootstrap();

            Assert.IsTrue(GrowModules.IsReady);
            Assert.AreEqual(ModulePhase.Running, GrowModules.Phase);
            Assert.AreSame(service, GrowModules.GetService<ResolveService>());

            GrowModules.Shutdown();
            GrowModules.ResetStaticState();
            Assert.AreEqual(ModulePhase.None, GrowModules.Phase);
            Assert.IsFalse(GrowModules.IsReady);
        }

        [Test]
        public void Diagnostics_KeepStructuredFailureKindsAndDescribeSnapshot() {
            var log = new OrderLog();
            var missingRegistry = new ModuleRegistry();
            missingRegistry.Register(new DepMissing(log));
            missingRegistry.Bootstrap();

            Assert.AreEqual(1, missingRegistry.Failures.Count);
            Assert.AreEqual(ModuleFailureKind.MissingDependency, missingRegistry.Failures[0].Kind);
            Assert.AreEqual(ModulePhase.Register, missingRegistry.Failures[0].Phase);
            StringAssert.Contains("phase=", missingRegistry.Describe());
            StringAssert.Contains("failures=", missingRegistry.Describe());

            var cycleRegistry = new ModuleRegistry();
            cycleRegistry.Register(new CycleX(log));
            cycleRegistry.Register(new CycleY(log));
            cycleRegistry.Bootstrap();

            Assert.AreEqual(2, cycleRegistry.Failures.Count);
            Assert.AreEqual(ModuleFailureKind.DependencyCycle, cycleRegistry.Failures[0].Kind);
            Assert.AreEqual(ModuleFailureKind.DependencyCycle, cycleRegistry.Failures[1].Kind);
        }
    }
}
