// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Inspector.Gui {
    /// <summary>
    /// A trimmed-down IMGUI layout toolkit for hand-written full Inspectors:
    /// foldouts / cards / indentation / separators / label-width scopes.
    /// <para>It mirrors the capabilities of com.grow.lens's legacy GrowEditorUtil by name, but keeps only the subset
    /// the annotation framework needs, so assemblies that cannot reference lens (com.grow.core / com.grow.flow) can
    /// use it.</para>
    /// </summary>
    internal static class LayoutGui {

        private static GUIStyle _foldoutHeaderStyle;
        private static GUIStyle _cardTitleStyle;

        /// <summary>The foldout header style (bold, based on EditorStyles.foldout).</summary>
        private static GUIStyle FoldoutHeaderStyle => _foldoutHeaderStyle ??= new GUIStyle(EditorStyles.foldout) {
            fontStyle = FontStyle.Bold
        };

        /// <summary>The card title style (bold, size 12, based on EditorStyles.label).</summary>
        private static GUIStyle CardTitleStyle => _cardTitleStyle ??= new GUIStyle(EditorStyles.label) {
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };

        /// <summary>A foldout header (toggle only; no content).</summary>
        /// <param name="foldout">The current foldout state.</param>
        /// <param name="title">The title text.</param>
        /// <returns>The latest foldout state.</returns>
        public static bool DrawFoldoutHeader(bool foldout, string title) {
            return EditorGUILayout.Foldout(foldout, title, true, FoldoutHeaderStyle);
        }

        /// <summary>A foldout whose content is indented when expanded; returns the latest foldout state.</summary>
        /// <param name="foldout">The current foldout state.</param>
        /// <param name="title">The title text.</param>
        /// <param name="content">The content drawn when expanded.</param>
        /// <param name="indentContent">Whether to indent the expanded content by one level.</param>
        /// <returns>The latest foldout state.</returns>
        public static bool DrawFoldout(bool foldout, string title, Action content, bool indentContent = true) {
            foldout = DrawFoldoutHeader(foldout, title);
            if (foldout && content != null) {
                if (indentContent) {
                    DrawIndent(content);
                } else {
                    content();
                }
            }

            return foldout;
        }

        /// <summary>A helpBox-style card container: optional title plus content.</summary>
        /// <param name="title">The card title (not drawn when empty).</param>
        /// <param name="content">The card content.</param>
        public static void DrawCard(string title, Action content) {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (!string.IsNullOrEmpty(title)) {
                EditorGUILayout.LabelField(title, CardTitleStyle);
            }

            content?.Invoke();
            EditorGUILayout.EndVertical();
        }

        /// <summary>A helpBox-style card container where the title is drawn by
        /// <paramref name="title"/> itself.</summary>
        /// <param name="title">The title drawing action.</param>
        /// <param name="content">The card content.</param>
        public static void DrawCard(Action title, Action content) {
            DrawCard(EditorStyles.helpBox, title, content);
        }

        /// <summary>A card container with an explicit style, where the title is drawn by
        /// <paramref name="title"/> itself.</summary>
        /// <param name="style">The card style (such as helpBox / ButtonGui.CardStyle).</param>
        /// <param name="title">The title drawing action.</param>
        /// <param name="content">The card content.</param>
        public static void DrawCard(GUIStyle style, Action title, Action content) {
            EditorGUILayout.BeginVertical(style);
            title?.Invoke();
            content?.Invoke();
            EditorGUILayout.EndVertical();
        }

        /// <summary>Indents by one level while running the content (via EditorGUI.indentLevel, so
        /// both GUILayout and rect drawing are affected).</summary>
        /// <param name="content">The content to run.</param>
        public static void DrawIndent(Action content) {
            EditorGUI.indentLevel++;
            try {
                content?.Invoke();
            } finally {
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>Draws a horizontal separator line.</summary>
        /// <param name="height">The line height (1px by default).</param>
        /// <param name="color">The line color; defaults to a semi-transparent neutral tone chosen
        /// for the light or dark theme.</param>
        public static void DrawHorizontalLine(float height = 1f, Color? color = null) {
            var rect = EditorGUILayout.GetControlRect(false, Mathf.Max(8f, height + 6f));
            rect.y += (rect.height - height) * 0.5f;
            rect.height = Mathf.Max(1f, height);
            var prev = GUI.color;
            GUI.color = color ?? (EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.18f) : new Color(0f, 0f, 0f, 0.22f));
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>Runs the drawing within a specified field label width.</summary>
        /// <param name="labelWidth">The label width.</param>
        /// <param name="action">The drawing to run.</param>
        public static void DrawInLabelWidthScope(float labelWidth, Action action) {
            using (new LabelWidthScope(labelWidth)) {
                action?.Invoke();
            }
        }

        /// <summary>An IDisposable scope that temporarily changes EditorGUIUtility.labelWidth and
        /// restores it.</summary>
        public readonly struct LabelWidthScope : IDisposable {
            private readonly float _previousWidth;

            /// <summary>Records the old width and applies the new one; Dispose restores it.</summary>
            /// <param name="labelWidth">The new label width.</param>
            public LabelWidthScope(float labelWidth) {
                _previousWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = labelWidth;
            }

            public void Dispose() {
                EditorGUIUtility.labelWidth = _previousWidth;
            }
        }
    }
}
