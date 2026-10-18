// SPDX-License-Identifier: MIT

using Grow.Core.Log;
using Grow.Kernel.Entry;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Settings.Preferences {
    /// <summary>
    /// Read-only <b>Preferences &gt; Grow</b> page.
    /// </summary>
    /// <remarks>
    /// The page reports session state that the kernel already owns. It deliberately exposes no
    /// persisted user setting yet; a setting is added only when a feature consumes it.
    /// </remarks>
    internal sealed class GrowUserSettingsProvider : GrowSettingsProviderBase {
        private GrowUserSettingsProvider(string path, SettingsScope scope) : base(path, scope) {
        }

        /// <summary>Registers the page under <b>Preferences &gt; Grow</b>.</summary>
        [SettingsProvider]
        public static SettingsProvider Create() {
            return new GrowUserSettingsProvider(
                GrowSettingsConstants.UserProviderPath, SettingsScope.User) {
                label = "Grow",
                keywords = GrowSettingsConstants.Keywords
            };
        }

        /// <inheritdoc />
        protected override void DrawContent(string searchContext) {
            EditorGUILayout.LabelField("Grow", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This page is read-only. User-level settings are added only when a feature " +
                "consumes them.", MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);

            DrawValue("Log level", GrowLog.MinimumLevel.ToString());
            DrawValue("Log sink", GrowLog.Sink.GetType().Name);
            DrawValue("Kernel initialized", GrowApp.IsInitialized ? "Yes" : "No");
            DrawValue("Auto initialize", GrowApp.AutoInitialize ? "Enabled" : "Disabled");

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "GrowLog resets to Info at the start of each play session.",
                MessageType.None);
        }

        private static void DrawValue(string label, string value) {
            EditorGUILayout.LabelField(label, string.IsNullOrEmpty(value) ? "-" : value);
        }
    }
}
