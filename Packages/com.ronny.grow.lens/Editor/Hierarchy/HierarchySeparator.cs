// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>
    /// Renders a GameObject whose name starts with <c>##</c> as a colored Hierarchy section
    /// header. This is display-only: it never moves children or edits the GameObject.
    /// </summary>
    internal static class HierarchySeparator {
        private static readonly Color DarkText = new Color32(0x1B, 0x1B, 0x1B, 0xFF);
        private static readonly Color LightText = new Color32(0xF5, 0xF5, 0xF5, 0xFF);
        private static readonly Color SelectionMarker = new Color32(0x3D, 0x7E, 0xE6, 0xFF);
        private static readonly Dictionary<string, SeparatorSpec?> Cache = new();
        private static GUIStyle _labelStyle;

        internal readonly struct SeparatorSpec {
            internal readonly Color? Background;
            internal readonly Color? TextColor;
            internal readonly string DisplayText;

            internal SeparatorSpec(Color? background, Color? textColor, string displayText) {
                Background = background;
                TextColor = textColor;
                DisplayText = displayText;
            }
        }

        /// <summary>Draws the row when it is a separator. Returns false for normal rows.</summary>
        internal static bool TryDraw(int instanceID, GameObject gameObject, Rect rect) {
            if (gameObject == null || !TryGetSpec(gameObject.name, out var spec)) {
                return false;
            }

            if (Event.current == null || Event.current.type != EventType.Repaint) {
                return true;
            }

            var width = Mathf.Max(0f, rect.width);
            var band = new Rect(rect.x, rect.y, width, rect.height);
            if (Selection.Contains(instanceID)) {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, Mathf.Min(3f, width), rect.height),
                    SelectionMarker);
                return true;
            }

            var background = spec.Background ?? ThemeBackground;
            EditorGUI.DrawRect(band, background);
            if (!string.IsNullOrEmpty(spec.DisplayText)) {
                var textColor = spec.TextColor ?? PickTextColor(background);
                var previous = GUI.contentColor;
                GUI.contentColor = textColor;
                GUI.Label(band, spec.DisplayText, LabelStyle);
                GUI.contentColor = previous;
            }

            return true;
        }

        internal static bool IsSeparator(string name) {
            return TryGetSpec(name, out _);
        }

        internal static void OnHierarchyChanged() {
            Cache.Clear();
        }

        internal static bool TryGetSpec(string name, out SeparatorSpec spec) {
            spec = default;
            if (string.IsNullOrEmpty(name)) {
                return false;
            }

            if (Cache.TryGetValue(name, out var cached)) {
                if (!cached.HasValue) {
                    return false;
                }

                spec = cached.Value;
                return true;
            }

            if (!TryParse(name, out spec)) {
                Cache[name] = null;
                return false;
            }

            Cache[name] = spec;
            return true;
        }

        internal static bool TryParse(string name, out SeparatorSpec spec) {
            spec = default;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("##", StringComparison.Ordinal)) {
                return false;
            }

            var index = 2;
            Color? background = null;
            if (index < name.Length && name[index] == '-') {
                index++;
            } else if (!TryReadColor(name, ref index, out var parsedBackground)) {
                return false;
            } else {
                background = parsedBackground;
            }

            Color? textColor = null;
            if (index < name.Length && name[index] == '#') {
                index++;
                if (!TryReadColor(name, ref index, out var parsedTextColor)) {
                    return false;
                }

                textColor = parsedTextColor;
            }

            spec = new SeparatorSpec(background, textColor, name.Substring(index).Trim());
            return true;
        }

        private static bool TryReadColor(string name, ref int index, out Color color) {
            color = default;
            var start = index;
            var count = 0;
            while (index < name.Length && count < 6 && IsHexDigit(name[index])) {
                index++;
                count++;
            }

            if (count != 3 && count != 6) {
                return false;
            }

            var value = 0;
            for (var i = 0; i < count; i++) {
                value = value << 4 | HexValue(name[start + i]);
            }

            if (count == 3) {
                var r = (value >> 8) & 0xF;
                var g = (value >> 4) & 0xF;
                var b = value & 0xF;
                color = new Color32((byte)(r * 17), (byte)(g * 17), (byte)(b * 17), 0xFF);
            } else {
                color = new Color32((byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF),
                    (byte)(value & 0xFF), 0xFF);
            }

            return true;
        }

        private static bool IsHexDigit(char value) {
            return value >= '0' && value <= '9' || value >= 'a' && value <= 'f' ||
                value >= 'A' && value <= 'F';
        }

        private static int HexValue(char value) {
            if (value >= '0' && value <= '9') {
                return value - '0';
            }

            if (value >= 'a' && value <= 'f') {
                return value - 'a' + 10;
            }

            return value - 'A' + 10;
        }

        private static Color ThemeBackground {
            get {
                return EditorGUIUtility.isProSkin
                    ? new Color32(0x38, 0x38, 0x38, 0xFF)
                    : new Color32(0xC8, 0xC8, 0xC8, 0xFF);
            }
        }

        private static Color PickTextColor(Color background) {
            var luminance = 0.2126f * ToLinear(background.r) + 0.7152f * ToLinear(background.g) +
                0.0722f * ToLinear(background.b);
            return luminance > 0.18f ? DarkText : LightText;
        }

        private static float ToLinear(float value) {
            return value <= 0.04045f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        private static GUIStyle LabelStyle {
            get {
                if (_labelStyle != null) {
                    return _labelStyle;
                }

                _labelStyle = new GUIStyle(EditorStyles.boldLabel) {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(6, 6, 0, 0)
                };
                _labelStyle.normal.textColor = Color.white;
                return _labelStyle;
            }
        }
    }
}
