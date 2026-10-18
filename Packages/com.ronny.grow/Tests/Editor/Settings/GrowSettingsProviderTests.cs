// SPDX-License-Identifier: MIT

using Grow.Editor.Settings;
using Grow.Editor.Settings.Preferences;
using Grow.Editor.Settings.Project;
using NUnit.Framework;
using UnityEditor;

namespace Grow.Tests.Editor.Settings {
    public class GrowSettingsProviderTests {
        [Test]
        public void ProjectProvider_UsesTheProjectScopeAndPath() {
            var provider = GrowProjectSettingsProvider.Create();

            Assert.AreEqual(GrowSettingsConstants.ProjectProviderPath, provider.settingsPath);
            Assert.AreEqual(SettingsScope.Project, provider.scope);
            Assert.AreEqual("Grow", provider.label);
        }

        [Test]
        public void PackageInfo_ResolvesForTheEditorAssembly() {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(GrowProjectSettingsProvider).Assembly);

            Assert.IsNotNull(package);
            Assert.AreEqual("com.ronny.grow", package.name);
        }

        [Test]
        public void UserProvider_UsesTheUserScopeAndPath() {
            var provider = GrowUserSettingsProvider.Create();

            Assert.AreEqual(GrowSettingsConstants.UserProviderPath, provider.settingsPath);
            Assert.AreEqual(SettingsScope.User, provider.scope);
            Assert.AreEqual("Grow", provider.label);
        }
    }
}
