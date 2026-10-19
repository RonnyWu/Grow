// SPDX-License-Identifier: MIT

using System;
using Grow.Editor.Common.MultiSelect;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Inspector.EnumFlags {
    /// <summary>
    /// The IMGUI entry point for [Flags] enum "bit-flag multi-select" (label plus dropdown assembled uniformly).
    /// <para>Consumed by <c>EnumFlagsDrawer</c> and by GrowEditor's [GrowButton] parameter drawing; the display data
    /// comes from <see cref="EnumFlagsCache"/> and the backend from <see cref="EnumFlagsBackend"/> /
    /// <see cref="EnumValueBackend"/>.</para>
    /// </summary>
    internal static class EnumFlagsGui {
        /// <summary>Creates the bit-flag read/write backend for an enum SerializedProperty.</summary>
        /// <param name="property">The SerializedProperty of the enum type.</param>
        /// <returns>A bit-flag backend bound to that property.</returns>
        public static IMultiSelectBackend CreateBackend(SerializedProperty property) {
            return new EnumFlagsBackend(property);
        }

        /// <summary>
        /// Draws a [Flags] enum property as a "hierarchical multi-select dropdown" (label plus dropdown assembled
        /// uniformly).
        /// <para>Shared by the property-level drawer (EnumFlagsDrawer) and [GrowButton] parameter drawing, keeping a
        /// single drawing path.</para>
        /// </summary>
        /// <param name="position">The drawing rect.</param>
        /// <param name="property">The enum property.</param>
        /// <param name="label">The field label.</param>
        /// <param name="enumType">The enum type.</param>
        /// <param name="includeCombos">Whether to include combination alias rows.</param>
        public static void DrawEnumFlagsProperty(Rect position, SerializedProperty property, GUIContent label, Type enumType, bool includeCombos = true) {
            EditorGUI.BeginProperty(position, label, property);
            var dropdownRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            var cache = EnumFlagsCache.GetOrCreateFlagsCache(enumType, includeCombos);
            var backend = CreateBackend(property);
            HierarchicalMultiSelectGui.DrawDropdown(dropdownRect, backend, cache);
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Draws a [Flags] enum "raw value" as a "hierarchical multi-select dropdown" (EditorGUILayout version for
        /// cases without a SerializedProperty, such as [GrowButton] method parameters). It shares the same cache and
        /// presentation layer as <see cref="DrawEnumFlagsProperty"/>, so fields and parameters are drawn by exactly
        /// the same rules.
        /// <para>The same <paramref name="backend"/> instance is passed in and returned unchanged: the caller must
        /// keep that backend alive so consecutive edits made in the popup survive repaints (see GrowEditor's button
        /// parameter cache).</para>
        /// </summary>
        /// <param name="label">The label text.</param>
        /// <param name="enumType">The enum type.</param>
        /// <param name="backend">The bit-mask backend (owned by the caller, kept across repaints).</param>
        /// <param name="includeCombos">Whether to include combination alias rows.</param>
        /// <returns>The <paramref name="backend"/> passed in, returned unchanged.</returns>
        public static EnumValueBackend DrawEnumFlagsValue(string label, Type enumType, EnumValueBackend backend, bool includeCombos = true) {
            var rect = EditorGUILayout.GetControlRect();
            var dropdownRect = EditorGUI.PrefixLabel(rect, GUIUtility.GetControlID(FocusType.Passive), new GUIContent(label));
            var cache = EnumFlagsCache.GetOrCreateFlagsCache(enumType, includeCombos);
            HierarchicalMultiSelectGui.DrawDropdown(dropdownRect, backend, cache);
            return backend;
        }
    }
}
