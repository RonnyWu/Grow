// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Grow.Core.Attributes {
    /// <summary>
    /// Draws a serialized field disabled in Grow's Inspector while keeping its current value visible.
    /// <para>The value is still serialized and can be changed by code or by a custom editor. The drawer
    /// only changes how the field is drawn; it is not a runtime write barrier.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GrowReadOnlyAttribute : PropertyAttribute {
        public GrowReadOnlyAttribute() {
            // Run after GrowLabel but before GrowEnumFlags; the drawer forwards via PropertyField.
            order = -500;
        }
    }
}
