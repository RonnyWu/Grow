// SPDX-License-Identifier: MIT

using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Settings.Project {
    /// <summary>
    /// Read-only <b>Project Settings &gt; Grow</b> page.
    /// </summary>
    /// <remarks>
    /// The page deliberately exposes no project-level switch yet. A setting is added only when a
    /// feature consumes it; until then the page reports the installed package identity.
    /// </remarks>
    internal sealed class GrowProjectSettingsProvider : GrowSettingsProviderBase {
        private GrowProjectSettingsProvider(string path, SettingsScope scope) : base(path, scope) {
        }

        /// <summary>Registers the page under <b>Project Settings &gt; Grow</b>.</summary>
        [SettingsProvider]
        public static SettingsProvider Create() {
            return new GrowProjectSettingsProvider(
                GrowSettingsConstants.ProjectProviderPath, SettingsScope.Project) {
                label = "Grow",
                keywords = GrowSettingsConstants.Keywords
            };
        }

        /// <inheritdoc />
        protected override void DrawContent(string searchContext) {
            EditorGUILayout.LabelField("Grow", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This page is read-only. Project-level settings are added only when a feature " +
                "consumes them.", MessageType.Info);

            EditorGUILayout.Space(8);

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(GrowProjectSettingsProvider).Assembly);
            if (package == null) {
                EditorGUILayout.HelpBox(
                    "Could not resolve the com.ronny.grow package information.",
                    MessageType.Warning);
                return;
            }

            DrawValue("Package", package.name);
            DrawValue("Display name", package.displayName);
            DrawValue("Version", package.version);
            DrawValue("Documentation", GrowSettingsConstants.DocumentationPath);

            if (!string.IsNullOrEmpty(package.description)) {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(package.description, EditorStyles.wordWrappedLabel);
            }
        }

        private static void DrawValue(string label, string value) {
            EditorGUILayout.LabelField(label, string.IsNullOrEmpty(value) ? "-" : value);
        }
    }
}
