// SPDX-License-Identifier: MIT

namespace Grow.Editor.Inspector.Condition {
    /// <summary>
    /// One parsed clause of a condition expression. For the syntax see the
    /// <see cref="Grow.Core.Attributes.GrowConditionAttribute"/> class documentation.
    /// <para>Produced by <see cref="ConditionResolver.Parse"/> and evaluated by
    /// <see cref="ConditionResolver.Evaluate"/>; an immutable struct with read-only fields.</para>
    /// </summary>
    internal readonly struct ConditionClause {
        /// <summary>Whether the clause is negated (the '!' prefix).</summary>
        public readonly bool Negated;

        /// <summary>Whether this is an equality comparison (contains '==').</summary>
        public readonly bool IsEquality;

        /// <summary>The target field path or method name (parentheses and the method hint already
        /// stripped).</summary>
        public readonly string Target;

        /// <summary>The equality-comparison literal (raw text; leading/trailing whitespace removed,
        /// quote handling deferred to evaluation).</summary>
        public readonly string Literal;

        /// <summary>Whether the caller explicitly hinted a method target with '()'.</summary>
        public readonly bool IsMethodHint;

        /// <summary>Constructs a parsed condition clause (normally created by
        /// <see cref="ConditionResolver.Parse"/>).</summary>
        /// <param name="negated">Whether the clause is negated.</param>
        /// <param name="isEquality">Whether this is an equality comparison.</param>
        /// <param name="target">The target field path or method name.</param>
        /// <param name="literal">The equality-comparison literal.</param>
        /// <param name="isMethodHint">Whether the caller explicitly hinted a method target.</param>
        public ConditionClause(bool negated, bool isEquality, string target, string literal, bool isMethodHint) {
            Negated = negated;
            IsEquality = isEquality;
            Target = target;
            Literal = literal;
            IsMethodHint = isMethodHint;
        }
    }
}
