// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.IO;
using Grow.Editor.Localization;
using Grow.Editor.Settings;
using Grow.Lens.Editor.Core;
using Grow.Lens.Editor.Localization;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Settings {
    /// <summary>Lens preferences drawn with Unity's native serialized fields.</summary>
    internal sealed class GrowLensSettingsProvider : GrowSettingsPageBase {
        internal const string ProviderPath = "Preferences/Grow/Lens";

        private const string StatusOnKey = "grow.lens.preferences.statusOn";
        private const string StatusOffKey = "grow.lens.preferences.statusOff";
        private const string GroupStatePrefix = "grow.lens.preferences.groupState.";

        private static readonly string[] KeywordKeys = {
            "master", "hierarchyGroup", "inspectorGroup", "projectGroup", "toolbarGroup", "icons",
            "componentInspector", "separators", "separatorMenu", "guides", "guideDashed",
            "guideColor", "activeToggle", "hierarchyContextMenus", "projectContextMenus",
            "assetHeader", "toolbar", "resetAll", "revealFile"
        };

        private static GrowLensSettingsProvider _instance;

        private GrowLensSettingsProxy _proxy;
        private SerializedObject _serialized;
        private bool _resetGuideColor;

        private GrowLensSettingsProvider(string path, SettingsScope scope) : base(path, scope) {
        }

        /// <summary>Registers the page under <b>Preferences &gt; Grow &gt; Lens</b>.</summary>
        [SettingsProvider]
        public static SettingsProvider Create() {
            var provider = new GrowLensSettingsProvider(ProviderPath, SettingsScope.User) {
                label = "Lens"
            };

            provider.RefreshKeywords();
            if (_instance != null) {
                GrowLocalization.LanguageChanged -= _instance.RefreshKeywords;
            }

            _instance = provider;
            GrowLocalization.LanguageChanged += provider.RefreshKeywords;
            return provider;
        }

        /// <inheritdoc />
        public override void OnDeactivate() {
            if (_proxy != null) {
                Object.DestroyImmediate(_proxy);
            }

            base.OnDeactivate();
        }

        /// <inheritdoc />
        protected override string FailureMessage => Text("grow.lens.preferences.failed");

        /// <inheritdoc />
        protected override void DrawContent(string searchContext) {
            EnsureProxy();

            var snapshot = GrowLensSettings.Snapshot;
            var persistedColor = snapshot.HierarchyGuideColor;
            var allHierarchy = GrowLensSettings.AllHierarchy(snapshot);
            var mixedHierarchy = !allHierarchy && GrowLensSettings.AnyHierarchy(snapshot);
            var hierarchyInput = mixedHierarchy || allHierarchy;

            _proxy.Data = snapshot;
            _proxy.Data.HierarchyGuideColor = GrowLensSettings.HierarchyGuideColor;
            _proxy.HierarchyGroup = hierarchyInput;
            _serialized.Update();

            DrawStatus();
            using (new EditorGUI.DisabledScope(!GrowLensSettings.CanSave)) {
                Field("master", "Data.Enabled");
            }

            using (new EditorGUI.IndentLevelScope()) {
                EditorGUILayout.LabelField(
                    Text(snapshot.Enabled ? StatusOnKey : StatusOffKey), EditorStyles.miniLabel);
            }

            using (new EditorGUI.DisabledScope(
                !GrowLensSettings.CanSave || !snapshot.Enabled)) {
                DrawHierarchy(snapshot, allHierarchy, mixedHierarchy);
                DrawSeparatorSection(snapshot);
                DrawSection("inspectorGroup", "assetHeader", "AssetHeader", snapshot.AssetHeader);
                DrawSection("projectGroup", "projectContextMenus", "ProjectContextMenus",
                    snapshot.ProjectContextMenus);
                DrawSection("toolbarGroup", "toolbar", "Toolbar", snapshot.Toolbar);
            }

            DrawFooter();
            if (!_serialized.ApplyModifiedPropertiesWithoutUndo() && !_resetGuideColor) {
                return;
            }

            var candidate = _proxy.Data;
            if (_proxy.HierarchyGroup != hierarchyInput) {
                GrowLensSettings.SetHierarchyFeatures(candidate,
                    mixedHierarchy || _proxy.HierarchyGroup);
            }

            if (candidate.HierarchyGuideColor != GrowLensSettings.HierarchyGuideColor) {
                candidate.HierarchyGuideCustomColor = true;
            } else {
                candidate.HierarchyGuideColor = persistedColor;
            }

            if (_resetGuideColor) {
                candidate.HierarchyGuideCustomColor = false;
                _resetGuideColor = false;
            }

            GrowLensSettings.Apply(candidate);
        }

        private void DrawHierarchy(GrowLensSettings.Data data, bool all, bool mixed) {
            Group("hierarchyGroup", "HierarchyGroup", all, mixed);

            using (new EditorGUI.IndentLevelScope()) {
                Field("guides", "Data.HierarchyIndentGuides");
                using (new EditorGUI.DisabledScope(!data.HierarchyIndentGuides)) {
                    Field("guideDashed", "Data.HierarchyGuideDashed");
                    DrawGuideColor();
                }

                Field("icons", "Data.HierarchyComponentIcons");
                using (new EditorGUI.IndentLevelScope())
                using (new EditorGUI.DisabledScope(!data.HierarchyComponentIcons)) {
                    Field("componentInspector", "Data.HierarchyComponentInspector");
                }

                Field("activeToggle", "Data.HierarchyActiveToggle");
                Field("hierarchyContextMenus", "Data.HierarchyContextMenus");
            }
        }

        private void DrawSeparatorSection(GrowLensSettings.Data data) {
            Group("separators", "Data.HierarchySeparators", data.HierarchySeparators, false);

            using (new EditorGUI.IndentLevelScope()) {
                using (new EditorGUI.DisabledScope(!data.HierarchySeparators)) {
                    Field("separatorMenu", "Data.HierarchySeparatorMenu");
                }

                using (new EditorGUI.IndentLevelScope()) {
                    EditorGUILayout.LabelField(
                        Text("grow.lens.preferences.separatorsHint"), EditorStyles.miniLabel);
                }
            }
        }

        private void DrawSection(string groupKey, string childKey, string property, bool value) {
            Group(groupKey, "Data." + property, value, false);

            using (new EditorGUI.IndentLevelScope()) {
                Field(childKey, "Data." + property);
            }
        }

        private void DrawGuideColor() {
            using (new EditorGUILayout.HorizontalScope()) {
                Field("guideColor", "Data.HierarchyGuideColor",
                    "#" + ColorUtility.ToHtmlStringRGBA(GrowLensSettings.HierarchyGuideColor));

                using (new EditorGUI.DisabledScope(!GrowLensSettings.HierarchyGuideColorCustomized)) {
                    if (GUILayout.Button(Content("guideColorReset"), GUILayout.ExpandWidth(false))) {
                        _resetGuideColor = true;
                    }
                }
            }
        }

        private static void DrawFooter() {
            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField(Text("grow.lens.preferences.languageHint"),
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button(Content("openLanguage"), GUILayout.ExpandWidth(false))) {
                    SettingsService.OpenUserPreferences("Preferences/Grow/General");
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(Content("revealFile"), GUILayout.ExpandWidth(false))) {
                    RevealSettingsFile();
                }

                using (new EditorGUI.DisabledScope(!GrowLensSettings.CanSave)) {
                    if (GUILayout.Button(Content("resetAll"), GUILayout.ExpandWidth(false))) {
                        ConfirmReset();
                    }
                }
            }
        }

        private static void DrawStatus() {
            var key = GrowLensSettings.Status switch {
                GrowLensSettingsStatus.Corrupted => "corrupted",
                GrowLensSettingsStatus.NewerSchema => "newerSchema",
                GrowLensSettingsStatus.ReadOnly => "readOnly",
                GrowLensSettingsStatus.WriteFailed => "writeFailed",
                _ => null
            };

            if (key == null) {
                return;
            }

            var type = GrowLensSettings.Status is GrowLensSettingsStatus.NewerSchema or
                GrowLensSettingsStatus.ReadOnly
                ? MessageType.Warning
                : MessageType.Error;
            EditorGUILayout.HelpBox(Text("grow.lens.preferences." + key), type);
            EditorGUILayout.Space(8f);
        }

        /// <summary>Draws one native label/control row.</summary>
        private void Field(string key, string path, string suffix = null) {
            var content = Content(key);
            if (suffix != null) {
                content.text += "  " + suffix;
            }

            EditorGUILayout.PropertyField(_serialized.FindProperty(path), content);
        }

        /// <summary>Uses the native mixed state only for the derived Hierarchy group.</summary>
        private void Group(string key, string path, bool all, bool mixed) {
            EditorGUILayout.Space(8f);
            var previous = EditorGUI.showMixedValue;
            try {
                EditorGUI.showMixedValue = mixed;
                Field(key, path, Text(GroupStatePrefix +
                    (mixed ? "partial" : all ? "allOn" : "allOff")));
            } finally {
                EditorGUI.showMixedValue = previous;
            }
        }

        private void EnsureProxy() {
            if (_proxy != null) {
                return;
            }

            _proxy = ScriptableObject.CreateInstance<GrowLensSettingsProxy>();
            _proxy.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
            _serialized = new SerializedObject(_proxy);
        }

        private static GUIContent Content(string key) {
            var full = "grow.lens.preferences." + key;
            var tooltip = full + ".tooltip";
            var text = Text(full);
            var tip = Text(tooltip);
            return new GUIContent(text == full ? key : text, tip == tooltip ? null : tip);
        }

        private static string Text(string key) {
            return GrowLensLocalization.Get(key);
        }

        private static void ConfirmReset() {
            var confirmed = EditorUtility.DisplayDialog(
                Text("grow.lens.preferences.resetConfirmTitle"),
                Text("grow.lens.preferences.resetConfirmMessage"),
                Text("grow.lens.preferences.resetConfirmOk"),
                Text("grow.lens.preferences.cancel"));

            if (confirmed) {
                GrowLensSettings.ResetToDefaults();
                GUIUtility.ExitGUI();
            }
        }

        private static void RevealSettingsFile() {
            var path = GrowLensSettings.FilePath;
            if (File.Exists(path)) {
                EditorUtility.RevealInFinder(path);
            } else if (Directory.Exists(Path.GetDirectoryName(path))) {
                EditorUtility.RevealInFinder(Path.GetDirectoryName(path));
            } else {
                EditorUtility.RevealInFinder(Path.GetDirectoryName(Application.dataPath) ?? ".");
            }
        }

        /// <summary>Rebuilds the search keywords from the current language.</summary>
        private void RefreshKeywords() {
            var keywords = new List<string> { "Grow", "Lens", "Hierarchy", "Inspector", "Project" };
            foreach (var key in KeywordKeys) {
                keywords.Add(Text("grow.lens.preferences." + key));
            }

            this.keywords = keywords;
        }
    }
}
