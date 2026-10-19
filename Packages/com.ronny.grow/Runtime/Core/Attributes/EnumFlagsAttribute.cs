// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Grow.Core.Attributes {
    /// <summary>
    /// Draws a <c>[Flags]</c> enum field as a hierarchical multi-select tree instead of Unity's single
    /// popup.
    /// <para>The tree treats single-bit members (1, 2, 4, 8, ...) as checkable leaves. Zero, negative and
    /// multi-bit members are not leaves; a multi-bit member whose bits are all visible can appear as a
    /// whole-toggle row when <c>showCombos</c> is enabled. <c>None</c> / <c>All</c> members are presented
    /// as the fixed Nothing / Everything rows.</para>
    /// <para>This is strictly opt-in: only fields annotated with <see cref="GrowEnumFlagsAttribute"/> use
    /// the hierarchical control. Unannotated enums keep Unity's native rendering, including
    /// <c>EnumFlagsField</c> for <c>[Flags]</c> enums.</para>
    /// <para>Arrays and <c>List&lt;T&gt;</c> fields are not supported by this drawer and fall back to
    /// Unity's native rendering.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GrowEnumFlagsAttribute : PropertyAttribute {
        /// <summary>Whether multi-bit defined members are also shown as whole-toggle rows.</summary>
        public bool ShowCombos { get; }

        /// <summary>Creates the enum-flags attribute.</summary>
        /// <param name="showCombos">Whether to show defined multi-bit members as whole-toggle rows.</param>
        public GrowEnumFlagsAttribute(bool showCombos = true) {
            ShowCombos = showCombos;
        }
    }
}
