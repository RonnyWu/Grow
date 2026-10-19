// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using Grow.Editor.Inspector.EnumFlags;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Inspector.Drawer {
    /// <summary>
    /// The Inspector drawer for <see cref="GrowEnumFlagsAttribute"/>: draws a [Flags] enum field as a "bit-flag
    /// hierarchical multi-select dropdown".
    /// <para>The display data and the bit-level read/write backend are reused from <see cref="EnumFlagsGui"/>; this
    /// class only arranges the field label next to the dropdown control. Non-enum fields (including T[]/List
    /// containers) fall back to the default drawing.</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(GrowEnumFlagsAttribute))]
    public class EnumFlagsDrawer : PropertyDrawer {
        /// <summary>Single-line height for a [GrowEnumFlags] field; non-enum fields / containers
        /// fall back to the native height.</summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            var enumType = EnumFlagsCache.GetEnumType(fieldInfo?.FieldType);
            if (enumType == null || property.propertyType != SerializedPropertyType.Enum) {
                return EditorGUI.GetPropertyHeight(property, label, property.hasChildren);
            }

            return EditorGUIUtility.singleLineHeight;
        }

        /// <summary>Resolves the field's enum type and draws the bit-flag hierarchical
        /// multi-select; falls back to the default drawing when it cannot be resolved.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var enumType = EnumFlagsCache.GetEnumType(fieldInfo?.FieldType);
            if (enumType == null || property.propertyType != SerializedPropertyType.Enum) {
                // A non-enum field (or a container) cannot use the bit-flag drawing, so fall back
                // to the default drawing
                EditorGUI.PropertyField(position, property, label, property.hasChildren);
                return;
            }

            // Standard enum field layout: label on the left, dropdown button on the right; the
            // helper handles BeginProperty.
            var includeCombos = (attribute as GrowEnumFlagsAttribute)?.ShowCombos ?? true;
            EnumFlagsGui.DrawEnumFlagsProperty(position, property, label, enumType, includeCombos);
        }
    }
}
