// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Settings {
    /// <summary>
    /// Base class for Grow <see cref="SettingsProvider"/> pages in both the project and user
    /// scopes.
    /// </summary>
    /// <remarks>
    /// Unity's built-in settings pages wrap their content in a fixed left margin and a fixed
    /// label width. Custom providers receive neither, so this base reproduces the parts of
    /// <c>SettingsWindow.GUIScope</c> that matter: a 10 px left margin and a 251 px label width.
    /// </remarks>
    internal abstract class GrowSettingsProviderBase : SettingsProvider {
        private const float MarginLeft = 10f;

        /// <summary>
        /// The label width used by Unity 2022.3's built-in Preferences and Project Settings pages.
        /// It does not scale with the window width.
        /// </summary>
        private const float DefaultLabelWidth = 251f;

        protected GrowSettingsProviderBase(string path, SettingsScope scope) : base(path, scope) {
        }

        /// <inheritdoc />
        public sealed override void OnGUI(string searchContext) {
            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = DefaultLabelWidth;

            try {
                GUILayout.BeginHorizontal();
                GUILayout.Space(MarginLeft);
                GUILayout.BeginVertical();

                DrawContent(searchContext);

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            } finally {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        /// <summary>Draws the page body inside the standard margin wrapper.</summary>
        protected abstract void DrawContent(string searchContext);

        /// <summary>
        /// Converts an absolute file-system path inside the project to a project-relative path
        /// starting with <c>Assets/</c>. Paths outside the project are returned unchanged.
        /// </summary>
        protected static string ToProjectRelative(string absolutePath) {
            if (string.IsNullOrEmpty(absolutePath)) {
                return absolutePath;
            }

            var dataPath = Application.dataPath.Replace('\\', '/');
            var normalized = absolutePath.Replace('\\', '/');

            if (normalized.StartsWith(dataPath, StringComparison.Ordinal)) {
                var relative = "Assets" + normalized.Substring(dataPath.Length);
                return relative == "Assets" ? "Assets/" : relative;
            }

            return absolutePath;
        }
    }
}
