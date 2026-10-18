// SPDX-License-Identifier: MIT

using System;
using System.IO;
using Grow.Editor.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Grow.Tests.Editor.Settings {
    public class GrowSettingsStoreTests {
        private string _root;
        private string _path;

        [SetUp]
        public void SetUp() {
            _root = Path.Combine(
                Path.GetTempPath(), "GrowStoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _path = Path.Combine(_root, "Settings.json");
        }

        [TearDown]
        public void TearDown() {
            if (Directory.Exists(_root)) {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void TrySave_WhenTargetLocked_RemainsDirtyAndRetries() {
            var store = new GrowSettingsStore<GrowSettingsFileTestSettings>(
                _path, GrowSettingsFileTestSettings.CreateDefault);

            store.Instance.Value = "first";
            store.MarkDirty();
            Assert.IsTrue(store.TrySave(out var error), error);

            store.Instance.Value = "second";
            store.MarkDirty();

            using (var lockStream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.None)) {
                Assert.IsFalse(store.TrySave(out error));
                Assert.IsTrue(store.HasUnsavedChanges);
                Assert.IsFalse(string.IsNullOrEmpty(error));
            }

            Assert.IsTrue(store.TrySave(out error), error);
            StringAssert.Contains("second", File.ReadAllText(_path));
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        [Test]
        public void TrySave_WhenFileChangedOnDisk_EntersConflict() {
            var store = new GrowSettingsStore<GrowSettingsFileTestSettings>(
                _path, GrowSettingsFileTestSettings.CreateDefault);
            store.Instance.Value = "mine";
            store.MarkDirty();

            File.WriteAllText(_path,
                "{\"format\":\"" + GrowSettingsFile.FormatMarker +
                "\",\"schemaVersion\":2,\"value\":\"external\"}");

            Assert.IsFalse(store.TrySave(out _));
            Assert.AreEqual(GrowSettingsLoadStatus.Conflict, store.LoadResult.Status);
            StringAssert.Contains("external", File.ReadAllText(_path));
            Assert.IsTrue(Directory.GetFiles(_root, "*.conflict-*").Length > 0);
        }

        [Test]
        public void ResetToDefaults_RefusesMigrationFailure() {
            File.WriteAllText(_path,
                "{\"format\":\"" + GrowSettingsFile.FormatMarker +
                "\",\"schemaVersion\":1,\"value\":\"old\"}");

            var store = new GrowSettingsStore<GrowSettingsMigrationFailureSettings>(
                _path, GrowSettingsMigrationFailureSettings.CreateDefault);

            Assert.AreEqual(GrowSettingsLoadStatus.MigrationFailed, store.LoadResult.Status);

            store.ResetToDefaults();

            Assert.AreEqual(GrowSettingsLoadStatus.MigrationFailed, store.LoadResult.Status);
            StringAssert.Contains("old", File.ReadAllText(_path));
        }

        [Test]
        public void Load_WhenValidateThrows_ReturnsMigrationFailure() {
            File.WriteAllText(_path,
                "{\"format\":\"" + GrowSettingsFile.FormatMarker +
                "\",\"schemaVersion\":2,\"value\":\"old\"}");

            var result = GrowSettingsFile.Load(
                _path, GrowSettingsValidateFailureSettings.CreateDefault, out _);

            Assert.AreEqual(GrowSettingsLoadStatus.MigrationFailed, result.Status);
            Assert.IsFalse(result.CanSave);
        }
    }

    [Serializable]
    public sealed class GrowSettingsMigrationFailureSettings : ScriptableObject, IGrowSettings {
        public const int Schema = 2;

        [SerializeField]
        private string format = GrowSettingsFile.FormatMarker;

        [SerializeField]
        private int schemaVersion = 1;

        [SerializeField]
        private string value = "old";

        int IGrowSettings.SchemaVersion {
            get => schemaVersion;
            set => schemaVersion = value;
        }

        int IGrowSettings.CurrentSchema => Schema;

        public static GrowSettingsMigrationFailureSettings CreateDefault() {
            var settings = CreateInstance<GrowSettingsMigrationFailureSettings>();
            settings.ResetToDefaults();
            return settings;
        }

        public void ResetToDefaults() {
            format = GrowSettingsFile.FormatMarker;
            schemaVersion = 1;
            value = "old";
        }

        public void Migrate(int fromVersion) {
            throw new InvalidOperationException("migration failure");
        }

        public bool Validate() {
            _ = format;
            _ = value;
            return false;
        }
    }

    [Serializable]
    public sealed class GrowSettingsValidateFailureSettings : ScriptableObject, IGrowSettings {
        public const int Schema = 2;

        [SerializeField]
        private string format = GrowSettingsFile.FormatMarker;

        [SerializeField]
        private int schemaVersion = Schema;

        [SerializeField]
        private string value = "old";

        int IGrowSettings.SchemaVersion {
            get => schemaVersion;
            set => schemaVersion = value;
        }

        int IGrowSettings.CurrentSchema => Schema;

        public static GrowSettingsValidateFailureSettings CreateDefault() {
            var settings = CreateInstance<GrowSettingsValidateFailureSettings>();
            settings.ResetToDefaults();
            return settings;
        }

        public void ResetToDefaults() {
            format = GrowSettingsFile.FormatMarker;
            schemaVersion = Schema;
            value = "old";
        }

        public void Migrate(int fromVersion) {
            schemaVersion = Schema;
        }

        public bool Validate() {
            _ = format;
            _ = value;
            throw new InvalidOperationException("validate failure");
        }
    }
}
