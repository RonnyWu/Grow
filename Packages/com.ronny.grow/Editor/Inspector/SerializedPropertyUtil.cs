// SPDX-License-Identifier: MIT

using System;
using System.Reflection;
using Grow.Core.Attributes;
using UnityEditor;

namespace Grow.Editor.Inspector {
    /// <summary>
    /// Reflection helpers for SerializedProperty: recover the FieldInfo from a property path / read custom
    /// annotations / get the display name / apply changes immediately.
    /// <para>Exposed as explicit static methods rather than extension methods, so they cannot collide with same-named
    /// legacy extensions in com.grow.lens (Grow.Editor.Extensions.SerializedPropertyExt /
    /// GrowEditorUtil.AutoDirty) when both namespaces are imported; shared by the annotation framework (condition
    /// evaluation, label fallback) and hand-written full Inspectors.</para>
    /// </summary>
    internal static class SerializedPropertyUtil {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Recovers the field's FieldInfo from a property path (nested structs and List/array element paths such as
        /// "Nested.Deep" or "List.Array.data[0].Deep" are supported).
        /// <para>Returns false on failure (for example when the path contains unresolvable pseudo nodes).</para>
        /// </summary>
        /// <param name="property">The target property.</param>
        /// <param name="fieldInfo">The resolved FieldInfo; null on failure.</param>
        /// <returns>Whether resolution succeeded.</returns>
        public static bool TryGetFieldInfo(SerializedProperty property, out FieldInfo fieldInfo) {
            fieldInfo = null;
            var target = property?.serializedObject?.targetObject;
            if (target == null) {
                return false;
            }

            var type = target.GetType();
            FieldInfo current = null;
            var parts = property.propertyPath.Split('.');

            foreach (var part in parts) {
                // Unity's serialization pseudo node: the "Array" node of a List<T>/array is skipped, not resolved
                if (part == "Array") {
                    continue;
                }

                var bracketIndex = part.IndexOf('[');
                var fieldName = bracketIndex >= 0 ? part.Substring(0, bracketIndex) : part;
                var hasBracket = bracketIndex >= 0;

                // For a List<T>/array element node "data[i]", "data" is a pseudo field name: it takes no part in the
                // field lookup and only narrows the element type
                var isContainerDataNode = hasBracket && fieldName == "data" && IsContainer(type);

                if (!isContainerDataNode && fieldName.Length > 0) {
                    current = FindField(type, fieldName);
                    if (current == null) {
                        fieldInfo = null;
                        return false;
                    }

                    type = current.FieldType;
                }

                if (hasBracket) {
                    // Enter the container element: narrow to the array element type / List<T> generic argument
                    if (type == null) {
                        fieldInfo = null;
                        return false;
                    }

                    if (type.IsArray) {
                        type = type.GetElementType();
                    } else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>)) {
                        type = type.GetGenericArguments()[0];
                    } else {
                        fieldInfo = null;
                        return false;
                    }
                }
            }

            fieldInfo = current;
            return current != null;
        }

        /// <summary>Finds a field on the type or any base type; private base fields need an explicit walk.</summary>
        private static FieldInfo FindField(Type type, string name) {
            for (var current = type; current != null; current = current.BaseType) {
                var field = current.GetField(name, FieldFlags | BindingFlags.DeclaredOnly);
                if (field != null) {
                    return field;
                }
            }

            return null;
        }

        /// <summary>Whether the type is an array or a List&lt;&gt; container.</summary>
        /// <param name="type">The type to test.</param>
        /// <returns>Whether it is a container type.</returns>
        private static bool IsContainer(Type type) {
            return type != null &&
                   (type.IsArray ||
                    (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>)));
        }

        /// <summary>Reads a custom annotation from the field (base-class inheritance included);
        /// returns false when absent.</summary>
        /// <typeparam name="T">The annotation type.</typeparam>
        /// <param name="property">The target property.</param>
        /// <param name="attribute">The resolved attribute instance; null when not found.</param>
        /// <returns>Whether the attribute was found.</returns>
        public static bool TryGetAttribute<T>(SerializedProperty property, out T attribute) where T : Attribute {
            attribute = null;
            if (!TryGetFieldInfo(property, out var fieldInfo)) {
                return false;
            }

            attribute = fieldInfo.GetCustomAttribute<T>(true);
            return attribute != null;
        }

        /// <summary>Gets the field display name: the [GrowLabel] annotation when present, otherwise
        /// property.displayName.</summary>
        /// <param name="property">The target property.</param>
        /// <returns>The display name.</returns>
        public static string GetLabel(SerializedProperty property) {
            if (TryGetAttribute<GrowLabelAttribute>(property, out var label) && !string.IsNullOrEmpty(label.Label)) {
                return label.Label;
            }

            return property.displayName;
        }

        /// <summary>
        /// Runs one change check around <paramref name="action"/> for the property: when something changed, calls
        /// ApplyModifiedProperties and marks the target object dirty.
        /// <para>Suited to small controls in hand-written panels that commit as they change (such
        /// as Toggle / Slider).</para>
        /// </summary>
        /// <param name="property">The target property.</param>
        /// <param name="action">The drawing / modification action.</param>
        /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
        public static void AutoDirty(SerializedProperty property, Action action) {
            if (property == null) {
                throw new ArgumentNullException(nameof(property));
            }

            EditorGUI.BeginChangeCheck();
            action();
            if (EditorGUI.EndChangeCheck()) {
                property.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(property.serializedObject.targetObject);
            }
        }
    }
}
