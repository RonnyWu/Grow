// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Reflection;
using Grow.Core.Attributes;
using UnityEngine;

namespace Grow.Editor.Common.Enumeration {
    /// <summary>
    /// Enum reflection helper: parses every defined member of an enum type into a list of <see cref="EnumInfo"/>
    /// and caches it sorted by ascending value.
    /// <para>The result carries [GrowLabel] display names and is shared by EnumFlagsGui and friends. Bit-flag
    /// drawing uses a 32-bit int mask, so members outside the <see cref="int"/> range are skipped with a
    /// one-time warning.</para>
    /// </summary>
    internal static class EnumTypeUtility {
        private static readonly Dictionary<Type, EnumInfo[]> EnumInfoCache = new();
        private static readonly HashSet<Type> OverflowWarned = new();

        internal static void ResetOverflowWarningsForTests() {
            OverflowWarned.Clear();
        }

        /// <summary>Gets the detailed member information for an enum type (cached per type;
        /// returned directly on a hit).</summary>
        /// <param name="enumType">The enum type; null or a non-enum type throws.</param>
        /// <returns>Member information sorted by ascending value.</returns>
        public static EnumInfo[] GetEnumInfos(Type enumType) {
            if (enumType == null) {
                throw new ArgumentNullException(nameof(enumType));
            }

            if (!enumType.IsEnum) {
                throw new ArgumentException($"Type '{enumType}' is not an enum type.", nameof(enumType));
            }

            if (!EnumInfoCache.TryGetValue(enumType, out var infos)) {
                infos = ParseEnumInfos(enumType);
                EnumInfoCache[enumType] = infos;
            }

            return infos;
        }

        /// <summary>Reflects every public static field of an enum type into <see cref="EnumInfo"/>
        /// and sorts it by ascending value.</summary>
        /// <param name="enumType">The enum type.</param>
        /// <returns>Member information sorted by ascending value.</returns>
        public static EnumInfo[] ParseEnumInfos(Type enumType) {
            var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            var infos = new List<EnumInfo>(fields.Length);
            foreach (var field in fields) {
                var rawValue = field.GetRawConstantValue();
                long value;
                try {
                    value = Convert.ToInt64(rawValue);
                } catch (OverflowException) {
                    if (OverflowWarned.Add(enumType)) {
                        Debug.LogWarning($"[Grow] Enum '{enumType}' has values outside the Int64 range; members are skipped.");
                    }

                    continue;
                }

                if (value < int.MinValue || value > int.MaxValue) {
                    if (OverflowWarned.Add(enumType)) {
                        Debug.LogWarning($"[Grow] Enum '{enumType}' has values outside the 32-bit flags range; higher members are skipped.");
                    }

                    continue;
                }

                var labelAttr = field.GetCustomAttribute<GrowLabelAttribute>();
                var label = string.IsNullOrEmpty(labelAttr?.Label) ? field.Name : labelAttr.Label;
                var isObsolete = field.IsDefined(typeof(ObsoleteAttribute), false);
                infos.Add(new EnumInfo(field, field.Name, label, (int)value, isObsolete));
            }

            infos.Sort((a, b) => a.EnumValue.CompareTo(b.EnumValue));
            return infos.ToArray();
        }
    }
}
