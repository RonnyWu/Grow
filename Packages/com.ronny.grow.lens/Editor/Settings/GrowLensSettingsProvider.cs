// SPDX-License-Identifier: MIT

using Grow.Editor.Localization;
using Grow.Editor.Settings;
using Grow.Lens.Editor.Core;
using Grow.Lens.Editor.Localization;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Settings {
    /// <summary>Preferences page for the optional Grow Lens editor enhancements.</summary>
    internal sealed class GrowLensSettingsProvider : GrowSettingsPageBase {
        internal const string ProviderPath = "Preferences/Grow/Lens";

        private static GrowLensSettingsProvider _instance;

        private GrowLensSettingsProxy _proxy;
        private SerializedObject _serialized;
        private SerializedProperty _enabled;
        private SerializedProperty _inspectorGroup;
        private SerializedProperty _projectGroup;
        private SerializedProperty _hierarchyGroup;
        private SerializedProperty _toolbarGroup;
        private SerializedProperty _hierarchyComponentIcons;
        private SerializedProperty _hierarchySeparators;
        private SerializedProperty _hierarchyIndentGuides;
        private SerializedProperty _hierarchyGuideDashed;
        private SerializedProperty _hierarchyGuideColor;
        private SerializedProperty _hierarchyActiveToggle;
        private SerializedProperty _hierarchyContextMenus;
        private SerializedProperty _projectContextMenus;
        private SerializedProperty _assetHeader;
        private SerializedProperty _toolbar;
        private bool _resetGuideColor;

        private GrowLensSettingsProvider(string path, SettingsScope scope) : base(path, scope) {
        }

        /// <summary>Registers the page under <b>Preferences &gt; Grow &gt; Lens</b>.</summary>
        [SettingsProvider]
        public static SettingsProvider Create() {
            var provider = new GrowLensSettingsProvider(ProviderPath, SettingsScope.User) {
                // "Grow" is the category; the page itself is just "Lens", so the tree reads
                // Grow > Lens instead of Grow > Grow Lens.
                label = "Lens"
            };

            provider.RefreshKeywords();

            // The keywords come from the locale files, and a provider instance is created once
            // per domain. Rebuild them when the shared Grow language changes; the language
            // service also repaints the window, so the new labels show up immediately.
            if (_instance != null) {
                GrowLocalization.LanguageChanged -= _instance.RefreshKeywords;
            }

            _instance = provider;
            GrowLocalization.LanguageChanged += provider.RefreshKeywords;
            return provider;
        }

        /// <summary>Rebuilds the search keywords from the current language.</summary>
        private void RefreshKeywords() {
            keywords = new[] {
                "Grow", "Lens", "Hierarchy", "Inspector", "Project", "Context Menu",
                GrowLensLocalization.Get("grow.lens.preferences.title"),
                GrowLensLocalization.Get("grow.lens.preferences.inspectorGroup"),
                GrowLensLocalization.Get("grow.lens.preferences.projectGroup"),
                GrowLensLocalization.Get("grow.lens.preferences.hierarchyGroup"),
                GrowLensLocalization.Get("grow.lens.preferences.toolbarGroup"),
                GrowLensLocalization.Get("grow.lens.preferences.activeToggle"),
                GrowLensLocalization.Get("grow.lens.preferences.projectContextMenus"),
                GrowLensLocalization.Get("grow.lens.preferences.hierarchyContextMenus")
            };
        }

        /// <inheritdoc />
        public override void OnDeactivate() {
            DisposeProxy();
            base.OnDeactivate();
        }

        /// <inheritdoc />
        protected override string FailureMessage =>
            GrowLensLocalization.Get("grow.lens.preferences.failed");

        /// <inheritdoc />
        protected override void DrawContent(string searchContext) {
            EnsureProxy();

            var snapshot = GrowLensSettings.Snapshot;
            SyncProxy(snapshot);
            DrawStatus(GrowLensSettings.Status);

            // A read-only file still shows its values; the controls are disabled instead of
            // accepting edits that the file would refuse.
            using (new EditorGUI.DisabledScope(!GrowLensSettings.CanSave)) {
                DrawSettings(snapshot);
            }

            if (!_serialized.ApplyModifiedPropertiesWithoutUndo()) {
                return;
            }

            // One frame becomes one candidate, one save and at most one host update.
            GrowLensSettings.TryApply(BuildEdits(snapshot));
            _resetGuideColor = false;
        }

        private void DrawSettings(GrowLensSettings.Data snapshot) {
            EditorGUILayout.PropertyField(_enabled, Content(
                "grow.lens.preferences.master", "grow.lens.preferences.master.tooltip"));

            DrawGroup(_inspectorGroup, "grow.lens.preferences.inspectorGroup", false);
            EditorGUI.indentLevel++;
            try {
                DrawFeature(_assetHeader, "grow.lens.preferences.assetHeader");
            } finally {
                EditorGUI.indentLevel--;
            }

            DrawGroup(_projectGroup, "grow.lens.preferences.projectGroup", false);
            EditorGUI.indentLevel++;
            try {
                DrawFeature(_projectContextMenus, "grow.lens.preferences.projectContextMenus");
            } finally {
                EditorGUI.indentLevel--;
            }

            var allHierarchy = GrowLensSettings.AllHierarchy(snapshot);
            var mixedHierarchy = !allHierarchy && GrowLensSettings.AnyHierarchy(snapshot);
            DrawGroup(_hierarchyGroup, "grow.lens.preferences.hierarchyGroup", mixedHierarchy);
            EditorGUI.indentLevel++;
            try {
                DrawFeature(_hierarchyComponentIcons, "grow.lens.preferences.icons");
                DrawFeature(_hierarchySeparators, "grow.lens.preferences.separators");
                DrawFeature(_hierarchyIndentGuides, "grow.lens.preferences.guides");
                using (new EditorGUI.IndentLevelScope())
                using (new EditorGUI.DisabledScope(!_hierarchyIndentGuides.boolValue)) {
                    DrawGuideSettings();
                }

                DrawFeature(_hierarchyActiveToggle, "grow.lens.preferences.activeToggle");
                DrawFeature(_hierarchyContextMenus, "grow.lens.preferences.hierarchyContextMenus");
            } finally {
                EditorGUI.indentLevel--;
            }

            DrawGroup(_toolbarGroup, "grow.lens.preferences.toolbarGroup", false);
            EditorGUI.indentLevel++;
            try {
                DrawFeature(_toolbar, "grow.lens.preferences.toolbar");
            } finally {
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>Reports a settings file that cannot be saved instead of failing silently.</summary>
        private static void DrawStatus(GrowLensSettingsStatus status) {
            var key = status switch {
                GrowLensSettingsStatus.Corrupted => "grow.lens.preferences.corrupted",
                GrowLensSettingsStatus.NewerSchema => "grow.lens.preferences.newerSchema",
                GrowLensSettingsStatus.ReadOnly => "grow.lens.preferences.readOnly",
                GrowLensSettingsStatus.WriteFailed => "grow.lens.preferences.writeFailed",
                _ => null
            };

            if (key == null) {
                return;
            }

            var type = status == GrowLensSettingsStatus.WriteFailed
                ? MessageType.Error
                : MessageType.Warning;
            EditorGUILayout.HelpBox(GrowLensLocalization.Get(key), type);
        }

        /// <summary>
        /// Turns the proxy into the frame's edit set. Only values that differ from the snapshot
        /// are reported, so an untouched frame produces no candidate and no save.
        /// </summary>
        private GrowLensPageEdits BuildEdits(GrowLensSettings.Data snapshot) {
            var edits = new GrowLensPageEdits();

            if (_enabled.boolValue != snapshot.Enabled) {
                edits.Master = _enabled.boolValue;
            }

            if (_inspectorGroup.boolValue != snapshot.AssetHeader) {
                edits.InspectorGroup = _inspectorGroup.boolValue;
            }

            if (_projectGroup.boolValue != snapshot.ProjectContextMenus) {
                edits.ProjectGroup = _projectGroup.boolValue;
            }

            if (_hierarchyGroup.boolValue != GrowLensSettings.AllHierarchy(snapshot)) {
                edits.HierarchyGroup = _hierarchyGroup.boolValue;
            }

            if (_toolbarGroup.boolValue != snapshot.Toolbar) {
                edits.ToolbarGroup = _toolbarGroup.boolValue;
            }

            ApplyFeatureEdit(ref edits, _assetHeader, snapshot, GrowLensFeature.AssetHeader);
            ApplyFeatureEdit(ref edits, _projectContextMenus, snapshot,
                GrowLensFeature.ProjectContextMenus);
            ApplyFeatureEdit(ref edits, _hierarchyComponentIcons, snapshot,
                GrowLensFeature.HierarchyComponentIcons);
            ApplyFeatureEdit(ref edits, _hierarchySeparators, snapshot,
                GrowLensFeature.HierarchySeparators);
            ApplyFeatureEdit(ref edits, _hierarchyIndentGuides, snapshot,
                GrowLensFeature.HierarchyIndentGuides);
            ApplyFeatureEdit(ref edits, _hierarchyActiveToggle, snapshot,
                GrowLensFeature.HierarchyActiveToggle);
            ApplyFeatureEdit(ref edits, _hierarchyContextMenus, snapshot,
                GrowLensFeature.HierarchyContextMenus);
            ApplyFeatureEdit(ref edits, _toolbar, snapshot, GrowLensFeature.Toolbar);

            if (_hierarchyGuideDashed.boolValue != snapshot.HierarchyGuideDashed) {
                edits.GuideDashed = _hierarchyGuideDashed.boolValue;
            }

            // A reset wins over a color edit in the same frame.
            if (_resetGuideColor) {
                edits.ResetGuideColor = true;
            } else if (_hierarchyGuideColor.colorValue != GrowLensSettings.HierarchyGuideColor) {
                edits.GuideColor = _hierarchyGuideColor.colorValue;
            }

            return edits;
        }

        private static void ApplyFeatureEdit(ref GrowLensPageEdits edits,
            SerializedProperty property, GrowLensSettings.Data snapshot, GrowLensFeature feature) {
            var value = property.boolValue;
            if (value == GrowLensSettings.ReadFeature(snapshot, feature)) {
                return;
            }

            switch (feature) {
                case GrowLensFeature.AssetHeader:
                    edits.AssetHeader = value;
                    break;
                case GrowLensFeature.ProjectContextMenus:
                    edits.ProjectContextMenus = value;
                    break;
                case GrowLensFeature.HierarchyComponentIcons:
                    edits.HierarchyComponentIcons = value;
                    break;
                case GrowLensFeature.HierarchySeparators:
                    edits.HierarchySeparators = value;
                    break;
                case GrowLensFeature.HierarchyIndentGuides:
                    edits.HierarchyIndentGuides = value;
                    break;
                case GrowLensFeature.HierarchyActiveToggle:
                    edits.HierarchyActiveToggle = value;
                    break;
                case GrowLensFeature.HierarchyContextMenus:
                    edits.HierarchyContextMenus = value;
                    break;
                case GrowLensFeature.Toolbar:
                    edits.Toolbar = value;
                    break;
            }
        }

        private static void DrawGroup(SerializedProperty property, string key, bool mixed) {
            var previousMixed = EditorGUI.showMixedValue;
            try {
                EditorGUI.showMixedValue = mixed;
                EditorGUILayout.PropertyField(property, Content(key, key + ".tooltip"));
            } finally {
                // Reset before drawing the children; a mixed state must not leak into them.
                EditorGUI.showMixedValue = previousMixed;
            }
        }

        private static void DrawFeature(SerializedProperty property, string key) {
            EditorGUILayout.PropertyField(property, Content(key, key + ".tooltip"));
        }

        private void DrawGuideSettings() {
            EditorGUILayout.PropertyField(_hierarchyGuideDashed, Content(
                "grow.lens.preferences.guideDashed",
                "grow.lens.preferences.guideDashed.tooltip"));

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.PropertyField(_hierarchyGuideColor, Content(
                    "grow.lens.preferences.guideColor",
                    "grow.lens.preferences.guideColor.tooltip"));
                using (new EditorGUI.DisabledScope(
                    !GrowLensSettings.HierarchyGuideColorCustomized)) {
                    if (GUILayout.Button(
                            new GUIContent(
                                GrowLensLocalization.Get("grow.lens.preferences.guideColorReset"),
                                GrowLensLocalization.Get(
                                    "grow.lens.preferences.guideColorReset.tooltip")),
                            GUILayout.Width(56f))) {
                        // Collected and applied with the rest of the frame's edits.
                        _resetGuideColor = true;
                    }
                }
            }
        }

        private static GUIContent Content(string labelKey, string tooltipKey) {
            return new GUIContent(
                GrowLensLocalization.Get(labelKey),
                GrowLensLocalization.Get(tooltipKey));
        }

        private void EnsureProxy() {
            if (_proxy != null) {
                return;
            }

            _proxy = GrowLensSettingsProxy.CreateTransient();
            _serialized = new SerializedObject(_proxy);
            _enabled = _serialized.FindProperty("enabled");
            _inspectorGroup = _serialized.FindProperty("inspectorGroup");
            _projectGroup = _serialized.FindProperty("projectGroup");
            _hierarchyGroup = _serialized.FindProperty("hierarchyGroup");
            _toolbarGroup = _serialized.FindProperty("toolbarGroup");
            _hierarchyComponentIcons = _serialized.FindProperty("hierarchyComponentIcons");
            _hierarchySeparators = _serialized.FindProperty("hierarchySeparators");
            _hierarchyIndentGuides = _serialized.FindProperty("hierarchyIndentGuides");
            _hierarchyGuideDashed = _serialized.FindProperty("hierarchyGuideDashed");
            _hierarchyGuideColor = _serialized.FindProperty("hierarchyGuideColor");
            _hierarchyActiveToggle = _serialized.FindProperty("hierarchyActiveToggle");
            _hierarchyContextMenus = _serialized.FindProperty("hierarchyContextMenus");
            _projectContextMenus = _serialized.FindProperty("projectContextMenus");
            _assetHeader = _serialized.FindProperty("assetHeader");
            _toolbar = _serialized.FindProperty("toolbar");
        }

        private void SyncProxy(GrowLensSettings.Data snapshot) {
            _proxy.Enabled = snapshot.Enabled;
            _proxy.InspectorGroup = snapshot.AssetHeader;
            _proxy.ProjectGroup = snapshot.ProjectContextMenus;
            _proxy.HierarchyGroup = GrowLensSettings.AllHierarchy(snapshot);
            _proxy.ToolbarGroup = snapshot.Toolbar;
            _proxy.HierarchyComponentIcons = snapshot.HierarchyComponentIcons;
            _proxy.HierarchySeparators = snapshot.HierarchySeparators;
            _proxy.HierarchyIndentGuides = snapshot.HierarchyIndentGuides;
            _proxy.HierarchyGuideDashed = snapshot.HierarchyGuideDashed;
            _proxy.HierarchyGuideColor = GrowLensSettings.HierarchyGuideColor;
            _proxy.HierarchyActiveToggle = snapshot.HierarchyActiveToggle;
            _proxy.HierarchyContextMenus = snapshot.HierarchyContextMenus;
            _proxy.ProjectContextMenus = snapshot.ProjectContextMenus;
            _proxy.AssetHeader = snapshot.AssetHeader;
            _proxy.Toolbar = snapshot.Toolbar;
            _serialized.Update();
        }

        private void DisposeProxy() {
            if (_proxy != null) {
                UnityEngine.Object.DestroyImmediate(_proxy);
                _proxy = null;
            }

            _serialized = null;
        }
    }
}
