// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Inspector.Drawer {
    /// <summary>
    /// The Inspector drawer for <see cref="GrowReadOnlyAttribute"/>: draws the field in a disabled state so it can
    /// be inspected but not edited.
    /// <para>It only wraps <see cref="EditorGUI.PropertyField(Rect, SerializedProperty, GUIContent, bool)"/>, so its
    /// height and foldout behavior match an ordinary field; composite/nested fields are therefore read-only as a
    /// whole too.</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(GrowReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer {
        /// <summary>The height matches an ordinary field (composite/nested fields keep their native
        /// foldout height).</summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUI.GetPropertyHeight(property, label, property.hasChildren);
        }

        /// <summary>Draws the field inside a disabled group so it stays visible but read-only.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.PropertyField(position, property, label, property.hasChildren);
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndProperty();
        }
    }
}
