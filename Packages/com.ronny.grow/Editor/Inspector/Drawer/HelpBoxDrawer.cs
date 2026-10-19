// SPDX-License-Identifier: MIT

using Grow.Core.Attributes;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Inspector.Drawer {
    /// <summary>
    /// The Inspector drawer for <see cref="GrowHelpBoxAttribute"/> (a DecoratorDrawer): draws a help box above the
    /// field.
    /// <para>The height is computed from the wrapped height of the message in the helpBox style, so the text is
    /// always fully shown.</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(GrowHelpBoxAttribute))]
    public class HelpBoxDrawer : DecoratorDrawer {
        /// <summary>The <see cref="GrowHelpBoxAttribute"/> instance carried by this decorator.</summary>
        private GrowHelpBoxAttribute HelpBox => (GrowHelpBoxAttribute)attribute;

        /// <summary>Computes the help box height in the helpBox style (wrapping included).</summary>
        /// <returns>The required height.</returns>
        public override float GetHeight() {
            var message = HelpBox.Message;
            if (string.IsNullOrEmpty(message)) {
                return EditorGUIUtility.singleLineHeight;
            }

            // DecoratorDrawer.GetHeight has no concrete Rect width, so approximate the wrapping
            // with the current view width
            var content = new GUIContent(message);
            var width = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 60f);
            return Mathf.Max(EditorGUIUtility.singleLineHeight, EditorStyles.helpBox.CalcHeight(content, width));
        }

        /// <summary>Draws the help box.</summary>
        /// <param name="position">The rect allocated to the decorator.</param>
        public override void OnGUI(Rect position) {
            if (string.IsNullOrEmpty(HelpBox.Message)) {
                return;
            }

            var messageType = HelpBox.Kind switch {
                GrowHelpBoxKind.Warning => MessageType.Warning,
                GrowHelpBoxKind.Error => MessageType.Error,
                _ => MessageType.Info
            };
            EditorGUI.HelpBox(position, HelpBox.Message, messageType);
        }
    }
}
