// SPDX-License-Identifier: MIT

using System;
using System.Globalization;
using System.Reflection;

namespace Grow.Editor.Inspector.Condition {
    /// <summary>
    /// Parses and evaluates condition expressions (pure reflection, testable without a GUI).
    /// <para>Evaluation applies to a single target object; the aggregation across multiple objects and clauses is
    /// done by GrowEditor: all targets satisfied → visible, some satisfied → visible but disabled (mixed), none
    /// satisfied → hidden.</para>
    /// </summary>
    internal static class ConditionResolver {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Parses a condition expression into a clause. Supports '!', '==' and method
        /// parentheses '()'.</summary>
        /// <param name="expression">The raw condition expression, such as "Enable" / "!Enable" /
        /// "Mode == 2" / "HasTag()".</param>
        /// <returns>The parsed <see cref="ConditionClause"/>.</returns>
        public static ConditionClause Parse(string expression) {
            var raw = expression?.Trim() ?? string.Empty;

            var negated = raw.StartsWith("!", StringComparison.Ordinal);
            if (negated) {
                raw = raw.Substring(1).Trim();
            }

            string target;
            string literal = null;
            var isEquality = false;

            var eqIndex = raw.IndexOf("==", StringComparison.Ordinal);
            if (eqIndex >= 0) {
                target = raw.Substring(0, eqIndex).Trim();
                literal = raw.Substring(eqIndex + 2).Trim();
                isEquality = true;
            } else {
                target = raw.Trim();
            }

            var isMethodHint = target.EndsWith("()", StringComparison.Ordinal) || target.IndexOf('(') >= 0;
            if (isMethodHint) {
                var parenIndex = target.IndexOf('(');
                target = parenIndex >= 0 ? target.Substring(0, parenIndex).Trim() : target;
            }

            return new ConditionClause(negated, isEquality, target, literal, isMethodHint);
        }

        /// <summary>
        /// Evaluates a clause against a single target object.
        /// <para>Returning null means the target field or method could not be resolved (the caller decides; it is
        /// usually treated as satisfied so that fields are not hidden by mistake).</para>
        /// </summary>
        /// <param name="target">The target object to evaluate against.</param>
        /// <param name="clause">The parsed condition clause.</param>
        /// <returns>true/false, or null when it cannot be resolved.</returns>
        public static bool? Evaluate(object target, in ConditionClause clause) {
            if (target == null) {
                return null;
            }

            object raw;
            var found = false;
            if (clause.IsMethodHint) {
                raw = InvokeBoolMethod(target, clause.Target, out found);
            } else {
                raw = ReadFieldValue(target, clause.Target, out found);
                if (!found) {
                    raw = InvokeBoolMethod(target, clause.Target, out found);
                }
            }

            if (!found) {
                return null;
            }

            var result = clause.IsEquality ? TryEquals(raw, clause.Literal) : ToTruthy(raw);
            if (result == null) {
                return null;
            }

            return clause.Negated ? !result.Value : result.Value;
        }

        /// <summary>Reads a field value along a '.' path (nested structs supported).</summary>
        private static object ReadFieldValue(object target, string path, out bool found) {
            var current = target;
            foreach (var segment in path.Split('.')) {
                if (current == null) {
                    found = false;
                    return null;
                }

                var field = FindField(current.GetType(), segment);
                if (field == null) {
                    found = false;
                    return null;
                }

                current = field.GetValue(current);
            }

            found = true;
            return current;
        }

        /// <summary>Finds a field on the type or a base type; private base fields need an explicit walk.</summary>
        private static FieldInfo FindField(Type type, string name) {
            for (var current = type; current != null; current = current.BaseType) {
                var field = current.GetField(name, InstanceFlags | BindingFlags.DeclaredOnly);
                if (field != null) {
                    return field;
                }
            }

            return null;
        }

        /// <summary>Invokes a parameterless method and returns its value (a bool is returned as is;
        /// other types fall back to truthiness).</summary>
        private static object InvokeBoolMethod(object target, string methodName, out bool found) {
            var method = target.GetType().GetMethod(methodName, InstanceFlags, null, Type.EmptyTypes, null);
            if (method == null) {
                found = false;
                return null;
            }

            try {
                found = true;
                return method.Invoke(target, null);
            } catch (Exception) {
                found = false;
                return null;
            }
        }

        /// <summary>Truthiness rule: bool by value; string non-empty; numbers/enums non-zero;
        /// anything else non-null is true.</summary>
        private static bool? ToTruthy(object raw) {
            switch (raw) {
                case null:
                    return false;
                case bool b:
                    return b;
                case string s:
                    return s.Length > 0;
                case Enum e:
                    return Convert.ToInt64(e) != 0;
                case UnityEngine.Object unityObject:
                    return unityObject != null;
                default:
                    return raw is IConvertible ? Math.Abs(Convert.ToDouble(raw, CultureInfo.InvariantCulture)) > 0.0 : raw != null;
            }
        }

        /// <summary>Compares against the literal; returns null when the literal cannot be
        /// interpreted as the field type (treated as a parse failure).</summary>
        private static bool? TryEquals(object raw, string literal) {
            var lit = literal?.Trim().Trim('\'', '"') ?? string.Empty;

            switch (raw) {
                case null:
                    return string.Equals(lit, "null", StringComparison.OrdinalIgnoreCase);

                case bool b:
                    if (bool.TryParse(lit, out var bv)) {
                        return bv == b;
                    }

                    return null;

                case Enum e: {
                        var rawValue = Convert.ToInt64(e);
                        if (long.TryParse(lit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) {
                            return rawValue == l;
                        }

                        if (Enum.TryParse(e.GetType(), lit, false, out var parsed)) {
                            return rawValue == Convert.ToInt64(parsed);
                        }

                        return null;
                    }

                case string s:
                    return string.Equals(s, lit, StringComparison.Ordinal);

                case float _:
                case double _:
                case decimal _:
                    if (double.TryParse(lit, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv)) {
                        return Math.Abs(Convert.ToDouble(raw, CultureInfo.InvariantCulture) - dv) < 1e-9;
                    }

                    return null;

                default:
                    // Other numeric types (byte/short/int/long...)
                    if (raw is IConvertible) {
                        if (long.TryParse(lit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lv)) {
                            return Convert.ToInt64(raw, CultureInfo.InvariantCulture) == lv;
                        }

                        return null;
                    }

                    return string.Equals(raw.ToString(), lit, StringComparison.Ordinal);
            }
        }
    }
}
