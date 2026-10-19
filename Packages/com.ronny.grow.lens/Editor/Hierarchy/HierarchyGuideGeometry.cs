// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>
    /// Pure geometry for Hierarchy indent guides. A guide is drawn at an ancestor's foldout
    /// center, never at the current row's own foldout column.
    /// </summary>
    internal static class HierarchyGuideGeometry {
        /// <summary>Maximum distance between content x and the computed depth column.</summary>
        private const float Tolerance = 0.5f;

        /// <summary>
        /// Returns true when a row at <paramref name="level"/> follows the given row before the
        /// tree returns to a shallower level. Descendant rows are skipped.
        /// </summary>
        internal static bool HasFollowingSibling(IReadOnlyList<int> depths, int row, int level) {
            if (depths == null || row < 0 || row >= depths.Count || level < 0) {
                return false;
            }

            for (var i = row + 1; i < depths.Count; i++) {
                var depth = depths[i];
                if (depth < level) {
                    return false;
                }

                if (depth == level) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Derives the item depth from the callback content x. Returns false when the values are
        /// invalid or the x does not sit on a depth column (layout changed, horizontal scroll, etc.).
        /// </summary>
        internal static bool TryComputeDepth(float contentX, float baseIndent, float indentWidth,
            float foldoutWidth, float marginLeft, out int depth) {
            depth = 0;
            if (indentWidth <= 0f || baseIndent < 0f || foldoutWidth < 0f || marginLeft < 0f) {
                return false;
            }

            var contentBase = baseIndent + foldoutWidth + marginLeft;
            var relative = contentX - contentBase;
            var rounded = Mathf.RoundToInt(relative / indentWidth);
            if (rounded < 0) {
                return false;
            }

            if (Mathf.Abs(relative - rounded * indentWidth) > Tolerance) {
                return false;
            }

            depth = rounded;
            return true;
        }

        /// <summary>X coordinate of an ancestor's foldout center (ancestor 0 = root).</summary>
        internal static float GuideX(float baseIndent, float indentWidth, float foldoutWidth,
            int ancestorDepth) {
            return baseIndent + ancestorDepth * indentWidth + foldoutWidth * 0.5f;
        }
    }
}
