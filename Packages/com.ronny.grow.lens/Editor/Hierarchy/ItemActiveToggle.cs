// SPDX-License-Identifier: MIT

using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>Draws and handles the optional GameObject active toggle at the right edge of a row.</summary>
    internal static class ItemActiveToggle {
        internal const float ToggleSize = 16f;

        internal const float RightMargin = 2f;

        /// <summary>Horizontal space reserved from the right edge for the toggle.</summary>
        internal const float ReservedWidth = ToggleSize + RightMargin;

        internal static void OnGameObjectGUI(GameObject gameObject, Rect rowRect) {
            if (gameObject == null || Event.current == null ||
                Event.current.type != EventType.Repaint) {
                return;
            }

            EditorGUI.Toggle(GetRect(rowRect), gameObject.activeSelf);
        }

        /// <summary>Returns true when the event is a plain left click on the active toggle.</summary>
        internal static bool TryHandleClick(GameObject gameObject, Rect rowRect) {
            var current = Event.current;
            if (gameObject == null || current == null || current.type != EventType.MouseDown ||
                current.button != 0 || current.control || current.shift || current.alt ||
                current.command || !GetRect(rowRect).Contains(current.mousePosition)) {
                return false;
            }

            Undo.RecordObject(gameObject, "Toggle Active");
            gameObject.SetActive(!gameObject.activeSelf);
            EditorUtility.SetDirty(gameObject);
            current.Use();
            return true;
        }

        internal static Rect GetRect(Rect rowRect) {
            return new Rect(rowRect.xMax - RightMargin - ToggleSize,
                rowRect.y + (rowRect.height - ToggleSize) * 0.5f, ToggleSize, ToggleSize);
        }
    }
}
