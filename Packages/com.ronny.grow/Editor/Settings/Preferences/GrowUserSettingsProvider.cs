// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using Grow.Core.Log;
using Grow.Editor.Localization;
using Grow.Editor.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Grow.Editor.Settings.Preferences {
    /// <summary>
    /// <b>Preferences &gt; Grow &gt; General</b> page.
    /// </summary>
    /// <remarks>
    /// The page owns two kinds of state: a persisted user-global language preference and a
    /// session-only log level. The language file is only written after an explicit change.
    /// </remarks>
    internal sealed class GrowUserSettingsProvider : GrowSettingsProviderBase {
        internal const string ProviderPath = "Preferences/Grow/General";

        private GrowUserSettingsProvider(string path, SettingsScope scope) : base(path, scope) {
        }

        /// <summary>Registers the page under <b>Preferences &gt; Grow &gt; General</b>.</summary>
        [SettingsProvider]
        public static SettingsProvider Create() {
            return new GrowUserSettingsProvider(ProviderPath, SettingsScope.User) {
                label = GrowLocalization.Get(GrowSettingsKeys.PreferencesGeneralTitle),
                keywords = GrowLocalization.Keywords(
                    GrowSettingsKeys.PreferencesGeneralTitle,
                    GrowSettingsKeys.PreferencesLanguage)
            };
        }

        /// <inheritdoc />
        public override void OnActivate(string searchContext, VisualElement rootElement) {
            GrowEditorPreferencesStore.Store.ReloadIfChangedOnDisk();
            GrowLocalization.NormalizeLanguagePreference();
        }

        /// <inheritdoc />
        public override void OnDeactivate() {
            GrowEditorPreferencesStore.Store.Flush();
            base.OnDeactivate();
        }

        /// <inheritdoc />
        protected override void DrawContent(string searchContext) {
            var loadResult = GrowEditorPreferencesStore.Store.LoadResult;

            EditorGUILayout.LabelField("Grow", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                GrowLocalization.Get(GrowSettingsKeys.PreferencesGeneralHelp), MessageType.Info);

            if (loadResult.Status == GrowSettingsLoadStatus.Corrupted) {
                EditorGUILayout.HelpBox(
                    GrowLocalization.Get(GrowSettingsKeys.PreferencesCorrupted),
                    MessageType.Error);
            } else if (loadResult.Status == GrowSettingsLoadStatus.MigrationFailed) {
                EditorGUILayout.HelpBox(
                    GrowLocalization.Get(GrowSettingsKeys.PreferencesMigrationFailed) +
                    "\n" + loadResult.Message,
                    MessageType.Error);
            } else if (loadResult.Status == GrowSettingsLoadStatus.NewerSchema) {
                EditorGUILayout.HelpBox(
                    GrowLocalization.Get(GrowSettingsKeys.PreferencesNewerSchema),
                    MessageType.Warning);
            } else if (loadResult.Status == GrowSettingsLoadStatus.Conflict) {
                EditorGUILayout.HelpBox(
                    GrowLocalization.Get(GrowSettingsKeys.PreferencesConflict),
                    MessageType.Warning);
            }

            EditorGUILayout.Space(8);

            using (new EditorGUI.DisabledScope(!loadResult.CanSave)) {
                DrawLanguage();
            }

            if (loadResult.Status == GrowSettingsLoadStatus.Corrupted &&
                GUILayout.Button(GrowLocalization.Get(GrowSettingsKeys.PreferencesReset))) {
                ConfirmReset(loadResult);
            }

            if (loadResult.Status == GrowSettingsLoadStatus.Conflict &&
                GUILayout.Button(GrowLocalization.Get(GrowSettingsKeys.PreferencesReload))) {
                GrowEditorPreferencesStore.Store.ReloadFromDisk();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(
                GrowLocalization.Get(GrowSettingsKeys.PreferencesSession),
                EditorStyles.boldLabel);

            var current = GrowLog.MinimumLevel;
            var next = (LogLevel)EditorGUILayout.EnumPopup(
                new GUIContent(
                    GrowLocalization.Get(GrowSettingsKeys.PreferencesMinimumLogLevel),
                    GrowLocalization.Get(
                        GrowSettingsKeys.PreferencesMinimumLogLevelTooltip)),
                current);

            if (next != current) {
                GrowLog.MinimumLevel = next;
            }

            GrowSettingsUi.DrawValue(
                GrowLocalization.Get(GrowSettingsKeys.PreferencesLogSink),
                GrowLog.Sink.GetType().Name);

            if (GUILayout.Button(
                GrowLocalization.Get(GrowSettingsKeys.PreferencesResetToInfo))) {
                GrowLog.MinimumLevel = LogLevel.Info;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                GrowLocalization.Get(GrowSettingsKeys.PreferencesSessionNote),
                MessageType.None);
        }

        private static void DrawLanguage() {
            var languages = GrowLocalization.Languages;
            var options = new List<string>(languages.Count + 1) {
                GrowLocalization.Get(GrowSettingsKeys.PreferencesLanguageSystem)
            };

            for (var i = 0; i < languages.Count; i++) {
                options.Add(languages[i].NativeName);
            }

            var preference = GrowLocalization.LanguagePreference;
            var selected = 0;

            for (var i = 0; i < languages.Count; i++) {
                if (languages[i].Code == preference) {
                    selected = i + 1;
                    break;
                }
            }

            var next = EditorGUILayout.Popup(
                GrowLocalization.Get(GrowSettingsKeys.PreferencesLanguage),
                selected,
                options.ToArray());

            if (next == selected) {
                return;
            }

            GrowLocalization.SetLanguage(
                next == 0 ? GrowLocalization.SystemLanguagePreference : languages[next - 1].Code);
        }

        private static void ConfirmReset(GrowSettingsLoadResult loadResult) {
            var path = string.IsNullOrEmpty(loadResult.QuarantinePath)
                ? "-"
                : loadResult.QuarantinePath;

            if (!EditorUtility.DisplayDialog(
                GrowLocalization.Get(GrowSettingsKeys.PreferencesResetTitle),
                GrowLocalization.Format(
                    GrowSettingsKeys.PreferencesResetMessage, path),
                GrowLocalization.Get(GrowSettingsKeys.PreferencesResetConfirm),
                GrowLocalization.Get(GrowSettingsKeys.PreferencesCancel))) {
                return;
            }

            GrowEditorPreferencesStore.Store.ResetToDefaults();
        }
    }
}
