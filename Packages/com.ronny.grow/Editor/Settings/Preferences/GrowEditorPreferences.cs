// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Grow.Editor.Settings.Preferences {
    /// <summary>
    /// User-global editor preferences, stored outside the project under Unity's Preferences
    /// folder.
    /// </summary>
    /// <remarks>
    /// Language is a personal preference, so it follows the user across projects. The file is
    /// only written after an explicit language change; opening a settings page never creates it.
    /// </remarks>
    [Serializable]
    internal sealed class GrowEditorPreferences : ScriptableObject, IGrowSettings {
        internal const int Schema = 1;

        internal const string SystemLanguage = "system";

        [SerializeField]
        private string format = GrowSettingsFile.FormatMarker;

        [SerializeField]
        private int schemaVersion = Schema;

        [SerializeField]
        private string language = SystemLanguage;

        public string Language {
            get => language;
            set => language = value;
        }

        int IGrowSettings.SchemaVersion {
            get => schemaVersion;
            set => schemaVersion = value;
        }

        int IGrowSettings.CurrentSchema => Schema;

        internal static GrowEditorPreferences CreateDefault() {
            var settings = CreateInstance<GrowEditorPreferences>();
            settings.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
            settings.ResetToDefaults();
            return settings;
        }

        public void ResetToDefaults() {
            format = GrowSettingsFile.FormatMarker;
            schemaVersion = Schema;
            language = SystemLanguage;
        }

        public void Migrate(int fromVersion) {
            schemaVersion = Schema;
        }

        public bool Validate() {
            if (format != GrowSettingsFile.FormatMarker) {
                format = GrowSettingsFile.FormatMarker;
                return true;
            }

            if (!string.IsNullOrEmpty(language)) {
                return false;
            }

            language = SystemLanguage;
            return true;
        }
    }

    /// <summary>Owns the user-global editor preferences file.</summary>
    internal static class GrowEditorPreferencesStore {
        private const string RelativePath = "Grow/EditorPreferences.json";

        private static GrowSettingsStore<GrowEditorPreferences> _settingsStore;

        private static string _pathOverride;

        public static GrowSettingsStore<GrowEditorPreferences> Store =>
            _settingsStore ??= new GrowSettingsStore<GrowEditorPreferences>(
                _pathOverride ?? GrowSettingsFile.ToPreferencesPath(RelativePath),
                GrowEditorPreferences.CreateDefault);

        public static GrowEditorPreferences Instance => Store.Instance;

        /// <summary>Redirects tests away from the real user preferences file.</summary>
        internal static void SetPathForTests(string absolutePath) {
            _pathOverride = absolutePath;
            _settingsStore = null;
        }

        internal static void ResetPathForTests() {
            _pathOverride = null;
            _settingsStore = null;
        }
    }
}
