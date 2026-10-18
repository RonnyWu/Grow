// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Grow.Editor.Localization;
using Grow.Editor.Settings.Preferences;
using Grow.Editor.Settings.Project;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Grow.Tests.Editor.Settings {
    public class GrowSettingsProviderTests {
        [Test]
        public void ProjectProvider_UsesTheProjectScopeAndPath() {
            var provider = GrowProjectSettingsProvider.Create();

            Assert.AreEqual(GrowProjectSettingsProvider.ProviderPath, provider.settingsPath);
            Assert.AreEqual(SettingsScope.Project, provider.scope);
            Assert.AreEqual(
                GrowLocalization.Get(GrowSettingsKeys.ProjectGeneralTitle), provider.label);
        }

        [Test]
        public void UserProvider_UsesTheUserScopeAndPath() {
            var provider = GrowUserSettingsProvider.Create();

            Assert.AreEqual(GrowUserSettingsProvider.ProviderPath, provider.settingsPath);
            Assert.AreEqual(SettingsScope.User, provider.scope);
            Assert.AreEqual(
                GrowLocalization.Get(GrowSettingsKeys.PreferencesGeneralTitle), provider.label);
        }

        [Test]
        public void Providers_UseDistinctNonNestedPaths() {
            var paths = TypeCache.GetMethodsWithAttribute<SettingsProviderAttribute>()
                .Where(method =>
                    method.DeclaringType?.Assembly == typeof(GrowProjectSettingsProvider).Assembly)
                .Select(method => (SettingsProvider)method.Invoke(null, null))
                .Select(provider => provider.settingsPath)
                .Distinct()
                .ToArray();

            Assert.IsNotEmpty(paths);

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
            CollectionAssert.Contains(GrowProjectSettingsProvider.Create().keywords, "grow");
            CollectionAssert.Contains(GrowUserSettingsProvider.Create().keywords, "grow");
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
