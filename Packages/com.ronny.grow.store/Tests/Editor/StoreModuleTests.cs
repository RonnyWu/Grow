// SPDX-License-Identifier: MIT

using System;
using Grow.Kernel.Modules;
using Grow.Store;
using Grow.Store.Contracts;
using Grow.Store.Internal;
using NUnit.Framework;

namespace Grow.Store.Tests.Editor {
    public class StoreModuleTests {
        private const string DefaultName = "module_test";
        private const string NamedName = "module_named_test";

        [SetUp]
        public void SetUp() {
            GrowModules.Reset();
        }

        [TearDown]
        public void TearDown() {
            GrowModules.Reset();

            using var cleanup = new StoreProvider();
            cleanup.DeleteStore(DefaultName, StoreType.User);
            cleanup.DeleteStore(NamedName, StoreType.User);
        }

        [Test]
        public void Bootstrap_RegistersProviderAndDefaultStore() {
            GrowModules.Register(new StoreModule(DefaultName, StoreType.User));

            GrowModules.Bootstrap();

            var provider = GrowModules.GetRequiredService<IStoreProvider>();
            var store = GrowModules.GetRequiredService<IStore>();
            Assert.NotNull(provider);
            Assert.NotNull(store);
            Assert.AreSame(provider.GetStore(DefaultName, StoreType.User), store);
        }

        [Test]
        public void Provider_OpensNamedStoresAndCachesThem() {
            GrowModules.Register(new StoreModule(DefaultName, StoreType.User));
            GrowModules.Bootstrap();
            var provider = GrowModules.GetRequiredService<IStoreProvider>();

            var first = provider.GetStore(NamedName, StoreType.User);
            var second = provider.GetStore(NamedName, StoreType.User);

            Assert.AreSame(first, second);
            provider.DeleteStore(NamedName, StoreType.User);
        }

        [Test]
        public void Shutdown_PersistsTheDefaultStore() {
            GrowModules.Register(new StoreModule(DefaultName, StoreType.User));
            GrowModules.Bootstrap();
            GrowModules.GetRequiredService<IStore>().Set("k", 1);

            GrowModules.Shutdown();

            using var reopenedProvider = new StoreProvider();
            var reopened = reopenedProvider.GetStore(DefaultName, StoreType.User);
            Assert.AreEqual(1, reopened.Get("k", -1));
        }

        [Test]
        public void Bootstrap_AfterShutdown_RecreatesTheOwnedProvider() {
            GrowModules.Register(new StoreModule(DefaultName, StoreType.User));
            GrowModules.Bootstrap();
            var first = GrowModules.GetRequiredService<IStoreProvider>();

            GrowModules.Shutdown();
            GrowModules.Bootstrap();
            var second = GrowModules.GetRequiredService<IStoreProvider>();

            Assert.AreNotSame(first, second);
            Assert.DoesNotThrow(() => second.GetStore(NamedName, StoreType.User));
            second.DeleteStore(NamedName, StoreType.User);
        }

        [Test]
        public void Module_ExposesDiagnostics() {
            var module = new StoreModule(DefaultName, StoreType.User);
            GrowModules.Register(module);
            GrowModules.Bootstrap();

            Assert.NotNull(module.Diagnostics);
        }

        [Test]
        public void Constructor_RejectsBlankNames() {
            Assert.Throws<ArgumentNullException>(() => new StoreModule("   "));
            Assert.Throws<ArgumentNullException>(() => new StoreModule(DefaultName, StoreType.User,
                moduleName: "   "));
        }

        [Test]
        public void ModuleName_IsConfigurable() {
            var module = new StoreModule(DefaultName, StoreType.User, moduleName: "Settings");

            Assert.AreEqual("Settings", module.Name);
        }
    }
}
