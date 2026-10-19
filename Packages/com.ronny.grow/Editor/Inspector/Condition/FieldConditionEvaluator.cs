// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using Grow.Editor.Inspector.Data;

namespace Grow.Editor.Inspector.Condition {
    /// <summary>
    /// Multi-target aggregation of field-level condition evaluation: derives "show / disable / hide" from the
    /// per-target <see cref="ConditionResolver"/> results.
    /// <para>Visibility (ShowIf/HideIf): all targets visible → normal; some visible → shown but disabled (mixed
    /// state); none visible → hidden. Editability (EnableIf/DisableIf): only editable when every target is
    /// editable (any disabled target dims the whole field). Clauses that cannot be resolved count as satisfied (to
    /// avoid hiding by mistake).</para>
    /// <para>Pure reflection with no GUI dependency, so it is directly testable in EditMode.</para>
    /// </summary>
    internal static class FieldConditionEvaluator {
        /// <summary>
        /// Computes field visibility across multiple targets (ShowIf/HideIf):
        /// all targets visible → normal; some visible → shown but disabled (mixed multi-selection, disabled rather
        /// than hidden); none visible → hidden. Clauses that cannot be resolved count as satisfied.
        /// </summary>
        /// <param name="field">The field model.</param>
        /// <param name="targets">The target objects.</param>
        /// <param name="mixed">Output: whether this is the "some targets visible" mixed state.</param>
        /// <returns>Whether the field should be shown (also true in the mixed state).</returns>
        public static bool ShouldShow(FieldModel field, object[] targets, out bool mixed) {
            mixed = false;
            var total = targets?.Length ?? 0;
            if (total == 0) {
                return true;
            }

            var visibleCount = 0;
            foreach (var target in targets) {
                if (IsVisibleOnTarget(field, target)) {
                    visibleCount++;
                }
            }

            if (visibleCount == total) {
                return true;
            }

            mixed = visibleCount > 0;
            return mixed;
        }

        /// <summary>EnableIf/DisableIf: only editable when every target is editable (any disabled
        /// target dims the whole field).</summary>
        /// <param name="field">The field model.</param>
        /// <param name="targets">The target objects.</param>
        /// <returns>Whether the field should be editable.</returns>
        public static bool ShouldEnable(FieldModel field, object[] targets) {
            var total = targets?.Length ?? 0;
            if (total == 0) {
                return true;
            }

            foreach (var target in targets) {
                if (!IsEnabledOnTarget(field, target)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Computes visibility on a single target: every ShowIf clause must hold and no
        /// HideIf clause may hold.</summary>
        /// <param name="field">The field model.</param>
        /// <param name="target">The target object.</param>
        /// <returns>Whether the field is visible.</returns>
        private static bool IsVisibleOnTarget(FieldModel field, object target) {
            if (!AllClausesMatch(field.ShowClauses, target, true)) {
                return false;
            }

            return AllClausesMatch(field.HideClauses, target, false);
        }

        /// <summary>Computes editability on a single target: every EnableIf clause must hold and no
        /// DisableIf clause may hold.</summary>
        /// <param name="field">The field model.</param>
        /// <param name="target">The target object.</param>
        /// <returns>Whether the field is editable.</returns>
        private static bool IsEnabledOnTarget(FieldModel field, object target) {
            if (!AllClausesMatch(field.EnableClauses, target, true)) {
                return false;
            }

            return AllClausesMatch(field.DisableClauses, target, false);
        }

        /// <summary>Evaluates every clause and compares the polarity: with expectTrue=true each
        /// clause must hold, with false each must not hold (unresolved clauses pass).</summary>
        /// <param name="clauses">The clause list.</param>
        /// <param name="target">The target object.</param>
        /// <param name="expectTrue">The expected polarity.</param>
        /// <returns>Whether every clause matches the expected polarity.</returns>
        private static bool AllClausesMatch(IReadOnlyList<ConditionClause> clauses, object target, bool expectTrue) {
            if (clauses == null) {
                return true;
            }

            foreach (var clause in clauses) {
                var result = ConditionResolver.Evaluate(target, clause);
                if (result != null && result.Value != expectTrue) {
                    return false;
                }
            }

            return true;
        }
    }
}
