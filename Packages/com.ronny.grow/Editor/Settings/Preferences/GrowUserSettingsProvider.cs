// SPDX-License-Identifier: MIT

using Grow.Core.Log;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Settings.Preferences {
    /// <summary>
    /// <b>Preferences &gt; Grow &gt; General</b> page.
    /// </summary>
    /// <remarks>
    /// The page exposes one real session setting: the minimum log level. It is not persisted;
    /// <see cref="GrowLog.Reset"/> restores the default at the start of every play session.
    /// Persisted user settings are added only when a feature consumes them.
    /// </remarks>
    internal sealed class GrowUserSettingsProvider : GrowSettingsProviderBase {
        internal const string ProviderPath = "Preferences/Grow/General";

        private GrowUserSettingsProvider(string path, SettingsScope scope) : base(path, scope) {
        }

        /// <summary>Registers the page under <b>Preferences &gt; Grow &gt; General</b>.</summary>
        [SettingsProvider]
        public static SettingsProvider Create() {
            return new GrowUserSettingsProvider(ProviderPath, SettingsScope.User) {
                label = "Grow",
                keywords = new[] { "grow", "log" }
            };
        }

        /// <inheritdoc />
        protected override void DrawContent(string searchContext) {
            EditorGUILayout.LabelField("Grow", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This page only contains session state. Persisted user settings are added only " +
                "when a feature consumes them.", MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);

            var current = GrowLog.MinimumLevel;
            var next = (LogLevel)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Minimum log level",
                    "Applies to this editor session only. Play Mode resets it to Info."),
                current);

            if (next != current) {
                GrowLog.MinimumLevel = next;
            }

            GrowSettingsUi.DrawValue("Log sink", GrowLog.Sink.GetType().Name);

            if (GUILayout.Button("Reset to Info")) {
                GrowLog.MinimumLevel = LogLevel.Info;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "GrowLog resets to Info at the start of each play session.",
                MessageType.None);
        }
    }
}
