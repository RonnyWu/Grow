// SPDX-License-Identifier: MIT

using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>Draws vertical dashed guides at each Hierarchy indent level.</summary>
    internal static class ItemIndentLine {
        private const float IndentStartX = 38f;
        private const float IndentInterval = 14f;
        private const float IndentWidth = 1f;
        private const int IndentDashTextureHeight = 16;
        private const int IndentDashTextureWidth = 1;

        private static GUIStyle _indentDashStyle;

        internal static void OnGameObjectGUI(Rect rect) {
            EnsureIndentStyle();
            if (_indentDashStyle == null) {
                return;
            }

            for (var x = IndentStartX; x < rect.x; x += IndentInterval) {
                var lineRect = new Rect(x, rect.y, IndentWidth, rect.height);
                GUI.Box(lineRect, GUIContent.none, _indentDashStyle);
            }
        }

        private static void EnsureIndentStyle() {
            if (_indentDashStyle != null) {
                return;
            }

            var color = EditorStyles.label.normal.textColor;
            color.a = 0.32f;
            var pixels = new Color[IndentDashTextureHeight];
            var clear = new Color(0, 0, 0, 0);
            var texture = new Texture2D(IndentDashTextureWidth, IndentDashTextureHeight, TextureFormat.RGBA32,
                false) {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (var y = 0; y < IndentDashTextureHeight; y++) {
                var solid = y / 2 % 2 == 0;
                pixels[y] = solid ? color : clear;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            _indentDashStyle = new GUIStyle { normal = { background = texture } };
        }
    }
}
