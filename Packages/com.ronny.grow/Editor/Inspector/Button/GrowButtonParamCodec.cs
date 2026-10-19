// SPDX-License-Identifier: MIT

using System;
using System.Globalization;
using Grow.Editor.Inspector.EnumFlags;

namespace Grow.Editor.Inspector.Button {
    /// <summary>
    /// Encoding/decoding and default-value policy for [GrowButton] parameter values (no GUI dependency, so directly
    /// testable in EditMode).
    /// <para>Covers per-type defaults, cache normalization for [Flags] enums, and round-tripping to and from
    /// EditorPrefs text.</para>
    /// </summary>
    internal static class GrowButtonParamCodec {
        /// <summary>Returns the default value of a parameter type: the first enum member for enums,
        /// an empty string for string, 0 for long/double, Activator for value types, null for
        /// reference types.</summary>
        /// <param name="type">The parameter type.</param>
        /// <returns>The type's default value.</returns>
        public static object GetDefaultValue(Type type) {
            if (type.IsEnum) {
                var values = Enum.GetValues(type);
                return values.Length > 0 ? values.GetValue(0) : Activator.CreateInstance(type);
            }

            if (type == typeof(string)) {
                return string.Empty;
            }

            if (type == typeof(long)) {
                return 0L;
            }

            if (type == typeof(double)) {
                return 0d;
            }

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>Normalizes a [Flags] enum parameter value into the
        /// <see cref="EnumValueBackend"/> cache form, so edits made in the popup multi-select
        /// survive repaints.</summary>
        /// <param name="type">The parameter type.</param>
        /// <param name="value">The raw value.</param>
        /// <returns>The normalized cache value.</returns>
        public static object NormalizeParam(Type type, object value) {
            if (type.IsEnum && type.IsDefined(typeof(FlagsAttribute), false)) {
                return value is EnumValueBackend backend ? backend : new EnumValueBackend((Enum)value);
            }

            return value;
        }

        /// <summary>Serializes a parameter value into persistent text (enum member name; floating
        /// point with "R" + InvariantCulture).</summary>
        /// <param name="type">The parameter type.</param>
        /// <param name="value">The parameter value.</param>
        /// <returns>The persistent text.</returns>
        public static string ToParamString(Type type, object value) {
            if (type.IsEnum) {
                return value?.ToString() ?? string.Empty;
            }

            switch (value) {
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
                default: return value?.ToString() ?? string.Empty;
            }
        }

        /// <summary>Restores a parameter value from persistent text; returns false and leaves value
        /// null when parsing fails.</summary>
        /// <param name="type">The parameter type.</param>
        /// <param name="text">The persistent text.</param>
        /// <param name="value">The restored parameter value.</param>
        /// <returns>Whether parsing succeeded.</returns>
        public static bool TryParseParam(Type type, string text, out object value) {
            value = null;
            try {
                if (type == typeof(int)) {
                    value = int.Parse(text, CultureInfo.InvariantCulture);
                    return true;
                }

                if (type == typeof(long)) {
                    value = long.Parse(text, CultureInfo.InvariantCulture);
                    return true;
                }

                if (type == typeof(float)) {
                    value = float.Parse(text, CultureInfo.InvariantCulture);
                    return true;
                }

                if (type == typeof(double)) {
                    value = double.Parse(text, CultureInfo.InvariantCulture);
                    return true;
                }

                if (type == typeof(bool)) {
                    value = bool.Parse(text);
                    return true;
                }

                if (type == typeof(string)) {
                    value = text;
                    return true;
                }

                if (type.IsEnum) {
                    value = Enum.Parse(type, text, false);
                    return true;
                }

                return false;
            } catch (Exception) {
                return false;
            }
        }
    }
}
