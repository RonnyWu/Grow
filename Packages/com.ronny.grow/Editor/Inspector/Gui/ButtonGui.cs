// SPDX-License-Identifier: MIT

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Inspector.Gui {
    /// <summary>
    /// Shared helper for drawing [GrowButton] method cards: signature text, parameter labels, type short names, and
    /// the row-height / card-style constants.
    /// <para>The pure-text parts (TypeShortName / BuildSignature / BuildParamLabel) are asserted directly by EditMode
    /// tests; layout and invocation are still orchestrated by <see cref="GrowEditor"/>.</para>
    /// </summary>
    internal static class ButtonGui {
        /// <summary>The standard single-line height of the method row (foldout header + invoke
        /// button), matching a normal Inspector row.</summary>
        public const float RowHeight = 18f;

        /// <summary>The fixed text of the invoke button (the display label already appears in the
        /// foldout header, so the button keeps a uniform verb).</summary>
        public const string InvokeLabel = "Invoke";

        /// <summary>The fixed width of the invoke button (it does not stretch with the label, so it
        /// never eats into the header space).</summary>
        public const float InvokeWidth = 50f;

        private static GUIStyle _cardStyle;

        /// <summary>Method card style: helpBox look with tight padding (1px above and below, so a
        /// single-line card totals row height + 2; 0 on the sides so content touches the edges;
        /// margins keep the base top/bottom with 0 on the sides; border zeroed).</summary>
        public static GUIStyle CardStyle {
            get {
                if (_cardStyle == null) {
                    var baseStyle = EditorStyles.helpBox;
                    _cardStyle = new GUIStyle(baseStyle) {
                        padding = new RectOffset(0, 0, 1, 1),
                        margin = new RectOffset(0, 0, baseStyle.margin.top, baseStyle.margin.bottom),
                        border = new RectOffset(0, 0, 0, 0)
                    };
                }

                return _cardStyle;
            }
        }

        /// <summary>The C# short type name of a parameter: primitives map to keywords
        /// (int/long/float/double/bool/string), enums return the type name.</summary>
        /// <param name="type">The parameter type.</param>
        /// <returns>The short type name; an empty string for null.</returns>
        public static string TypeShortName(Type type) {
            if (type == null) {
                return string.Empty;
            }

            if (type == typeof(int)) {
                return "int";
            }

            if (type == typeof(long)) {
                return "long";
            }

            if (type == typeof(float)) {
                return "float";
            }

            if (type == typeof(double)) {
                return "double";
            }

            if (type == typeof(bool)) {
                return "bool";
            }

            if (type == typeof(string)) {
                return "string";
            }

            return type.Name;
        }

        /// <summary>Builds the method signature text, such as "AddToWatch(int delta)" / "Reset()":
        /// short type names and parameter names listed one by one.</summary>
        /// <param name="method">The target method.</param>
        /// <returns>The signature string; an empty string for null.</returns>
        public static string BuildSignature(MethodInfo method) {
            if (method == null) {
                return string.Empty;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 0) {
                return method.Name + "()";
            }

            var parts = new string[parameters.Length];
            for (var i = 0; i < parameters.Length; i++) {
                parts[i] = TypeShortName(parameters[i].ParameterType) + " " + parameters[i].Name;
            }

            return method.Name + "(" + string.Join(", ", parts) + ")";
        }

        /// <summary>The input-control label for a single parameter row, such as "int delta" (type
        /// prefix + parameter name).</summary>
        /// <param name="parameter">The parameter info.</param>
        /// <returns>The parameter label; an empty string for null.</returns>
        public static string BuildParamLabel(ParameterInfo parameter) {
            if (parameter == null) {
                return string.Empty;
            }

            return TypeShortName(parameter.ParameterType) + " " + parameter.Name;
        }
    }
}
