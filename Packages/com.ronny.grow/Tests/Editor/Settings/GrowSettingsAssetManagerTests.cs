// SPDX-License-Identifier: MIT

using System;
using Grow.Editor.Settings;
using NUnit.Framework;

namespace Grow.Tests.Editor.Settings {
    public class GrowSettingsAssetManagerTests {
        private const string AssetPath =
            "UserSettings/Grow/Tests/GrowSettingsAssetManagerTests.asset";

        [SetUp]
        public void SetUp() {
            GrowSettingsAssetManager.DeleteAsset(AssetPath);
        }

        [TearDown]
        public void TearDown() {
            GrowSettingsAssetManager.DeleteAsset(AssetPath);
        }

        [Test]
        public void GetOrCreate_CreatesDefaultAsset() {
            var settings = GrowSettingsAssetManager.GetOrCreate<GrowSettingsTestAsset>(AssetPath);

            Assert.IsNotNull(settings);
            Assert.IsTrue(GrowSettingsAssetManager.Exists(AssetPath));
            Assert.AreEqual(7, settings.Value);
        }

        [Test]
        public void GetOrCreate_LoadsExistingAsset() {
            var created = GrowSettingsAssetManager.GetOrCreate<GrowSettingsTestAsset>(AssetPath);
            created.Value = 42;
            GrowSettingsAssetManager.Save(created, AssetPath);

            var loaded = GrowSettingsAssetManager.GetOrCreate<GrowSettingsTestAsset>(AssetPath);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(42, loaded.Value);
        }

        [Test]
        public void TryLoad_ReturnsFalseForMissingAsset() {
            var loaded = GrowSettingsAssetManager.TryLoad<GrowSettingsTestAsset>(
                AssetPath, out var settings);

            Assert.IsFalse(loaded);
            Assert.IsNull(settings);
        }

        [Test]
        public void DeleteAsset_RemovesAsset() {
            GrowSettingsAssetManager.GetOrCreate<GrowSettingsTestAsset>(AssetPath);

            GrowSettingsAssetManager.DeleteAsset(AssetPath);

            Assert.IsFalse(GrowSettingsAssetManager.Exists(AssetPath));
        }

        [Test]
        public void Exists_ReturnsFalseForMissingAsset() {
            Assert.IsFalse(GrowSettingsAssetManager.Exists(AssetPath));
        }

        [Test]
        public void GetOrCreate_RejectsEmptyPath() {
            Assert.Throws<ArgumentException>(
                () => GrowSettingsAssetManager.GetOrCreate<GrowSettingsTestAsset>(string.Empty));
        }
    }
}
