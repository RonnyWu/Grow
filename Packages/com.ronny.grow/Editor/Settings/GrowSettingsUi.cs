// SPDX-License-Identifier: MIT

using UnityEditor;

namespace Grow.Editor.Settings {
    /// <summary>
    /// Shared IMGUI helpers for Grow settings pages.
    /// </summary>
    internal static class GrowSettingsUi {
        /// <summary>
        /// The label width measured from Unity 2022.3's built-in settings pages. The value comes
        /// from the settings-window USS theme rather than a public editor constant.
        /// </summary>
        public const float MeasuredLabelWidth = 251f;

        /// <summary>Draws a read-only label/value row, using a dash for an empty value.</summary>
        public static void DrawValue(string label, string value) {
            EditorGUILayout.LabelField(label, string.IsNullOrEmpty(value) ? "-" : value);
        }
    }
}
