// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Grow.Core.Attributes {
    /// <summary>
    /// Draws a horizontal separator above a serialized field.
    /// <para>This is a decorator: it adds visual spacing without changing how the field is drawn. It only
    /// applies to Grow's editor; Unity's default Inspector ignores it.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GrowSeparatorAttribute : PropertyAttribute {
        /// <summary>Line thickness in pixels.</summary>
        public readonly float Thickness;

        /// <summary>Vertical margin around the line in pixels.</summary>
        public readonly float Margin;

        /// <summary>Creates a separator attribute. Negative values are clamped to zero.</summary>
        /// <param name="thickness">Line thickness in pixels.</param>
        /// <param name="margin">Vertical margin in pixels.</param>
        public GrowSeparatorAttribute(float thickness = 1f, float margin = 6f) {
            Thickness = Mathf.Max(0f, thickness);
            Margin = Mathf.Max(0f, margin);
        }
    }
}
