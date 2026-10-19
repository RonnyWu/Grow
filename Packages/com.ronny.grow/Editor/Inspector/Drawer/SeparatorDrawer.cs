// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Inspector.Drawer {
    /// <summary>
    /// The Inspector drawer for <see cref="GrowSeparatorAttribute"/> (a DecoratorDrawer): draws a horizontal
    /// separator line above the field.
    /// </summary>
    [CustomPropertyDrawer(typeof(GrowSeparatorAttribute))]
    public class SeparatorDrawer : DecoratorDrawer {
        /// <summary>The <see cref="GrowSeparatorAttribute"/> instance carried by this decorator.</summary>
        private GrowSeparatorAttribute Separator => (GrowSeparatorAttribute)attribute;

        /// <summary>The height the separator occupies = vertical margin * 2 + line thickness.</summary>
        /// <returns>The required height.</returns>
        public override float GetHeight() {
            return Separator.Margin * 2f + Separator.Thickness;
        }

        /// <summary>Draws the horizontal separator (vertically centered, with a small inset on each side).</summary>
        /// <param name="position">The rect allocated to the decorator.</param>
        public override void OnGUI(Rect position) {
            var thickness = Separator.Thickness;
            if (thickness <= 0f) {
                return;
            }

            // Center the line vertically and leave a small inset on each side
            var lineRect = new Rect(position.x + 2f,
                position.y + (position.height - thickness) * 0.5f,
                position.width - 4f,
                thickness);

            var prevColor = GUI.color;
            GUI.color = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.18f) : new Color(0f, 0f, 0f, 0.22f);
            GUI.DrawTexture(lineRect, EditorGUIUtility.whiteTexture);
            GUI.color = prevColor;
        }
    }
}
