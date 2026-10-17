// SPDX-License-Identifier: MIT

using System;
using Grow.Kernel.Modules;
using Grow.Kernel.Modules.Impl;
using NUnit.Framework;

namespace Grow.Tests.Editor.Modules {
    [Category("Grow.Modules")]
    public class ModuleRegistryTests : ModulesTestBase {
        [Test]
        public void Bootstrap_OrdersModules_ByDependencyTopology() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new TopoA(log));
            registry.Register(new TopoB(log));
            registry.Register(new TopoC(log));

            registry.Bootstrap();

            Assert.AreEqual("C,B,A", log.Phase("Start"));
            Assert.IsTrue(registry.IsReady);
            Assert.IsTrue(registry.Healthy);
            Assert.AreEqual(0, registry.FailedModules.Count);
        }

        [Test]
        public void Bootstrap_SameLayer_OrdersByPriorityThenRegistration() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new PrioLow(log));
            registry.Register(new PrioHigh(log));
            registry.Register(new PrioMid(log));

            registry.Bootstrap();

            Assert.AreEqual("High,Low,Mid", log.Phase("Start"));
        }

        [Test]
        public void Bootstrap_MissingDependency_SkipsModuleButStartsOthers() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new DepMissing(log));
            registry.Register(new DepMissingOther(log));

            registry.Bootstrap();

            Assert.AreEqual("Other", log.Phase("Start"));
            Assert.IsTrue(registry.IsReady);
            Assert.IsFalse(registry.Healthy);
            CollectionAssert.Contains(registry.FailedModules, "Missing");
        }

        [Test]
        public void Bootstrap_CyclicDependency_SkipsCycleButStartsOthers() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new CycleX(log));
            registry.Register(new CycleY(log));
            registry.Register(new CycleFree(log));

            registry.Bootstrap();

            Assert.AreEqual("Free", log.Phase("Start"));
            Assert.IsTrue(registry.IsReady);
            Assert.IsFalse(registry.Healthy);
            CollectionAssert.Contains(registry.FailedModules, "X");
            CollectionAssert.Contains(registry.FailedModules, "Y");
            CollectionAssert.DoesNotContain(registry.FailedModules, "Free");
        }

        [Test]
        public void Bootstrap_HookException_IsIsolated() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new ThrowOnRegisterModule(log));
            registry.Register(new ThrowOnStartModule(log));
            registry.Register(new ExceptionSurvivor(log));

            registry.Bootstrap();

            Assert.AreEqual("ThrowRegister,ThrowStart,Survivor", log.Phase("Start"));
            Assert.AreEqual("ThrowRegister,ThrowStart,Survivor", log.Phase("Initialize"));
            Assert.IsTrue(registry.IsReady);
            Assert.IsFalse(registry.Healthy);
            CollectionAssert.Contains(registry.FailedModules, "ThrowRegister");
            CollectionAssert.Contains(registry.FailedModules, "ThrowStart");
            CollectionAssert.DoesNotContain(registry.FailedModules, "Survivor");
        }

        [Test]
        public void Bootstrap_DependencyMatchesMultipleModules_IsConfigurationError() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new SharedDependencyA(log));
            registry.Register(new SharedDependencyB(log));
            registry.Register(new NeedsSharedDependency(log));

            registry.Bootstrap();

            Assert.AreEqual("SharedA,SharedB", log.Phase("Start"));
            Assert.AreEqual(-1, log.IndexOf("Start:NeedsShared"));
            Assert.IsFalse(registry.Healthy);
            CollectionAssert.Contains(registry.FailedModules, "NeedsShared");
        }

        [Test]
        public void Bootstrap_EmptyRegistry_IsSafe() {
            var registry = new ModuleRegistry();

            Assert.DoesNotThrow(() => registry.Bootstrap());
            Assert.IsTrue(registry.IsReady);
            Assert.AreEqual(ModulePhase.Running, registry.Phase);
        }

        [Test]
        public void Hooks_ObserveExpectedPhases() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            var module = new TopoC(log);
            registry.Register(module);

            registry.Bootstrap();

            CollectionAssert.AreEqual(
                new[] { ModulePhase.Register, ModulePhase.Initialize, ModulePhase.Start },
                module.Phases);
        }

        [Test]
        public void Shutdown_CallsHooksInReverseOrder() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new TopoA(log));
            registry.Register(new TopoB(log));
            registry.Register(new TopoC(log));
            registry.Bootstrap();

            log.Clear();
            registry.Shutdown();

            Assert.AreEqual("A,B,C", log.Phase("Stop"));
            Assert.AreEqual("A,B,C", log.Phase("Dispose"));
            Assert.IsFalse(registry.IsReady);
            Assert.AreEqual(ModulePhase.None, registry.Phase);
        }

        [Test]
        public void Bootstrap_IsIdempotent() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new TopoC(log));

            registry.Bootstrap();
            registry.Bootstrap();

            Assert.AreEqual(1, log.Count("Register:C"));
            Assert.AreEqual(1, log.Count("Start:C"));
        }

        [Test]
        public void Shutdown_IsIdempotent_AndReBootstrap_RerunsHooks() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new TopoC(log));
            registry.Bootstrap();

            registry.Shutdown();
            Assert.DoesNotThrow(() => registry.Shutdown());
            Assert.AreEqual(ModulePhase.None, registry.Phase);
            Assert.IsFalse(registry.IsReady);

            log.Clear();
            registry.Bootstrap();

            Assert.AreEqual(1, log.Count("Register:C"));
            Assert.AreEqual(1, log.Count("Start:C"));
            Assert.IsTrue(registry.IsReady);
        }

        [Test]
        public void Register_Null_Throws() {
            var registry = new ModuleRegistry();
            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
        }

        [Test]
        public void Register_DuplicateName_Throws() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new TopoC(log));

            Assert.Throws<InvalidOperationException>(() => registry.Register(new TopoCDuplicate(log)));
        }

        [Test]
        public void Register_SameInstanceTwice_IsIdempotent() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            var module = new TopoC(log);

            registry.Register(module);
            Assert.DoesNotThrow(() => registry.Register(module));

            registry.Bootstrap();
            Assert.AreEqual(1, log.Count("Register:C"));
            Assert.AreEqual(1, log.Count("Start:C"));
        }

        [Test]
        public void Register_AfterBootstrap_Throws() {
            var log = new OrderLog();
            var registry = new ModuleRegistry();
            registry.Register(new TopoC(log));
            registry.Bootstrap();

            var exception = Assert.Throws<InvalidOperationException>(
                () => registry.Register(new TopoA(log)));
            StringAssert.Contains("Bootstrap", exception.Message);
            StringAssert.Contains("SubsystemRegistration", exception.Message);

            registry.Shutdown();
            Assert.DoesNotThrow(() => registry.Register(new TopoA(log)));
        }
    }
}
