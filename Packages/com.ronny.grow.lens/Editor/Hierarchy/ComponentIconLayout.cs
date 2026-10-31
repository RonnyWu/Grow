// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>Pure right-to-left icon layout shared by drawing and click hit-testing.</summary>
    internal static class ComponentIconLayout {
        internal static int GetVisibleCount(Rect rowRect, float nameWidth, int iconCount, float iconSize,
            float rightInset = 0f) {
            if (iconCount <= 0 || iconSize <= 0f || rowRect.width <= 0f) {
                return 0;
            }

            var minX = rowRect.x + nameWidth;
            var startX = rowRect.xMax - rightInset;
            var count = 0;
            while (count < iconCount && startX - iconSize > minX) {
                count++;
                startX -= iconSize;
            }

            return count;
        }

        internal static Rect GetRect(Rect rowRect, int index, float iconSize,
            float rightInset = 0f) {
            return new Rect(rowRect.xMax - rightInset - (index + 1) * iconSize, rowRect.y,
                iconSize, iconSize);
        }
    }
}
