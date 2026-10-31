// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Localization;
using Grow.Lens.Editor.Menus;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>Small modal editor for creating a formatted Hierarchy separator.</summary>
    internal sealed class GrowSeparatorWindow : EditorWindow {
        private enum InsertPosition {
            Above,
            Below
        }

        private static GUIStyle _previewStyle;

        private string _title;
        private Color _background;
        private bool _automaticText = true;
        private Color _textColor;
        private InsertPosition _position;
        private GameObject _target;

        internal static void Show(GameObject target) {
            var window = CreateInstance<GrowSeparatorWindow>();
            window.titleContent = new GUIContent(Text("grow.lens.separator.title"));
            window._target = target != null && !EditorUtility.IsPersistent(target) ? target : null;
            window._title = Text("grow.lens.separator.defaultText");
            window._background = HierarchySeparator.DefaultBackground;
            window._textColor = HierarchySeparator.PickTextColor(window._background);
            window.minSize = new Vector2(360f, 250f);
            window.ShowModalUtility();
        }

        private void OnGUI() {
            EditorGUILayout.Space(4f);
            _title = EditorGUILayout.TextField(
                new GUIContent(Text("grow.lens.separator.text")), _title);
            _background = EditorGUILayout.ColorField(
                new GUIContent(Text("grow.lens.separator.background")), _background,
                true, false, false);

            _automaticText = EditorGUILayout.Toggle(
                new GUIContent(Text("grow.lens.separator.automaticText")), _automaticText);
            using (new EditorGUI.DisabledScope(_automaticText)) {
                _textColor = EditorGUILayout.ColorField(
                    new GUIContent(Text("grow.lens.separator.textColor")), _textColor,
                    true, false, false);
            }

            if (_target == null) {
                EditorGUILayout.LabelField(
                    new GUIContent(Text("grow.lens.separator.position")),
                    new GUIContent(Text("grow.lens.separator.root")));
            } else {
                _position = (InsertPosition)EditorGUILayout.Popup(
                    new GUIContent(Text("grow.lens.separator.position")), (int)_position,
                    new[] {
                        Text("grow.lens.separator.above"),
                        Text("grow.lens.separator.below")
                    });
            }

            DrawPreview();
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(Text("grow.lens.separator.cancel"), GUILayout.Width(80f))) {
                    Close();
                }

                if (GUILayout.Button(Text("grow.lens.separator.create"), GUILayout.Width(100f))) {
                    Create();
                }
            }
        }

        private void DrawPreview() {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(Text("grow.lens.separator.preview"), EditorStyles.miniLabel);
            var rect = EditorGUILayout.GetControlRect(false, 28f);
            EditorGUI.DrawRect(rect, _background);

            var previous = GUI.contentColor;
            GUI.contentColor = _automaticText
                ? HierarchySeparator.PickTextColor(_background)
                : _textColor;
            GUI.Label(rect, _title, PreviewStyle);
            GUI.contentColor = previous;
        }

        private void Create() {
            var textColor = _automaticText ? (Color?)null : _textColor;
            var name = HierarchySeparator.BuildName(_title, _background, textColor);
            GrowMenuActions.CreateHierarchySeparator(
                name, _target, _position == InsertPosition.Below);
            Close();
        }

        private static GUIStyle PreviewStyle {
            get {
                if (_previewStyle == null) {
                    _previewStyle = new GUIStyle(EditorStyles.boldLabel) {
                        alignment = TextAnchor.MiddleCenter,
                        clipping = TextClipping.Clip
                    };
                    // contentColor tints the style color; force white so the preview shows the
                    // same opaque text color as the Hierarchy row in both editor skins.
                    _previewStyle.normal.textColor = Color.white;
                }

                return _previewStyle;
            }
        }

        private static string Text(string key) {
            return GrowLensLocalization.Get(key);
        }
    }
}
