// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Settings {
    /// <summary>Preferences page for the optional Grow Lens editor enhancements.</summary>
    internal sealed class GrowLensSettingsProvider : SettingsProvider {
        internal const string ProviderPath = "Preferences/Grow/Lens";

        private GrowLensSettingsProvider(string path, SettingsScope scope) : base(path, scope) {
        }

        /// <summary>Registers the page under <b>Preferences &gt; Grow &gt; Lens</b>.</summary>
        [SettingsProvider]
        public static SettingsProvider Create() {
            return new GrowLensSettingsProvider(ProviderPath, SettingsScope.User) {
                label = "Grow Lens",
                keywords = new[] { "Grow", "Lens", "Hierarchy", "Inspector", "Context Menu" }
            };
        }

        /// <inheritdoc />
        public override void OnGUI(string searchContext) {
            EditorGUILayout.LabelField("Grow Lens", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Optional editor productivity tools. Disabled by default; the setting is stored per " +
                "project under UserSettings/Grow/Lens.json.",
                MessageType.Info);

            EditorGUILayout.Space(8);
            var enabled = EditorGUILayout.Toggle("Enable editor enhancements", GrowLensSettings.Enabled);
            if (enabled != GrowLensSettings.Enabled) {
                GrowLensSettings.Enabled = enabled;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Features", EditorStyles.boldLabel);
            DrawFeature("Hierarchy component icons (click opens focused Inspector)", enabled);
            DrawFeature("Hierarchy separators (##RRGGBB[#RRGGBB]Text)", enabled);
            DrawFeature("Hierarchy indent guides", enabled);
            DrawFeature("Asset header path / GUID / Rider actions", enabled);
            DrawFeature("Project and Hierarchy context menus", enabled);
            DrawFeature("Toolbar Grow menu", enabled);
        }

        private static void DrawFeature(string label, bool enabled) {
            using (new EditorGUI.DisabledScope(true)) {
                EditorGUILayout.ToggleLeft(label, enabled);
            }
        }
    }
}
