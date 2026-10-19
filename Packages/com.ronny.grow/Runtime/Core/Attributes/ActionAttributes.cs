// SPDX-License-Identifier: MIT

using System;

namespace Grow.Core.Attributes {
    /// <summary>
    /// Calls a parameterless method on the serialized object after the annotated field changes in the
    /// Inspector. Each Inspector change invokes the callback once per selected target.
    /// <para>The callback is resolved on the edited object. It may be public or private. A missing method
    /// is reported when the Inspector model is built. This attribute only works with
    /// <c>GrowEditor</c>; Unity's default Inspector ignores it.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GrowOnValueChangedAttribute : Attribute {
        /// <summary>Name of the parameterless method on the serialized object.</summary>
        public readonly string MethodName;

        /// <summary>Creates the callback attribute.</summary>
        /// <param name="methodName">Name of the parameterless method; must not be null.</param>
        public GrowOnValueChangedAttribute(string methodName) {
            MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        }
    }

    /// <summary>
    /// Exposes a method as a button in Grow's Inspector.
    /// <para>The button can be given a label. The method may take <c>int</c>, <c>long</c>, <c>float</c>,
    /// <c>double</c>, <c>bool</c>, <c>string</c> or enum arguments; the editor generates argument controls
    /// and persists their values in <c>EditorPrefs</c>. Static, generic, non-void and unsupported-signature
    /// methods are skipped with a warning.</para>
    /// <para>Unity's default Inspector ignores the attribute; this is deliberate graceful degradation.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class GrowButtonAttribute : Attribute {
        /// <summary>Button label; empty falls back to the method name.</summary>
        public readonly string Label;

        /// <summary>Creates a button attribute.</summary>
        /// <param name="label">Button label; empty falls back to the method name.</param>
        public GrowButtonAttribute(string label = null) {
            Label = label ?? string.Empty;
        }
    }
}
