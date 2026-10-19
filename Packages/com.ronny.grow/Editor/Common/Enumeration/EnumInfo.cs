// SPDX-License-Identifier: MIT

using System.Reflection;

namespace Grow.Editor.Common.Enumeration {
    /// <summary>
    /// Reflection metadata snapshot of a single defined enum member (static field).
    /// <para>Construction resolves the backing field, member name, display name ([GrowLabel] or member
    /// name) and raw integer value once, so <see cref="EnumTypeUtility"/> and the bit-flag / multi-select
    /// drawing code can share them instead of reflecting repeatedly.</para>
    /// </summary>
    internal readonly struct EnumInfo {
        /// <summary>The backing enum static field (usable for attribute checks such as [Obsolete];
        /// never null).</summary>
        public readonly FieldInfo EnumField;

        /// <summary>The enum member name (a C# identifier such as "AB").</summary>
        public readonly string EnumName;

        /// <summary>Display name (the [GrowLabel] value, or the member name when unannotated).</summary>
        public readonly string EnumLabel;

        /// <summary>The member's raw integer value (truncated to int according to the enum's
        /// underlying type).</summary>
        public readonly int EnumValue;

        /// <summary>Whether the member is marked [Obsolete] (resolved once at parse time so drawers
        /// can reuse it for dimming).</summary>
        public readonly bool IsObsolete;

        public EnumInfo(FieldInfo enumField, string enumName, string enumLabel, int enumValue, bool isObsolete = false) {
            EnumField = enumField;
            EnumName = enumName;
            EnumLabel = enumLabel;
            EnumValue = enumValue;
            IsObsolete = isObsolete;
        }

        /// <summary>Debug-friendly text: "[display name] member name = value".</summary>
        public override string ToString() {
            return $"[{EnumLabel}] {EnumName} = {EnumValue}";
        }
    }
}
