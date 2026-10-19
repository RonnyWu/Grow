// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Inspector.Drawer {
    /// <summary>
    /// The Inspector drawer for <see cref="GrowLabelAttribute"/>.
    /// <para>Overrides the default label with the attribute's display name before drawing; when the attribute's
    /// Label is empty it falls back to the field's displayName, and fields without the attribute behave exactly as
    /// before.</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(GrowLabelAttribute))]
    public class LabelDrawer : PropertyDrawer {
        /// <summary>The height matches an ordinary field (composite/nested fields keep their native
        /// foldout height).</summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUI.GetPropertyHeight(property, label, property.hasChildren);
        }

        /// <summary>Overrides the default label with the attribute's Label, then draws the field natively.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            ApplyDisplayLabel(property, label, (GrowLabelAttribute)attribute);
            EditorGUI.PropertyField(position, property, label, property.hasChildren);
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Writes the attribute's Label into the GUIContent.
        /// <para>Only overrides when the label already has default text (Unity always supplies one when drawing a
        /// field); an empty-label case (<see cref="GUIContent.none"/>) is left untouched.</para>
        /// <para>When the attribute's Label is empty it falls back to
        /// <see cref="SerializedProperty.displayName"/>.</para>
        /// </summary>
        /// <param name="property">The target property (used for the displayName fallback).</param>
        /// <param name="label">The GUIContent to override (modified in place).</param>
        /// <param name="labelAttribute">The Label attribute instance.</param>
        private static void ApplyDisplayLabel(SerializedProperty property, GUIContent label, GrowLabelAttribute labelAttribute) {
            if (label == null || label == GUIContent.none) {
                return;
            }

            if (string.IsNullOrEmpty(label.text)) {
                return;
            }

            var labelText = labelAttribute.Label;
            label.text = string.IsNullOrEmpty(labelText) ? property.displayName : labelText;
        }
    }
}
