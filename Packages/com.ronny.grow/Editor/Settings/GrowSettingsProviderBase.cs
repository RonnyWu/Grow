// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Settings {
    /// <summary>
    /// Base class for Grow <see cref="SettingsProvider"/> pages in both the project and user
    /// scopes.
    /// </summary>
    /// <remarks>
    /// Unity's built-in settings pages wrap their content in a fixed left margin and a fixed
    /// label width. Custom providers receive neither, so this base reproduces those two parts of
    /// <c>SettingsWindow.GUIScope</c> and contains page failures instead of letting them escape
    /// into the Settings window.
    /// </remarks>
    internal abstract class GrowSettingsProviderBase : SettingsProvider {
        protected GrowSettingsProviderBase(string path, SettingsScope scope) : base(path, scope) {
        }

        /// <inheritdoc />
        public sealed override void OnGUI(string searchContext) {
            using (new GrowSettingsGuiScope()) {
                try {
                    DrawContent(searchContext);
                } catch (ExitGUIException) {
                    throw;
                } catch (Exception exception) {
                    UnityEngine.Debug.LogException(exception);
                    EditorGUILayout.HelpBox(
                        "The Grow settings page failed to draw. See the Console for details.",
                        MessageType.Error);
                }
            }
        }

        /// <summary>Draws the page body inside the standard margin wrapper.</summary>
        protected abstract void DrawContent(string searchContext);
    }
}
