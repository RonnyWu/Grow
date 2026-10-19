// SPDX-License-Identifier: MIT

using System.Collections;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Hierarchy {
    /// <summary>
    /// Draws classic tree connectors at ancestor columns: T when the branch continues, L when the
    /// row is the last child. The current row's own foldout column is never used as a vertical guide.
    /// </summary>
    internal static class ItemIndentLine {
        private const int DashTextureHeight = 16;
        private const int DashTextureWidth = 16;
        private const float LineWidth = 1f;

        private static Texture2D _verticalDash;
        private static Texture2D _horizontalDash;
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

            EnsureDashTextures();
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
            DrawVertical(rect, parentX, hasNextSibling ? rect.height : rect.height * 0.5f);

            // Horizontal arm: stop at the foldout arrow for branches, reach the content for leaves.
            var endX = context.IsExpandable(item)
                ? parentX + indentWidth - foldoutWidth * 0.5f
                : rect.x - marginLeft;
            if (endX > parentX + 0.5f) {
                DrawHorizontal(midY, parentX, endX);
            }
        }

        private static void DrawVertical(Rect rowRect, float x) {
            DrawVertical(rowRect, x, rowRect.height);
        }

        private static void DrawVertical(Rect rowRect, float x, float height) {
            var lineRect = new Rect(x, rowRect.y, LineWidth, height);
            if (lineRect.x >= 0f && lineRect.x < rowRect.xMax) {
                GUI.DrawTexture(lineRect, _verticalDash, ScaleMode.StretchToFill);
            }
        }

        private static void DrawHorizontal(float y, float startX, float endX) {
            var lineRect = new Rect(startX, y, endX - startX, LineWidth);
            GUI.DrawTexture(lineRect, _horizontalDash, ScaleMode.StretchToFill);
        }

        private static void EnsureDashTextures() {
            if (_verticalDash != null && _horizontalDash != null) {
                return;
            }

            var color = EditorStyles.label.normal.textColor;
            color.a = 0.32f;
            var clear = new Color(0, 0, 0, 0);

            if (_verticalDash == null) {
                var pixels = new Color[DashTextureHeight];
                for (var y = 0; y < DashTextureHeight; y++) {
                    pixels[y] = y / 2 % 2 == 0 ? color : clear;
                }

                _verticalDash = new Texture2D(1, DashTextureHeight, TextureFormat.RGBA32, false) {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };
                _verticalDash.SetPixels(pixels);
                _verticalDash.Apply();
            }

            if (_horizontalDash == null) {
                var pixels = new Color[DashTextureWidth];
                for (var x = 0; x < DashTextureWidth; x++) {
                    pixels[x] = x / 2 % 2 == 0 ? color : clear;
                }

                _horizontalDash = new Texture2D(DashTextureWidth, 1, TextureFormat.RGBA32, false) {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };
                _horizontalDash.SetPixels(pixels);
                _horizontalDash.Apply();
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
