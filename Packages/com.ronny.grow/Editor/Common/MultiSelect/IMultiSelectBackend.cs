// SPDX-License-Identifier: MIT

namespace Grow.Editor.Common.MultiSelect {
    /// <summary>
    /// Abstraction over the storage backend of the multi-select control: the presentation layer (TreeView / Popup /
    /// RowGUI) depends only on this interface and knows nothing about whether the underlying storage is a bit mask,
    /// a value set or a SerializedProperty array.
    /// </summary>
    internal interface IMultiSelectBackend {
        /// <summary>Whether the value is currently selected.</summary>
        bool Contains(int value);

        /// <summary>Whether this is the mixed state of multi-object editing with differing values
        /// (the presentation layer then shows Mixed… / nothing checked).</summary>
        bool IsMixed { get; }

        /// <summary>Whether the value differs across targets (used to show a dash on an individual leaf).</summary>
        bool IsMixedAt(int value);

        /// <summary>Sets the selected state of a value.</summary>
        void SetValue(int value, bool on);

        /// <summary>Clears every selection (Nothing).</summary>
        void Clear();

        /// <summary>Selects the whole given value set (Everything).</summary>
        void SelectAll(int[] allValues);

        /// <summary>Commits the changes (writes back SerializedProperty / ApplyModifiedProperties, etc.).</summary>
        void Apply();
    }
}
