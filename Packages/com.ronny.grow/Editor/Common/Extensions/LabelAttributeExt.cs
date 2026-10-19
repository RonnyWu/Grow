// SPDX-License-Identifier: MIT

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Grow.Core.Attributes;

namespace Grow.Editor.Common.Extensions {
    /// <summary>Reads <see cref="GrowLabelAttribute"/> display names for fields, enums and enum members.</summary>
    internal static class LabelAttributeExt {
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private const BindingFlags EnumFlags = BindingFlags.Public | BindingFlags.Static;
        private static readonly ConcurrentDictionary<(Type type, string name, string fallback), string> FieldLabelCache =
            new();
        private static readonly ConcurrentDictionary<Type, string[]> TypeLabelsCache = new();
        private static readonly ConcurrentDictionary<FieldInfo, string> FieldInfoCache = new();

        /// <summary>Reads the label of a field by name on the given type or its base types.</summary>
        public static string GetLabel(this Type type, string name, string fallback = "Unknown") {
            if (type == null) {
                return fallback;
            }

            return FieldLabelCache.GetOrAdd((type, name, fallback), key => {
                var label = FindField(key.type, key.name)?.GetLabel();
                return string.IsNullOrEmpty(label) ? key.fallback : label;
            });
        }

        /// <summary>Reads the label of a field, falling back to the field name.</summary>
        public static string GetLabel(this FieldInfo fieldInfo, string fallback = "Unknown") {
            if (fieldInfo == null) {
                return fallback;
            }

            return FieldInfoCache.GetOrAdd(fieldInfo, field => {
                var label = field.GetCustomAttribute<GrowLabelAttribute>(false)?.Label;
                return string.IsNullOrEmpty(label) ? field.Name : label;
            });
        }

        /// <summary>Reads the label of an enum member.</summary>
        public static string GetLabel(this Enum value, string fallback = "Unknown") {
            return value == null
                ? fallback
                : value.GetType().GetField(value.ToString(), EnumFlags)?.GetLabel() ?? value.ToString();
        }

        /// <summary>Reads the labels of all enum members.</summary>
        public static IEnumerable<string> GetLabels(this Enum value) {
            return GetLabels(value?.GetType());
        }

        /// <summary>Reads the labels of all enum members or fields on the type.</summary>
        public static IEnumerable<string> GetLabels(this Type type) {
            return type == null
                ? Enumerable.Empty<string>()
                : TypeLabelsCache.GetOrAdd(type,
                    t => {
                        return t.IsEnum
                            ? Enum.GetNames(t).Select(name => t.GetField(name, EnumFlags)?.GetLabel() ?? name)
                                .ToArray()
                            : t.GetFields(InstanceFlags).Select(field => field.GetLabel()).ToArray();
                    });
        }

        /// <summary>Reads the labels of all enum members or fields on <typeparamref name="T"/>.</summary>
        public static IEnumerable<string> GetLabels<T>() {
            return GetLabels(typeof(T));
        }

        /// <summary>Clears all reflection caches.</summary>
        public static void ClearCache() {
            FieldLabelCache.Clear();
            TypeLabelsCache.Clear();
            FieldInfoCache.Clear();
        }

        private static FieldInfo FindField(Type type, string name) {
            for (var current = type; current != null; current = current.BaseType) {
                var field = current.GetField(name, InstanceFlags);
                if (field != null) {
                    return field;
                }
            }

            return null;
        }
    }
}
