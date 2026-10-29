// SPDX-License-Identifier: MIT

using Grow.Lens.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>
    /// Draws classic tree connectors at ancestor columns: T when the branch continues, L when the
    /// row is the last child. The current row's own foldout column is never used as a vertical guide.
    /// </summary>
    internal static class ItemIndentLine {
        private const float LineWidth = 1f;

        private const float DashLength = 2f;

        private const float DashGap = 2f;

        // Unity 2022.3's foldout arrow visual center sits one pixel right of the calculated
        // foldout center; the offset is constant across depths.
        private const float LineXOffset = 1f;

        private static bool _warned;

        internal static void OnHierarchyChanged() {
            HierarchyGuideProbe.Reset();
        }

        internal static void OnGameObjectGUI(int instanceID, Rect rect) {
            if (Event.current == null || Event.current.type != EventType.Repaint) {
                return;
            }

            var context = HierarchyGuideProbe.TryGetContext();
            if (context == null) {
                WarnOnce();
                return;
            }

            if (context.IsSearching) {
                return;
            }

            var baseIndent = context.BaseIndent;
            var indentWidth = context.IndentWidth;
            var foldoutWidth = context.FoldoutWidth;
            var marginLeft = context.MarginLeft;
            if (!HierarchyGuideGeometry.TryComputeDepth(rect.x, baseIndent, indentWidth, foldoutWidth,
                    marginLeft, out var derivedDepth) || derivedDepth <= 0) {
                return;
            }

            var rows = context.Rows;
            var rowHeight = context.RowHeight;
            if (rows == null || rowHeight <= 0f) {
                return;
            }

            var depths = context.GetRowDepths(rows);
            if (depths == null) {
                return;
            }

            var row = Mathf.RoundToInt((rect.y - context.TopRowMargin) / rowHeight);
            if (row < 0 || row >= rows.Count || rows[row] == null ||
                context.ItemId(rows[row]) != instanceID) {
                // Scrolled or animating rows can move; fall back to the data source lookup.
                row = context.GetRow(instanceID);
            }

            if (row < 0 || row >= rows.Count) {
                return;
            }

            var item = rows[row];
            if (item == null || context.ItemId(item) != instanceID) {
                return;
            }

            var depth = depths[row];
            if (depth != derivedDepth || depth <= 0) {
                return;
            }

            var midY = rect.y + rect.height * 0.5f;

            // Higher ancestors: their path child must have a following sibling for the vertical
            // line to continue through this row.
            for (var ancestor = 0; ancestor < depth - 1; ancestor++) {
                if (!HierarchyGuideGeometry.HasFollowingSibling(depths, row, ancestor + 1)) {
                    continue;
                }

                DrawVertical(rect,
                    HierarchyGuideGeometry.GuideX(baseIndent, indentWidth, foldoutWidth, ancestor));
            }

            // Parent column: top half always, bottom half only when this row has a following sibling.
            var parentX = HierarchyGuideGeometry.GuideX(baseIndent, indentWidth, foldoutWidth,
                depth - 1);
            var hasNextSibling = HierarchyGuideGeometry.HasFollowingSibling(depths, row, depth);
            var parentLineX = PixelLineStartX(parentX);
            var armY = PixelLineStartY(midY);
            DrawVertical(rect, parentX, rect.y, armY - rect.y, true);
            if (hasNextSibling) {
                var bottomY = armY + LineWidth;
                DrawVertical(rect, parentX, bottomY,
                    rect.y + rect.height - bottomY, false);
            }

            // Horizontal arm: stop at the foldout arrow for branches, reach the content for leaves.
            var endX = context.IsExpandable(item)
                ? parentX + indentWidth - foldoutWidth * 0.5f
                : rect.x - marginLeft;
            // The vertical is split around the arm's pixel row, so the arm owns that corner
            // pixel. Starting at the vertical's left edge fills the corner without overdraw.
            var armStart = parentLineX;
            if (endX > armStart + 0.5f) {
                DrawHorizontal(armY, armStart, endX);
            }
        }

        private static float PixelLineStartX(float center) {
            return HierarchyGuideGeometry.PixelSnappedStart(center, LineWidth) + LineXOffset;
        }

        private static float PixelLineStartY(float center) {
            return HierarchyGuideGeometry.PixelSnappedStart(center, LineWidth);
        }

        private static void DrawVertical(Rect rowRect, float x) {
            DrawVertical(rowRect, x, rowRect.y, rowRect.height, false);
        }

        private static void DrawVertical(Rect rowRect, float x, float y, float height,
            bool anchorAtEnd) {
            var lineX = PixelLineStartX(x);
            if (lineX + LineWidth <= 0f || lineX >= rowRect.xMax) {
                return;
            }

            var color = GrowLensSettings.HierarchyGuideColor;
            if (!GrowLensSettings.HierarchyGuideDashed) {
                EditorGUI.DrawRect(new Rect(lineX, y, LineWidth, height), color);
                return;
            }

            if (anchorAtEnd) {
                for (var end = height; end > 0f; end -= DashLength + DashGap) {
                    var start = Mathf.Max(0f, end - DashLength);
                    EditorGUI.DrawRect(
                        new Rect(lineX, y + start, LineWidth, end - start), color);
                }

                return;
            }

            for (var offset = 0f; offset < height; offset += DashLength + DashGap) {
                var segment = Mathf.Min(DashLength, height - offset);
                EditorGUI.DrawRect(new Rect(lineX, y + offset, LineWidth, segment), color);
            }
        }

        private static void DrawHorizontal(float lineY, float startX, float endX) {
            var color = GrowLensSettings.HierarchyGuideColor;
            if (!GrowLensSettings.HierarchyGuideDashed) {
                EditorGUI.DrawRect(new Rect(startX, lineY, endX - startX, LineWidth), color);
                return;
            }

            for (var offset = 0f; offset < endX - startX; offset += DashLength + DashGap) {
                var segment = Mathf.Min(DashLength, endX - startX - offset);
                EditorGUI.DrawRect(
                    new Rect(startX + offset, lineY, segment, LineWidth), color);
            }
        }

        private static void WarnOnce() {
            if (_warned) {
                return;
            }

            _warned = true;
            Debug.LogWarning(
                "[Grow Lens] Hierarchy tree connectors are skipped because the current Hierarchy " +
                "tree view geometry could not be resolved.");
        }
    }
}
