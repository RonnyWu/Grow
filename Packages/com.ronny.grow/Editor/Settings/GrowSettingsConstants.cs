// SPDX-License-Identifier: MIT

namespace Grow.Editor.Settings {
    /// <summary>
    /// Paths and search keywords shared by the Grow editor settings pages.
    /// </summary>
    internal static class GrowSettingsConstants {
        /// <summary>Settings window path for the project-scoped page.</summary>
        public const string ProjectProviderPath = "Project/Grow";

        /// <summary>Settings window path for the user-scoped page.</summary>
        public const string UserProviderPath = "Preferences/Grow";

        /// <summary>Project-relative directory reserved for team-shared settings assets.</summary>
        public const string ProjectSettingsDirectory = "ProjectSettings/Grow";

        /// <summary>Project-relative directory reserved for local-only settings assets.</summary>
        public const string UserSettingsDirectory = "UserSettings/Grow";

        /// <summary>Project-relative path reserved for the project settings asset.</summary>
        public const string ProjectSettingsAssetPath =
            ProjectSettingsDirectory + "/GrowProjectSettings.asset";

        /// <summary>Project-relative path reserved for the user settings asset.</summary>
        public const string UserSettingsAssetPath =
            UserSettingsDirectory + "/GrowUserSettings.asset";

        /// <summary>Package-relative path to the user-facing documentation page.</summary>
        public const string DocumentationPath = "Packages/com.ronny.grow/Documentation~/index.md";

        /// <summary>Search keywords shown by the Settings window.</summary>
        public static readonly string[] Keywords = { "grow", "framework", "kernel" };
    }
}
