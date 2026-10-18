// SPDX-License-Identifier: MIT

using System;
using System.IO;
using Grow.Editor.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Grow.Tests.Editor.Settings {
    public class GrowSettingsFileTests {
        private string _root;

        [SetUp]
        public void SetUp() {
            _root = Path.Combine(
                Path.GetTempPath(), "GrowSettingsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown() {
            if (Directory.Exists(_root)) {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void Load_MissingFile_ReturnsCreatedAndDoesNotWrite() {
            var path = Path.Combine(_root, "Settings.json");

            var result = GrowSettingsFile.Load(
                path, GrowSettingsFileTestSettings.CreateDefault, out var settings);

            Assert.AreEqual(GrowSettingsLoadStatus.Created, result.Status);
            Assert.AreEqual("default", settings.Value);
            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void SaveThenLoad_RoundTrips() {
            var path = Path.Combine(_root, "Settings.json");
            var original = GrowSettingsFileTestSettings.CreateDefault();
            original.Value = "hello";

            GrowSettingsFile.Save(path, original);
            var result = GrowSettingsFile.Load(
                path, GrowSettingsFileTestSettings.CreateDefault, out var loaded);

            Assert.AreEqual(GrowSettingsLoadStatus.Loaded, result.Status);
            Assert.AreEqual("hello", loaded.Value);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void Save_CreatesBackupOfPreviousContent() {
            var path = Path.Combine(_root, "Settings.json");
            var first = GrowSettingsFileTestSettings.CreateDefault();
            first.Value = "first";
            GrowSettingsFile.Save(path, first);

            var second = GrowSettingsFileTestSettings.CreateDefault();
            second.Value = "second";
            GrowSettingsFile.Save(path, second);

            Assert.IsTrue(File.Exists(path + ".bak"));
            StringAssert.Contains("first", File.ReadAllText(path + ".bak"));
            StringAssert.Contains("second", File.ReadAllText(path));
        }

        [Test]
        public void Load_CorruptFile_RestoresBackup() {
            var path = Path.Combine(_root, "Settings.json");
            var first = GrowSettingsFileTestSettings.CreateDefault();
            first.Value = "first";
            GrowSettingsFile.Save(path, first);

            var second = GrowSettingsFileTestSettings.CreateDefault();
            second.Value = "second";
            GrowSettingsFile.Save(path, second);

            File.WriteAllText(path, "{ broken");

            var result = GrowSettingsFile.Load(
                path, GrowSettingsFileTestSettings.CreateDefault, out var settings);

            Assert.AreEqual(GrowSettingsLoadStatus.RestoredFromBackup, result.Status);
            Assert.AreEqual("first", settings.Value);
            Assert.IsTrue(Directory.GetFiles(_root, "*.corrupt-*").Length > 0);
            StringAssert.Contains("first", File.ReadAllText(path));
        }

        [Test]
        public void Load_BackupWithOlderSchema_MigratesAndRewritesMain() {
            var path = Path.Combine(_root, "Settings.json");
            File.WriteAllText(path + ".bak",
                "{\"format\":\"" + GrowSettingsFile.FormatMarker +
                "\",\"schemaVersion\":1,\"value\":\"old\"}");
            File.WriteAllText(path, "{ broken");

            var result = GrowSettingsFile.Load(
                path, GrowSettingsFileTestSettings.CreateDefault, out var settings);

            Assert.AreEqual(GrowSettingsLoadStatus.Migrated, result.Status);
            Assert.AreEqual("migrated", settings.Value);
            StringAssert.Contains("migrated", File.ReadAllText(path));
        }

        [Test]
        public void Load_CorruptWithoutBackup_ReturnsCorrupted() {
            var path = Path.Combine(_root, "Settings.json");
            File.WriteAllText(path, "{ broken");

            var result = GrowSettingsFile.Load(
                path, GrowSettingsFileTestSettings.CreateDefault, out _);

            Assert.AreEqual(GrowSettingsLoadStatus.Corrupted, result.Status);
            Assert.IsFalse(result.CanSave);
            Assert.IsTrue(Directory.GetFiles(_root, "*.corrupt-*").Length > 0);
        }

        [Test]
        public void Load_NewerSchema_IsReadOnlyAndDoesNotRewrite() {
            var path = Path.Combine(_root, "Settings.json");
            var future = "{\"format\":\"" + GrowSettingsFile.FormatMarker +
                "\",\"schemaVersion\":99,\"value\":\"future\"}";
            File.WriteAllText(path, future);

            var result = GrowSettingsFile.Load(
                path, GrowSettingsFileTestSettings.CreateDefault, out var settings);

            Assert.AreEqual(GrowSettingsLoadStatus.NewerSchema, result.Status);
            Assert.IsFalse(result.CanSave);
            Assert.AreEqual("default", settings.Value);
            Assert.AreEqual(future, File.ReadAllText(path));
        }

        [Test]
        public void Load_OlderSchema_RunsMigration() {
            var path = Path.Combine(_root, "Settings.json");
            File.WriteAllText(path,
                "{\"format\":\"" + GrowSettingsFile.FormatMarker +
                "\",\"schemaVersion\":1,\"value\":\"old\"}");

            var result = GrowSettingsFile.Load(
                path, GrowSettingsFileTestSettings.CreateDefault, out var settings);

            Assert.AreEqual(GrowSettingsLoadStatus.Migrated, result.Status);
            Assert.AreEqual("migrated", settings.Value);
            Assert.AreEqual(GrowSettingsFileTestSettings.Schema,
                ((IGrowSettings)settings).SchemaVersion);
        }

        [Test]
        public void ToProjectPath_RejectsEscapingPaths() {
            Assert.Throws<ArgumentException>(
                () => GrowSettingsFile.ToProjectPath("../outside.json"));
            Assert.Throws<ArgumentException>(
                () => GrowSettingsFile.ToProjectPath(
                    Path.GetFullPath(Path.Combine(_root, "outside.json"))));
        }
    }

    [Serializable]
    public sealed class GrowSettingsFileTestSettings : ScriptableObject, IGrowSettings {
        public const int Schema = 2;

        [SerializeField]
        private string format = GrowSettingsFile.FormatMarker;

        [SerializeField]
        private int schemaVersion = Schema;

        [SerializeField]
        private string value = "default";

        public string Value {
            get => value;
            set => this.value = value;
        }

        int IGrowSettings.SchemaVersion {
            get => schemaVersion;
            set => schemaVersion = value;
        }

        int IGrowSettings.CurrentSchema => Schema;

        public static GrowSettingsFileTestSettings CreateDefault() {
            var settings = CreateInstance<GrowSettingsFileTestSettings>();
            settings.ResetToDefaults();
            return settings;
        }

        public void ResetToDefaults() {
            format = GrowSettingsFile.FormatMarker;
            schemaVersion = Schema;
            value = "default";
        }

        public void Migrate(int fromVersion) {
            schemaVersion = Schema;
            value = "migrated";
        }

        public bool Validate() {
            if (format != GrowSettingsFile.FormatMarker) {
                format = GrowSettingsFile.FormatMarker;
                return true;
            }

            if (!string.IsNullOrEmpty(value)) {
                return false;
            }

            value = "default";
            return true;
        }
    }
}
