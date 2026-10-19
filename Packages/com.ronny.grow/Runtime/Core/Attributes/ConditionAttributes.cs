// SPDX-License-Identifier: MIT

using System;

namespace Grow.Core.Attributes {
    /// <summary>
    /// Base class for Grow visibility and editability conditions.
    /// <para>Supported expression forms:</para>
    /// <code>
    /// "FieldName"          truthy: true, a non-zero number or enum, a non-empty string, a non-null object
    /// "!FieldName"         inverted truthiness
    /// "FieldName == 2"     equality against a number, bool, enum member, string or null
    /// "MethodName()"       parameterless method; its result is tested for truthiness
    /// "MethodName() == 2"  comparison against a parameterless method result
    /// </code>
    /// <para>Field names may be dotted paths such as <c>Stats.Enabled</c>. Resolution and evaluation
    /// happen in the editor. Boolean operators, ranges and arithmetic are not supported.</para>
    /// </summary>
    public abstract class GrowConditionAttribute : Attribute {
        /// <summary>The original condition expression.</summary>
        public readonly string Condition;

        /// <summary>Creates a condition attribute.</summary>
        /// <param name="condition">Condition expression; must not be null.</param>
        protected GrowConditionAttribute(string condition) {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }
    }

    /// <summary>
    /// Shows the field only when the condition matches. With multiple <c>GrowShowIf</c> attributes,
    /// every condition must match.
    /// <para>Only Grow's editor evaluates the condition; Unity's default Inspector ignores it.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class GrowShowIfAttribute : GrowConditionAttribute {
        /// <summary>Creates a show-if condition.</summary>
        /// <param name="condition">Condition expression.</param>
        public GrowShowIfAttribute(string condition) : base(condition) {
        }
    }

    /// <summary>
    /// Hides the field when the condition matches. With multiple <c>GrowHideIf</c> attributes, any
    /// matching condition hides the field.
    /// <para>Only Grow's editor evaluates the condition; Unity's default Inspector ignores it.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class GrowHideIfAttribute : GrowConditionAttribute {
        /// <summary>Creates a hide-if condition.</summary>
        /// <param name="condition">Condition expression.</param>
        public GrowHideIfAttribute(string condition) : base(condition) {
        }
    }

    /// <summary>
    /// Keeps the field visible and makes it editable only when the condition matches. With multiple
    /// <c>GrowEnableIf</c> attributes, every condition must match.
    /// <para>Only Grow's editor evaluates the condition; Unity's default Inspector ignores it.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class GrowEnableIfAttribute : GrowConditionAttribute {
        /// <summary>Creates an enable-if condition.</summary>
        /// <param name="condition">Condition expression.</param>
        public GrowEnableIfAttribute(string condition) : base(condition) {
        }
    }

    /// <summary>
    /// Keeps the field visible but disables it when the condition matches. With multiple
    /// <c>GrowDisableIf</c> attributes, any matching condition disables the field.
    /// <para>Only Grow's editor evaluates the condition; Unity's default Inspector ignores it.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class GrowDisableIfAttribute : GrowConditionAttribute {
        /// <summary>Creates a disable-if condition.</summary>
        /// <param name="condition">Condition expression.</param>
        public GrowDisableIfAttribute(string condition) : base(condition) {
        }
    }
}
