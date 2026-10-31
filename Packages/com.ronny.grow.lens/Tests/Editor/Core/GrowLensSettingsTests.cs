// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text.RegularExpressions;
using Grow.Editor.Settings;
using Grow.Lens.Editor.Core;
using Grow.Lens.Editor.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Lens.Tests.Editor.Core {
    /// <summary>Tests for the per-project settings file.</summary>
    public class GrowLensSettingsTests {
        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp() {
            _directory = Path.Combine(Path.GetTempPath(), "GrowLensTests", Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "Lens.json");
            GrowLensSettings.SetPathForTests(_path);
        }

        [TearDown]
        public void TearDown() {
            GrowLensSettings.Enabled = false;
            GrowLensSettings.ResetPathForTests();
            GrowLensHost.ResetForTests();
            if (Directory.Exists(_directory)) {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void MissingFile_DefaultsToDisabledWithAllFeaturesOn() {
            Assert.IsFalse(GrowLensSettings.Enabled);
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyComponentIcons));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchySeparators));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyIndentGuides));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyActiveToggle));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyContextMenus));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.ProjectContextMenus));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.AssetHeader));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.Toolbar));
        }

        [Test]
        public void CorruptFile_DefaultsToDisabled() {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{ not json");
            LogAssert.Expect(LogType.Warning, new Regex("Could not read"));
            GrowLensSettings.ReloadForTests();
            Assert.IsFalse(GrowLensSettings.Enabled);
            Assert.AreEqual(GrowLensSettingsStatus.Corrupted, GrowLensSettings.Status);
            Assert.IsFalse(GrowLensSettings.CanSave);

            GrowLensSettings.Enabled = true;

            Assert.IsFalse(GrowLensSettings.Enabled);
            Assert.AreEqual("{ not json", File.ReadAllText(_path));
        }

        [Test]
        public void Enabled_RoundTripsThroughJson() {
            GrowLensSettings.Enabled = true;
            GrowLensSettings.ReloadForTests();
            Assert.IsTrue(GrowLensSettings.Enabled);

            GrowLensSettings.Enabled = false;
            GrowLensSettings.ReloadForTests();
            Assert.IsFalse(GrowLensSettings.Enabled);
        }

        [Test]
        public void Features_RoundTripThroughJson() {
            GrowLensSettings.Enabled = true;
            GrowLensSettings.SetFeature(GrowLensFeature.HierarchyComponentIcons, false);
            GrowLensSettings.SetFeature(GrowLensFeature.HierarchyIndentGuides, false);

            GrowLensSettings.ReloadForTests();

            Assert.IsTrue(GrowLensSettings.Enabled);
            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyComponentIcons));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchySeparators));
            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyIndentGuides));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.AssetHeader));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.ProjectContextMenus));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyContextMenus));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.Toolbar));
        }

        [Test]
        public void LegacyFile_KeepsAllFeaturesOn() {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{\"Enabled\":true}");
            GrowLensSettings.ReloadForTests();

            Assert.IsTrue(GrowLensSettings.Enabled);
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyComponentIcons));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchySeparators));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyIndentGuides));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyActiveToggle));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyContextMenus));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.ProjectContextMenus));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.AssetHeader));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.Toolbar));
        }

        [Test]
        public void HierarchyGroup_SetsAllFeaturesAtOnce() {
            GrowLensSettings.SetFeature(GrowLensFeature.AssetHeader, false);

            GrowLensSettings.SetHierarchyFeatures(false);
            GrowLensSettings.ReloadForTests();

            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyComponentIcons));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchySeparators));
            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyIndentGuides));
            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyActiveToggle));
            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyContextMenus));
            Assert.IsFalse(GrowLensSettings.AnyHierarchyFeature);
            Assert.IsFalse(GrowLensSettings.AllHierarchyFeatures);
            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.AssetHeader));

            GrowLensSettings.SetHierarchyFeatures(true);
            GrowLensSettings.ReloadForTests();

            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyComponentIcons));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchySeparators));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyIndentGuides));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyActiveToggle));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyContextMenus));
            Assert.IsTrue(GrowLensSettings.AnyHierarchyFeature);
            Assert.IsTrue(GrowLensSettings.AllHierarchyFeatures);
        }

        [Test]
        public void Schema4_AddsTheClickAndMenuDefaults() {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{\"SchemaVersion\":4,\"Enabled\":true,\"HierarchySeparators\":false}");
            GrowLensSettings.ReloadForTests();

            Assert.IsFalse(GrowLensSettings.HierarchyComponentInspector);
            Assert.IsFalse(GrowLensSettings.HierarchySeparatorMenuEnabled);

            GrowLensSettings.SetFeature(GrowLensFeature.HierarchySeparators, true);
            Assert.IsTrue(GrowLensSettings.HierarchySeparatorMenuEnabled);
            StringAssert.Contains("\"SchemaVersion\": 5", File.ReadAllText(_path));
        }

        [Test]
        public void Schema2ContextMenus_SplitsIntoBothWindowSwitches() {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path,
                "{\"SchemaVersion\":2,\"Enabled\":true,\"ContextMenus\":false}");
            GrowLensSettings.ReloadForTests();

            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.ProjectContextMenus));
            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyContextMenus));
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchyActiveToggle));
        }

        [Test]
        public void HierarchyGroup_ReportsMixedWhenOneFeatureIsOff() {
            GrowLensSettings.SetFeature(GrowLensFeature.HierarchyComponentIcons, false);

            Assert.IsTrue(GrowLensSettings.AnyHierarchyFeature);
            Assert.IsFalse(GrowLensSettings.AllHierarchyFeatures);
        }

        [Test]
        public void GuideSettings_DefaultToDashedAndRoundTrip() {
            Assert.IsTrue(GrowLensSettings.HierarchyGuideDashed);
            var theme = GrowLensSettings.EditorTextColor;
            Assert.AreEqual(theme.r, GrowLensSettings.HierarchyGuideColor.r, 0.001f);
            Assert.AreEqual(theme.g, GrowLensSettings.HierarchyGuideColor.g, 0.001f);
            Assert.AreEqual(theme.b, GrowLensSettings.HierarchyGuideColor.b, 0.001f);
            Assert.AreEqual(0.32f, GrowLensSettings.HierarchyGuideColor.a, 0.001f);

            var custom = new Color(0.8f, 0.2f, 0.1f, 0.6f);
            GrowLensSettings.SetHierarchyGuideColor(custom);
            GrowLensSettings.HierarchyGuideDashed = false;
            GrowLensSettings.ReloadForTests();

            Assert.IsFalse(GrowLensSettings.HierarchyGuideDashed);
            Assert.AreEqual(custom.r, GrowLensSettings.HierarchyGuideColor.r, 0.001f);
            Assert.AreEqual(custom.g, GrowLensSettings.HierarchyGuideColor.g, 0.001f);
            Assert.AreEqual(custom.b, GrowLensSettings.HierarchyGuideColor.b, 0.001f);
            Assert.AreEqual(custom.a, GrowLensSettings.HierarchyGuideColor.a, 0.001f);

            GrowLensSettings.ResetHierarchyGuideColor();
            GrowLensSettings.ReloadForTests();

            Assert.AreEqual(theme.r, GrowLensSettings.HierarchyGuideColor.r, 0.001f);
            Assert.AreEqual(0.32f, GrowLensSettings.HierarchyGuideColor.a, 0.001f);
        }

        [Test]
        public void PreferencesPage_IsNamedLensUnderGrow() {
            var provider = GrowLensSettingsProvider.Create();
            Assert.AreEqual("Lens", provider.label);
            Assert.AreEqual("Preferences/Grow/Lens", GrowLensSettingsProvider.ProviderPath);
            Assert.IsInstanceOf<GrowSettingsPageBase>(provider);
        }

        [Test]
        public void IsFeatureEnabled_RequiresTheMasterSwitch() {
            GrowLensSettings.SetFeature(GrowLensFeature.Toolbar, false);
            Assert.IsFalse(GrowLensSettings.IsFeatureEnabled(GrowLensFeature.Toolbar));

            GrowLensSettings.SetFeature(GrowLensFeature.Toolbar, true);
            Assert.IsFalse(GrowLensSettings.IsFeatureEnabled(GrowLensFeature.Toolbar));

            GrowLensSettings.Enabled = true;
            Assert.IsTrue(GrowLensSettings.IsFeatureEnabled(GrowLensFeature.Toolbar));
        }

        [Test]
        public void SessionDisable_DoesNotPersistTheMasterSwitch() {
            GrowLensSettings.Enabled = true;
            GrowLensSettings.DisableForSession();
            Assert.IsFalse(GrowLensSettings.Enabled);

            // An unrelated edit must not write the session-only switch to disk.
            GrowLensSettings.SetFeature(GrowLensFeature.Toolbar, false);
            GrowLensSettings.ReloadForTests();

            Assert.IsTrue(GrowLensSettings.Enabled);
        }

        [Test]
        public void MissingFile_ReportsMissingAndDoesNotCreateTheFile() {
            Assert.AreEqual(GrowLensSettingsStatus.Missing, GrowLensSettings.Status);
            Assert.IsTrue(GrowLensSettings.CanSave);
            Assert.IsFalse(File.Exists(_path));
        }

        [Test]
        public void FirstCommit_CreatesTheFileAndReportsOk() {
            GrowLensSettings.SetFeature(GrowLensFeature.Toolbar, false);

            Assert.AreEqual(GrowLensSettingsStatus.Ok, GrowLensSettings.Status);
            Assert.IsTrue(File.Exists(_path));
            StringAssert.Contains("\"SchemaVersion\": 5", File.ReadAllText(_path));
        }

        [Test]
        public void Apply_CommitsTheNativePageDataOnce() {
            var before = GrowLensSettings.SaveCountForTests;
            var candidate = GrowLensSettings.Snapshot;
            candidate.Enabled = true;
            candidate.Toolbar = false;
            candidate.HierarchyGuideDashed = false;

            GrowLensSettings.Apply(candidate);

            Assert.AreEqual(before + 1, GrowLensSettings.SaveCountForTests);
            Assert.IsTrue(GrowLensSettings.Enabled);
            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.Toolbar));
            Assert.IsFalse(GrowLensSettings.HierarchyGuideDashed);
        }

        [Test]
        public void GuideStyle_StaysEditableWhileTheMasterSwitchIsOff() {
            Assert.IsFalse(GrowLensSettings.Enabled);

            GrowLensSettings.HierarchyGuideDashed = false;
            GrowLensSettings.SetHierarchyGuideColor(new Color(1f, 0f, 0f, 0.5f));

            Assert.IsFalse(GrowLensSettings.HierarchyGuideDashed);
            Assert.IsTrue(GrowLensSettings.HierarchyGuideColorCustomized);
            Assert.AreEqual(0.5f, GrowLensSettings.HierarchyGuideColor.a, 0.001f);
        }

        [Test]
        public void NewerSchema_IsReadOnlyAndKeepsItsValues() {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path,
                "{\"SchemaVersion\":99,\"Enabled\":true,\"Toolbar\":false}");
            GrowLensSettings.ReloadForTests();

            Assert.AreEqual(GrowLensSettingsStatus.NewerSchema, GrowLensSettings.Status);
            Assert.IsFalse(GrowLensSettings.CanSave);
            Assert.IsTrue(GrowLensSettings.Enabled);

            GrowLensSettings.SetFeature(GrowLensFeature.Toolbar, true);

            Assert.IsFalse(GrowLensSettings.GetFeature(GrowLensFeature.Toolbar));
            StringAssert.Contains("\"SchemaVersion\":99", File.ReadAllText(_path));
        }

        [Test]
        public void ReadOnlyFile_IsReportedAndNotOverwritten() {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{\"SchemaVersion\":4,\"Enabled\":true}");
            var attributes = File.GetAttributes(_path);
            File.SetAttributes(_path, attributes | FileAttributes.ReadOnly);
            try {
                GrowLensSettings.ReloadForTests();

                Assert.AreEqual(GrowLensSettingsStatus.ReadOnly, GrowLensSettings.Status);
                Assert.IsFalse(GrowLensSettings.CanSave);

                GrowLensSettings.SetFeature(GrowLensFeature.Toolbar, false);

                Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.Toolbar));
            } finally {
                File.SetAttributes(_path, attributes);
            }
        }

        [Test]
        public void ResetToDefaults_RestoresEveryValueInOneWrite() {
            GrowLensSettings.Enabled = true;
            GrowLensSettings.SetHierarchyFeatures(false);
            GrowLensSettings.HierarchyGuideDashed = false;
            GrowLensSettings.SetHierarchyGuideColor(new Color(1f, 0f, 0f, 1f));
            var before = GrowLensSettings.SaveCountForTests;

            GrowLensSettings.ResetToDefaults();

            Assert.AreEqual(before + 1, GrowLensSettings.SaveCountForTests);
            Assert.IsFalse(GrowLensSettings.Enabled);
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.HierarchySeparators));
            Assert.IsTrue(GrowLensSettings.HierarchyGuideDashed);
            Assert.IsFalse(GrowLensSettings.HierarchyGuideColorCustomized);
        }

        [Test]
        public void ResetToDefaults_DoesNothingWhenTheFileCannotBeSaved() {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{ not json");
            LogAssert.Expect(LogType.Warning, new Regex("Could not read"));
            GrowLensSettings.ReloadForTests();
            var before = GrowLensSettings.SaveCountForTests;

            GrowLensSettings.ResetToDefaults();

            Assert.AreEqual(before, GrowLensSettings.SaveCountForTests);
            Assert.AreEqual("{ not json", File.ReadAllText(_path));
        }

        [Test]
        public void WriteFailure_KeepsThePreviousValues() {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "placeholder");
            GrowLensSettings.SetPathForTests(Path.Combine(_path, "Lens.json"));
            Assert.AreEqual(GrowLensSettingsStatus.Missing, GrowLensSettings.Status);

            LogAssert.Expect(LogType.Error, new Regex("Could not write"));
            GrowLensSettings.SetFeature(GrowLensFeature.Toolbar, false);

            Assert.AreEqual(GrowLensSettingsStatus.WriteFailed, GrowLensSettings.Status);
            Assert.IsTrue(GrowLensSettings.GetFeature(GrowLensFeature.Toolbar));
            Assert.AreEqual("placeholder", File.ReadAllText(_path));

            GrowLensSettings.SetPathForTests(_path);
        }
    }
}
