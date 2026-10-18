// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Settings {
    /// <summary>
    /// Applies the standard settings-page margin and label width, and closes the IMGUI layout
    /// groups even when the page body throws.
    /// </summary>
    internal sealed class GrowSettingsGuiScope : IDisposable {
        private const float MarginLeft = 10f;

        private const float MarginTop = 4f;

        private readonly float _previousLabelWidth;

        public GrowSettingsGuiScope() {
            _previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = GrowSettingsUi.MeasuredLabelWidth;

            GUILayout.BeginHorizontal();
            GUILayout.Space(MarginLeft);
            GUILayout.BeginVertical();
            GUILayout.Space(MarginTop);
        }

        public void Dispose() {
            try {
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            } finally {
                EditorGUIUtility.labelWidth = _previousLabelWidth;
            }
        }
    }
}
