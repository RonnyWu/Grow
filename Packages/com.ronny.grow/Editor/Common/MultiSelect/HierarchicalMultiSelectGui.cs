// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Common.MultiSelect {
    /// <summary>
    /// Single entry point for the hierarchical multi-select presentation layer: draws the dropdown button plus the
    /// popup TreeView.
    /// <para>All reads and writes go through <see cref="IMultiSelectBackend"/>; the display data is supplied by the
    /// caller as <see cref="MultiSelectData"/>.</para>
    /// </summary>
    internal static class HierarchicalMultiSelectGui {
        private const string MixedLabel = "Mixed…";

        /// <summary>Builds the text shown on the dropdown button (matching built-in enum semantics:
        /// a hit on a defined member — combination aliases included — shows its name, otherwise
        /// Mixed…).</summary>
        /// <param name="backend">The storage backend.</param>
        /// <param name="cache">The display data.</param>
        /// <returns>The button text.</returns>
        public static string GetDisplayLabel(IMultiSelectBackend backend, in MultiSelectData cache) {
            // Multi-object editing with differing values → Mixed (like the native dash)
            if (backend.IsMixed) {
                return MixedLabel;
            }

            var selected = cache.Values.Where(backend.Contains).ToArray();
            if (selected.Length == 0) {
                return cache.NothingLabel;
            }

            if (selected.Length == cache.Values.Length) {
                return cache.EverythingLabel;
            }

            if (selected.Length == 1) {
                return GetNameByValue(selected[0], cache);
            }

            // Multi-select state: Flags mode matches "defined member (combination aliases included)" and shows the
            // alias on a hit; otherwise Mixed…
            if (cache.SupportsAliasMatch && cache.ValueToNameMap != null) {
                long mask = 0;
                foreach (var v in selected) {
                    mask |= (long)v;
                }

                if (cache.ValueToNameMap.TryGetValue((int)mask, out var alias)) {
                    return alias;
                }

                return MixedLabel;
            }

            // Bit-index / value-set modes have no alias semantics, so keep the detailed value list
            return string.Join(",", selected);
        }

        /// <summary>Looks up a member display name by value: for a "Value : Name : Label" triple
        /// returns Name, otherwise falls back to the numeric text.</summary>
        private static string GetNameByValue(int value, in MultiSelectData cache) {
            var index = Array.IndexOf(cache.Values, value);
            if (index < 0) {
                return value.ToString();
            }

            var parts = cache.Labels[index].Split(new[] { " : " }, StringSplitOptions.None);
            return parts.Length == 3 ? parts[1] : cache.Labels[index];
        }

        /// <summary>Draws the dropdown button; clicking it opens the hierarchical multi-select TreeView.</summary>
        /// <param name="position">The dropdown button rect.</param>
        /// <param name="backend">The storage backend.</param>
        /// <param name="cache">The display data.</param>
        public static void DrawDropdown(Rect position, IMultiSelectBackend backend, in MultiSelectData cache) {
            var label = GetDisplayLabel(backend, cache);
            if (EditorGUI.DropdownButton(position, new GUIContent(label), FocusType.Keyboard)) {
                PopupWindow.Show(position, new MultiSelectPopupContent(backend, cache));
            }
        }
    }
}
