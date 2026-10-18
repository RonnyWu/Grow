// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text;
using Grow.Store.Contracts;
using Grow.Store.Format;
using Grow.Store.Internal;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Grow.Store.Tests.Editor {
    public class StoreFileTests {
        private string _root;
        private StoreProvider _provider;

        [SetUp]
        public void SetUp() {
            _root = Path.Combine(Path.GetTempPath(), "Grow_StoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _provider = NewProvider();
        }

        [TearDown]
        public void TearDown() {
            _provider?.Dispose();
            if (Directory.Exists(_root)) {
                Directory.Delete(_root, true);
            }
        }

        private StoreProvider NewProvider() {
            return new StoreProvider(
                new StoreOptions { TargetBucketBytes = 2 * 1024 * 1024 }, _root);
        }

        private IStore NewStore(string name = "settings") {
            return _provider.GetStore(name, StoreType.User);
        }

        private string ScopeRoot() {
            return Path.Combine(_root, StoreType.User.ToString());
        }

        private string ManifestPath() {
            return Path.Combine(ScopeRoot(), "manifests");
        }

        private string BucketPath(string name, int bucket, long version) {
            return Path.Combine(ScopeRoot(), $"{name}.{bucket}.{version}.kv");
        }

        [Test]
        public void Open_DoesNotCreateFiles() {
            var store = NewStore();

            Assert.AreEqual("default", store.Get("missing", "default"));
            Assert.IsFalse(Directory.Exists(ScopeRoot()));
            Assert.AreEqual(0, store.Size.TotalBytes);
        }

        [Test]
        public void Primitives_RoundTripThroughManifestAndBucket() {
            var store = NewStore();
            store.Set("i", 42);
            store.Set("l", 5_000_000_000L);
            store.Set("f", 2.5f);
            store.Set("d", 3.25d);
            store.Set("b", true);
            store.Set("s", "hello\tworld");
            store.Save();

            Assert.IsTrue(File.Exists(ManifestPath()));
            Assert.IsTrue(File.Exists(BucketPath("settings", 0, 1)));
            StringAssert.StartsWith("// grow-store-manifests 1 ", File.ReadAllText(ManifestPath()));

            _provider.Dispose();
            _provider = NewProvider();
            var reload = NewStore();

            Assert.AreEqual(42, reload.Get("i", -1));
            Assert.AreEqual(5_000_000_000L, reload.Get("l", -1L));
            Assert.AreEqual(2.5f, reload.Get("f", -1f));
            Assert.AreEqual(3.25d, reload.Get("d", -1d));
            Assert.AreEqual(true, reload.Get("b", false));
            Assert.AreEqual("hello\tworld", reload.Get("s", string.Empty));
        }

        [Test]
        public void ByteArray_UsesBlobFileAndPointer() {
            var payload = new byte[] { 0, 1, 2, 127, 128, 255 };
            var store = NewStore();
            store.Set("blob", payload).Save();

            var blobDirectory = Path.Combine(ScopeRoot(), "settings.blobs");
            Assert.IsTrue(Directory.Exists(blobDirectory));
            Assert.AreEqual(1, Directory.GetFiles(blobDirectory, "*.bin").Length);

            var raw = File.ReadAllText(BucketPath("settings", 0, 1));
            StringAssert.Contains("blob\t", raw);
            StringAssert.DoesNotContain(Convert.ToBase64String(payload), raw);

            _provider.Dispose();
            _provider = NewProvider();
            CollectionAssert.AreEqual(payload, NewStore().Get("blob", Array.Empty<byte>()));
        }

        [Test]
        public void Get_DifferentKind_Throws_AndTryGetIsFalse() {
            var store = NewStore();
            store.Set("number", 7);

            Assert.Throws<InvalidCastException>(() => store.Get("number", "missing"));
            Assert.IsFalse(store.TryGet("number", out string _));
            Assert.IsTrue(store.TryGet("number", out int number));
            Assert.AreEqual(7, number);
        }

        [Test]
        public void DeleteAndClear_AreExplicitlyPersisted() {
            var store = NewStore();
            store.Set("a", 1).Set("b", 2).Save();
            store.Delete("a").Save();

            Assert.IsFalse(store.ContainsKey("a"));
            Assert.AreEqual(2, store.Get("b", -1));

            store.Clear();
            Assert.IsTrue(store.IsDirty);
            store.Save();
            Assert.IsFalse(store.ContainsKey("b"));
            Assert.IsTrue(File.Exists(BucketPath("settings", 0, 3)));
        }

        [Test]
        public void CleanSave_DoesNotAdvanceGeneration() {
            var store = NewStore();
            store.Set("k", 1).Save();
            var before = File.ReadAllText(ManifestPath());

            Assert.IsTrue(store.TrySave(out var error));
            Assert.IsNull(error);
            Assert.AreEqual(before, File.ReadAllText(ManifestPath()));
        }

        [Test]
        public void TrySaveAll_CommitsOneManifestForMultipleStores() {
            var first = NewStore("settings");
            var second = NewStore("audio");
            first.Set("a", 1);
            second.Set("b", 2);

            var before = File.Exists(ManifestPath()) ? File.ReadAllText(ManifestPath()) : string.Empty;
            Assert.IsTrue(_provider.TrySaveAll(out var error));
            Assert.IsNull(error);

            var after = File.ReadAllText(ManifestPath());
            StringAssert.Contains("store settings", after);
            StringAssert.Contains("store audio", after);
            Assert.AreNotEqual(before, after);
            Assert.AreEqual(2, CountOccurrences(after, "store "));
        }

        [Test]
        public void Close_SavesAndReleasesTheInstance() {
            var store = NewStore();
            store.Set("k", 1);

            Assert.IsTrue(_provider.Close("settings", StoreType.User));
            Assert.IsFalse(_provider.TryGetStore("settings", StoreType.User, out _));

            var reopened = NewStore();
            Assert.AreNotSame(store, reopened);
            Assert.AreEqual(1, reopened.Get("k", -1));
        }

        [Test]
        public void DeleteStore_RemovesManifestEntryAndFiles() {
            var store = NewStore();
            store.Set("k", 1).Save();
            Assert.IsTrue(File.Exists(BucketPath("settings", 0, 1)));

            Assert.IsTrue(_provider.DeleteStore("settings", StoreType.User));
            Assert.IsFalse(File.Exists(BucketPath("settings", 0, 1)));
            Assert.IsFalse(File.ReadAllText(ManifestPath()).Contains("store settings"));

            var reopened = NewStore();
            Assert.IsFalse(reopened.ContainsKey("k"));
        }

        [Test]
        public void CorruptedBucket_BlocksWrites_AndResetRecovers() {
            var store = NewStore();
            store.Set("k", 1).Save();
            var path = BucketPath("settings", 0, 1);
            var bytes = File.ReadAllBytes(path);
            bytes[bytes.Length - 1] ^= 0xFF;
            File.WriteAllBytes(path, bytes);

            _provider.Dispose();
            _provider = NewProvider();
            LogAssert.ignoreFailingMessages = true;
            try {
                var corrupted = NewStore();

                Assert.IsTrue(corrupted.IsCorrupted);
                Assert.Throws<InvalidOperationException>(() => corrupted.Set("k", 2));
                Assert.IsFalse(corrupted.TrySave(out _));

                Assert.IsTrue(corrupted.Reset());
                Assert.IsFalse(corrupted.IsCorrupted);
                corrupted.Set("k", 3).Save();
            } finally {
                LogAssert.ignoreFailingMessages = false;
            }

            _provider.Dispose();
            _provider = NewProvider();
            Assert.AreEqual(3, NewStore().Get("k", -1));
        }

        [Test]
        public void StoreNames_AreNormalizedAndValidated() {
            var upper = _provider.GetStore("Settings", StoreType.User);
            var lower = _provider.GetStore("settings", StoreType.User);
            Assert.AreSame(upper, lower);

            Assert.Throws<ArgumentException>(() => _provider.GetStore("  ", StoreType.User));
            Assert.Throws<ArgumentException>(() => _provider.GetStore("bad/name", StoreType.User));
            Assert.Throws<ArgumentException>(() => _provider.GetStore(".bad", StoreType.User));
            Assert.Throws<ArgumentException>(() => _provider.GetStore("bad..name", StoreType.User));
            Assert.Throws<ArgumentException>(() => _provider.GetStore("con", StoreType.User));
            Assert.Throws<ArgumentException>(() => _provider.GetStore("x.blobs", StoreType.User));
        }

        [Test]
        public void ManifestFileNameParsing_RejectsLeadingZero() {
            Assert.IsTrue(StoreManifest.TryParseFileName(
                "app.ui.0.1.kv", out var store, out var bucket, out var version));
            Assert.AreEqual("app.ui", store);
            Assert.AreEqual(0, bucket);
            Assert.AreEqual(1, version);

            Assert.IsFalse(StoreManifest.TryParseFileName(
                "app.0.01.kv", out _, out _, out _));
        }

        [Test]
        public void BlobGc_RemovesUnreferencedFile() {
            var store = NewStore();
            store.Set("blob", new byte[] { 1, 2, 3 }).Save();
            store.Set("blob", new byte[] { 4, 5, 6 }).Save();
            store.Set("blob", new byte[] { 7, 8, 9 }).Save();
            _provider.TryGc("settings", StoreType.User, out _);

            var blobs = Directory.GetFiles(Path.Combine(ScopeRoot(), "settings.blobs"), "*.bin");
            Assert.LessOrEqual(blobs.Length, 2, "current and backup blob references may remain");
        }

        [Test]
        public void SingleFile_MigratesToBucketAndBlob() {
            Directory.CreateDirectory(ScopeRoot());
            var payload = Convert.ToBase64String(new byte[] { 4, 5, 6 });
            File.WriteAllText(
                Path.Combine(ScopeRoot(), "settings.kv"),
                "// grow-store 1\nanswer\tint\t42\nblob\tbytes\t" + payload + "\n");

            var store = NewStore();

            Assert.AreEqual(42, store.Get("answer", -1));
            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, store.Get("blob", Array.Empty<byte>()));
            Assert.IsTrue(File.Exists(Path.Combine(ScopeRoot(), "settings.kv.migrated")));
            Assert.IsTrue(File.Exists(BucketPath("settings", 0, 1)));
            Assert.IsTrue(Directory.Exists(Path.Combine(ScopeRoot(), "settings.blobs")));
        }

        [Test]
        public void Provider_RejectsASecondOwnerOfTheSameScope() {
            NewStore("settings");
            using var second = new StoreProvider(new StoreOptions(), _root);
            Assert.Throws<InvalidOperationException>(
                () => second.GetStore("settings", StoreType.User));
        }

        [Test]
        public void StartupSplit_ConvergesAfterReopen() {
            _provider.Dispose();
            _provider = new StoreProvider(
                new StoreOptions { TargetBucketBytes = 1024, BucketCountLimit = 16 }, _root);
            var store = NewStore();
            for (var i = 0; i < 100; i++) {
                store.Set($"k{i}", new string('x', 64));
            }

            store.Save();
            var before = File.ReadAllText(ManifestPath());
            _provider.Dispose();
            _provider = new StoreProvider(
                new StoreOptions { TargetBucketBytes = 1024, BucketCountLimit = 16 }, _root);
            NewStore().Get("k1", string.Empty);

            var after = File.ReadAllText(ManifestPath());
            StringAssert.Contains("bucketCount=1", before);
            StringAssert.DoesNotContain("bucketCount=1\n", after);
            Assert.Greater(Directory.GetFiles(ScopeRoot(), "settings.*.kv").Length, 1);
        }

        private static int CountOccurrences(string text, string value) {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0) {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
