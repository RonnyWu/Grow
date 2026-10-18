// SPDX-License-Identifier: MIT

using System;
using System.IO;
using Grow.Store.Contracts;
using Grow.Store.Internal;
using NUnit.Framework;

namespace Grow.Store.Tests.Editor {
    public class StoreProviderTests {
        private string _root;
        private StoreProvider _provider;

        [SetUp]
        public void SetUp() {
            _root = Path.Combine(Path.GetTempPath(), "Grow_ProviderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _provider = new StoreProvider(new StoreOptions(), _root);
        }

        [TearDown]
        public void TearDown() {
            _provider?.Dispose();
            if (Directory.Exists(_root)) {
                Directory.Delete(_root, true);
            }
        }

        private string Root(StoreType type) {
            return Path.Combine(_root, type.ToString());
        }

        [Test]
        public void GetStore_SameNameAndType_ReturnsSameInstance() {
            var first = _provider.GetStore("settings", StoreType.User);
            var second = _provider.GetStore("settings", StoreType.User);

            Assert.AreSame(first, second);
        }

        [Test]
        public void GetStore_SameNameDifferentType_ReturnsDistinctInstances() {
            var user = _provider.GetStore("settings", StoreType.User);
            var runtime = _provider.GetStore("settings", StoreType.Runtime);

            Assert.AreNotSame(user, runtime);
        }

        [Test]
        public void TryGetStore_DoesNotOpenAStore() {
            Assert.IsFalse(_provider.TryGetStore("settings", StoreType.User, out _));

            var opened = _provider.GetStore("settings", StoreType.User);
            Assert.IsTrue(_provider.TryGetStore("settings", StoreType.User, out var found));
            Assert.AreSame(opened, found);
        }

        [Test]
        public void GetStore_ResolveOnly_DoesNotCreateFiles() {
            _provider.GetStore("settings", StoreType.Editor);
            _provider.GetStore("settings", StoreType.Runtime);
            _provider.GetStore("settings", StoreType.User);

            Assert.IsFalse(Directory.Exists(Root(StoreType.Editor)));
            Assert.IsFalse(Directory.Exists(Root(StoreType.Runtime)));
            Assert.IsFalse(Directory.Exists(Root(StoreType.User)));
        }

        [Test]
        public void GetStore_EachType_UsesItsOwnScope() {
            var editor = _provider.GetStore("settings", StoreType.Editor);
            var runtime = _provider.GetStore("settings", StoreType.Runtime);
            var user = _provider.GetStore("settings", StoreType.User);

            editor.Set("where", "editor").Save();
            runtime.Set("where", "runtime").Save();
            user.Set("where", "user").Save();

            Assert.IsTrue(File.Exists(Path.Combine(Root(StoreType.Editor), "settings.0.1.kv")));
            Assert.IsTrue(File.Exists(Path.Combine(Root(StoreType.Runtime), "settings.0.1.kv")));
            Assert.IsTrue(File.Exists(Path.Combine(Root(StoreType.User), "settings.0.1.kv")));
        }

        [Test]
        public void Close_KeepsFilesAndReleasesCacheEntry() {
            var store = _provider.GetStore("settings", StoreType.User);
            store.Set("k", 1);

            Assert.IsTrue(_provider.Close("settings", StoreType.User));
            Assert.IsFalse(_provider.TryGetStore("settings", StoreType.User, out _));
            Assert.IsTrue(File.Exists(Path.Combine(Root(StoreType.User), "settings.0.1.kv")));

            var reopened = _provider.GetStore("settings", StoreType.User);
            Assert.AreNotSame(store, reopened);
            Assert.AreEqual(1, reopened.Get("k", -1));
        }

        [Test]
        public void DeleteStore_RemovesIndexEntryAndFiles() {
            var store = _provider.GetStore("settings", StoreType.User);
            store.Set("k", 1).Save();

            Assert.IsTrue(_provider.DeleteStore("settings", StoreType.User));
            Assert.IsFalse(File.Exists(Path.Combine(Root(StoreType.User), "settings.0.1.kv")));
            Assert.IsFalse(File.ReadAllText(Path.Combine(Root(StoreType.User), "manifests"))
                .Contains("store settings"));
        }

        [Test]
        public void TrySaveAll_SavesEveryOpenStore() {
            var first = _provider.GetStore("settings", StoreType.User);
            var second = _provider.GetStore("audio", StoreType.User);
            first.Set("a", 1);
            second.Set("b", 2);

            Assert.IsTrue(_provider.TrySaveAll(out var error));
            Assert.IsNull(error);

            var manifest = File.ReadAllText(Path.Combine(Root(StoreType.User), "manifests"));
            StringAssert.Contains("store settings", manifest);
            StringAssert.Contains("store audio", manifest);
        }

        [Test]
        public void Dispose_DropsEveryCachedStore() {
            var store = _provider.GetStore("settings", StoreType.User);
            _provider.Dispose();

            Assert.IsFalse(_provider.TryGetStore("settings", StoreType.User, out _));
            Assert.Throws<ObjectDisposedException>(() => store.Get("k", 1));
        }

        [Test]
        public void IsSupported_ExposesTheEditorTypes() {
            Assert.IsTrue(_provider.IsSupported(StoreType.Editor));
            Assert.IsTrue(_provider.IsSupported(StoreType.Runtime));
            Assert.IsTrue(_provider.IsSupported(StoreType.User));
        }

        [Test]
        public void SecondProvider_CannotOwnTheSameScope() {
            _provider.GetStore("settings", StoreType.User);
            using var second = new StoreProvider(new StoreOptions(), _root);
            Assert.Throws<InvalidOperationException>(
                () => second.GetStore("settings", StoreType.User));
        }
    }
}
