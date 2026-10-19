// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using Grow.Editor.Common.MultiSelect;

namespace Grow.Editor.Inspector.EnumFlags {
    /// <summary>
    /// Entry helper for [Flags] enum fields: resolves the enum type behind a field type and delegates to
    /// <see cref="EnumMultiSelectDataBuilder.BuildFlags"/> to build the display data (cached per enum type).
    /// <para>This class is kept so the public entry points used by drawers and tests stay stable; the actual build
    /// rules have been consolidated into the unified builder.</para>
    /// </summary>
    internal static class EnumFlagsCache {
        /// <summary>
        /// Resolves the usable enum type from a field type: a direct enum field returns itself; for compatibility
        /// callers may also pass T[] / List&lt;T&gt; and get the element enum type. The GrowEnumFlags drawer itself
        /// only enables on direct enum fields, while array and List fields fall back to the default drawing.
        /// <para>Returning null means the field is not an enum itself (the caller then falls back to the default
        /// drawing).</para>
        /// </summary>
        /// <param name="fieldType">The field type.</param>
        /// <returns>The resolved enum type, or null for non-enum cases.</returns>
        public static Type GetEnumType(Type fieldType) {
            if (fieldType == null) {
                return null;
            }

            if (fieldType.IsEnum) {
                return fieldType;
            }

            if (fieldType.IsArray) {
                var elementType = fieldType.GetElementType();
                return elementType != null && elementType.IsEnum ? elementType : null;
            }

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)) {
                var elementType = fieldType.GetGenericArguments().FirstOrDefault();
                return elementType != null && elementType.IsEnum ? elementType : null;
            }

            return null;
        }

        /// <summary>Gets or creates the display data for a [Flags] enum (returned directly on a cache hit).</summary>
        /// <param name="enumType">The enum type.</param>
        /// <param name="includeCombos">Whether to include combination alias rows.</param>
        /// <returns>The flags display data.</returns>
        public static MultiSelectData GetOrCreateFlagsCache(Type enumType, bool includeCombos = true) {
            return EnumMultiSelectDataBuilder.BuildFlags(enumType, includeCombos);
        }

        /// <summary>Builds the display data for a [Flags] enum (only single-bit members are
        /// selectable; combination alias rows are optional).</summary>
        /// <param name="enumType">The enum type (must be an enum, otherwise an exception is thrown).</param>
        /// <param name="includeCombos">Whether to include combination alias rows.</param>
        /// <returns>The flags display data.</returns>
        public static MultiSelectData BuildFlagsCache(Type enumType, bool includeCombos = true) {
            return EnumMultiSelectDataBuilder.BuildFlags(enumType, includeCombos);
        }
    }
}
