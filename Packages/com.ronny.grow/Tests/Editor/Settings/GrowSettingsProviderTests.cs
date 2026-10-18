// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using Grow.Editor.Localization;
using Grow.Editor.Settings.Preferences;
using Grow.Editor.Settings.Project;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Grow.Tests.Editor.Settings {
    public class GrowSettingsProviderTests {
        private string _root;

        [SetUp]
        public void SetUp() {
            _root = Path.Combine(
                Path.GetTempPath(), "GrowProviderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            GrowEditorPreferencesStore.SetPathForTests(
                Path.Combine(_root, "EditorPreferences.json"));
            GrowLocalization.Reload();
        }

        [TearDown]
        public void TearDown() {
            GrowEditorPreferencesStore.Store.Flush();
            GrowEditorPreferencesStore.ResetPathForTests();
            GrowLocalization.Reload();

            if (Directory.Exists(_root)) {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void ProjectProvider_UsesTheProjectScopeAndPath() {
            var provider = GrowProjectSettingsProvider.Create();

            Assert.AreEqual(GrowProjectSettingsProvider.ProviderPath, provider.settingsPath);
            Assert.AreEqual(SettingsScope.Project, provider.scope);
            Assert.IsFalse(string.IsNullOrEmpty(provider.label));
            Assert.AreNotEqual(GrowSettingsKeys.ProjectGeneralTitle, provider.label);
        }

        [Test]
        public void UserProvider_UsesTheUserScopeAndPath() {
            var provider = GrowUserSettingsProvider.Create();

            Assert.AreEqual(GrowUserSettingsProvider.ProviderPath, provider.settingsPath);
            Assert.AreEqual(SettingsScope.User, provider.scope);
            Assert.IsFalse(string.IsNullOrEmpty(provider.label));
            Assert.AreNotEqual(GrowSettingsKeys.PreferencesGeneralTitle, provider.label);
        }

        [Test]
        public void Providers_UseDistinctNonNestedPaths() {
            var providers = TypeCache.GetMethodsWithAttribute<SettingsProviderAttribute>()
                .Where(method =>
                    method.DeclaringType?.Assembly == typeof(GrowProjectSettingsProvider).Assembly)
                .Select(method => (SettingsProvider)method.Invoke(null, null))
                .ToArray();

            Assert.IsNotEmpty(providers);
            Assert.IsTrue(
                providers.GroupBy(provider => (provider.scope, provider.settingsPath))
                    .All(group => group.Count() == 1),
                "A settings path is registered more than once.");

            var paths = providers.Select(provider => provider.settingsPath).ToArray();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    GrowProjectSettingsProvider.ProviderPath,
                    GrowUserSettingsProvider.ProviderPath
                },
                paths);

            foreach (var path in paths) {
                foreach (var other in paths) {
                    if (path == other) {
                        continue;
                    }

                    Assert.IsFalse(
                        other.StartsWith(path + "/", StringComparison.Ordinal),
                        $"Settings path '{other}' is nested under the leaf '{path}'.");
                }
            }
        }

        [Test]
        public void Providers_ExposeTheGrowSearchKeyword() {
            var project = GrowProjectSettingsProvider.Create();
            var user = GrowUserSettingsProvider.Create();

            CollectionAssert.Contains(project.keywords, "grow");
            CollectionAssert.Contains(user.keywords, "grow");
            Assert.Greater(user.keywords.Count(), 1);
        }

        [Test]
        public void PackageInfo_ResolvesForTheEditorAssembly() {
            var package = PackageInfo.FindForAssembly(
                typeof(GrowProjectSettingsProvider).Assembly);

            Assert.IsNotNull(package);
            Assert.AreEqual(GrowProjectSettingsProvider.PackageName, package.name);
        }
    }
}
