// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Grow.Core.Attributes {
    /// <summary>
    /// Supplies a display label for a serialized field or an enum member.
    /// <para>On a field, the label replaces the Inspector label; the editor drawer applies it. An empty
    /// label falls back to the field's serialized display name.</para>
    /// <para>On an enum member, the label is used by the <see cref="GrowEnumFlagsAttribute"/> tree.</para>
    /// <para>This attribute does not set the label of a <see cref="GrowButtonAttribute"/> method.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GrowLabelAttribute : PropertyAttribute {
        /// <summary>The display label. An empty value falls back to the serialized name.</summary>
        public readonly string Label;

        /// <summary>Creates the label attribute.</summary>
        /// <param name="label">Display label; empty falls back to the serialized name.</param>
        public GrowLabelAttribute(string label) {
            // Run before other Grow drawers; a drawer forwards to the next one with PropertyField.
            order = -1000;
            Label = label;
        }
    }
}
