// SPDX-License-Identifier: MIT

using System;
using System.IO;
using Grow.Editor.Localization;
using Grow.Editor.Settings.Preferences;
using Grow.Lens.Editor.Core;
using Grow.Lens.Editor.Localization;
using Grow.Lens.Editor.Settings;
using NUnit.Framework;
using UnityEditor;

namespace Grow.Lens.Tests.Editor.Core {
    /// <summary>Tests that Lens follows the shared Grow language preference.</summary>
    public class GrowLensLocalizationTests {
        private string _directory;

        [SetUp]
        public void SetUp() {
            _directory = Path.Combine(Path.GetTempPath(), "GrowLensTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            GrowEditorPreferencesStore.SetPathForTests(
                Path.Combine(_directory, "EditorPreferences.json"));
            GrowLocalization.Reload();
            GrowLensLocalization.ResetForTests();
        }

        [TearDown]
        public void TearDown() {
            GrowEditorPreferencesStore.ResetPathForTests();
            GrowLocalization.Reload();
            GrowLensLocalization.ResetForTests();
            if (Directory.Exists(_directory)) {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void EnglishPreference_UsesEnglishCatalog() {
            GrowEditorPreferencesStore.Instance.Language = "en";

            Assert.AreEqual("en", GrowLensLocalization.CurrentLanguage);
            Assert.AreEqual("Enable Grow Lens",
                GrowLensLocalization.Get("grow.lens.preferences.master"));
        }

        [Test]
        public void ChinesePreference_UsesChineseCatalog() {
            GrowEditorPreferencesStore.Instance.Language = "zh-Hans";

            Assert.AreEqual("zh-Hans", GrowLensLocalization.CurrentLanguage);
            Assert.AreEqual("启用 Grow Lens",
                GrowLensLocalization.Get("grow.lens.preferences.master"));
        }

        [Test]
        public void UnsupportedPreference_FallsBackToEnglish() {
            GrowEditorPreferencesStore.Instance.Language = "fr";

            Assert.AreEqual("en", GrowLensLocalization.CurrentLanguage);
            Assert.AreEqual("Enable Grow Lens",
                GrowLensLocalization.Get("grow.lens.preferences.master"));
        }

        [Test]
        public void MissingPreference_FollowsTheGrowLanguage() {
            Assert.AreEqual(GrowLocalization.CurrentLanguage,
                GrowLensLocalization.CurrentLanguage);
        }

        [Test]
        public void MissingKey_ReturnsTheKey() {
            Assert.AreEqual("grow.lens.missing.key",
                GrowLensLocalization.Get("grow.lens.missing.key"));
        }

        [Test]
        public void PreferencesKeywords_FollowTheLanguage() {
            GrowLocalization.SetLanguage("en");
            var provider = GrowLensSettingsProvider.Create();
            CollectionAssert.Contains(provider.keywords, "Restore defaults");

            GrowLocalization.SetLanguage("zh-Hans");

            CollectionAssert.Contains(provider.keywords, "恢复默认设置");
            CollectionAssert.DoesNotContain(provider.keywords, "Restore defaults");
        }
    }
}
