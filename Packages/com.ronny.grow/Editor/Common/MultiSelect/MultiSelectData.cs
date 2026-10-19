// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace Grow.Editor.Common.MultiSelect {
    /// <summary>
    /// Display data for the multi-select control: labels used for grouping, disabled state, Nothing / Everything
    /// text and so on.
    /// <para>Built by the individual drawers / painters (with identical rules: "/" in a Label delimits path
    /// grouping at any depth). Construction is comparatively expensive, so it is normally cached and reused per
    /// (type, options) pair.</para>
    /// </summary>
    internal struct MultiSelectData {
        /// <summary>The selectable values.</summary>
        public int[] Values;

        /// <summary>Display labels, formatted "{value} : {Name} : {Label}" (the Label may use "/"
        /// to nest levels).</summary>
        public string[] Labels;

        /// <summary>Whether the entry is [Obsolete] (disabled, dimmed and non-interactive).</summary>
        public bool[] Obsoletes;

        /// <summary>The "Nothing" display text.</summary>
        public string NothingLabel;

        /// <summary>The "Everything" display text.</summary>
        public string EverythingLabel;

        /// <summary>Trailing dimmed detail for "Nothing" (may be null).</summary>
        public string NothingInfo;

        /// <summary>Trailing dimmed detail for "Everything" (may be null).</summary>
        public string EverythingInfo;

        /// <summary>Combination alias values (multi-bit defined members drawn as a whole-row
        /// toggle; populated only in [Flags] mode, empty otherwise).</summary>
        public int[] ComboValues;

        /// <summary>Combination alias labels, formatted like <see cref="Labels"/> as "{value} :
        /// {Name} : {Label}"; index-parallel with <see cref="ComboValues"/>.</summary>
        public string[] ComboLabels;

        /// <summary>Map from defined member value to display name (combination aliases included;
        /// used for alias-hit matching in the button label; populated only in [Flags]
        /// mode).</summary>
        public Dictionary<int, string> ValueToNameMap;

        /// <summary>Whether the label supports matching "selected set OR-ed equals a defined member
        /// (combination aliases included)" (true only in [Flags] mode; bit-index / value-set modes
        /// have no alias semantics).</summary>
        public bool SupportsAliasMatch;
    }
}
