// SPDX-License-Identifier: MIT

using System;
using System.IO;
using Grow.Editor.Settings;
using Grow.Editor.Settings.Preferences;
using NUnit.Framework;
using UnityEngine;

namespace Grow.Tests.Editor.Settings {
    public class GrowEditorPreferencesTests {
        private string _root;

        [SetUp]
        public void SetUp() {
            _root = Path.Combine(
                Path.GetTempPath(), "GrowPreferencesTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown() {
            if (Directory.Exists(_root)) {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void DefaultsToSystemLanguage() {
            var settings = GrowEditorPreferences.CreateDefault();

            try {
                Assert.AreEqual(GrowEditorPreferences.SystemLanguage, settings.Language);
                Assert.AreEqual(GrowEditorPreferences.Schema,
                    ((IGrowSettings)settings).SchemaVersion);
            } finally {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ValidateRepairsMissingLanguage() {
            var settings = GrowEditorPreferences.CreateDefault();

            try {
                settings.Language = null;

                Assert.IsTrue(settings.Validate());
                Assert.AreEqual(GrowEditorPreferences.SystemLanguage, settings.Language);
            } finally {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Store_RoundTripsLanguage() {
            var path = Path.Combine(_root, "EditorPreferences.json");
            var store = new GrowSettingsStore<GrowEditorPreferences>(
                path, GrowEditorPreferences.CreateDefault);

            store.Instance.Language = "zh-Hans";
            store.MarkDirty();

            Assert.IsTrue(store.TrySave(out var error), error);

            var reloaded = new GrowSettingsStore<GrowEditorPreferences>(
                path, GrowEditorPreferences.CreateDefault);

            Assert.AreEqual("zh-Hans", reloaded.Instance.Language);
            Assert.AreEqual(GrowSettingsLoadStatus.Loaded, reloaded.LoadResult.Status);
        }
    }
}
