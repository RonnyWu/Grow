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

            var allInspector = GrowLensSettings.GetFeature(GrowLensFeature.AssetHeader);
            var allProject = GrowLensSettings.GetFeature(GrowLensFeature.ProjectContextMenus);
            var allHierarchy = GrowLensSettings.AllHierarchyFeatures;
            var mixedHierarchy = GrowLensSettings.AnyHierarchyFeature && !allHierarchy;
            var allToolbar = GrowLensSettings.GetFeature(GrowLensFeature.Toolbar);
            SyncProxy(allInspector, allProject, allHierarchy, allToolbar);

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

            if (!_serialized.ApplyModifiedPropertiesWithoutUndo()) {
                return;
            }

            if (_enabled.boolValue != GrowLensSettings.Enabled) {
                GrowLensSettings.Enabled = _enabled.boolValue;
            }

            // A group row is a batch switch over its children. Apply each changed group, then
            // apply the children of groups that did not change, so clicking one group never
            // discards an edit made to a child of another group in the same frame.
            var inspectorChanged = _inspectorGroup.boolValue != allInspector;
            var projectChanged = _projectGroup.boolValue != allProject;
            var hierarchyChanged = _hierarchyGroup.boolValue != allHierarchy;
            var toolbarChanged = _toolbarGroup.boolValue != allToolbar;

            if (inspectorChanged) {
                GrowLensSettings.SetFeature(GrowLensFeature.AssetHeader, _inspectorGroup.boolValue);
            }

            if (projectChanged) {
                GrowLensSettings.SetFeature(
                    GrowLensFeature.ProjectContextMenus, _projectGroup.boolValue);
            }

            if (hierarchyChanged) {
                GrowLensSettings.SetHierarchyFeatures(_hierarchyGroup.boolValue);
            }

            if (toolbarChanged) {
                GrowLensSettings.SetFeature(GrowLensFeature.Toolbar, _toolbarGroup.boolValue);
            }

            if (!hierarchyChanged) {
                ApplyFeature(_hierarchyComponentIcons, GrowLensFeature.HierarchyComponentIcons);
                ApplyFeature(_hierarchySeparators, GrowLensFeature.HierarchySeparators);
                ApplyFeature(_hierarchyIndentGuides, GrowLensFeature.HierarchyIndentGuides);
                ApplyFeature(_hierarchyActiveToggle, GrowLensFeature.HierarchyActiveToggle);
                ApplyFeature(_hierarchyContextMenus, GrowLensFeature.HierarchyContextMenus);
            }

            if (!projectChanged) {
                ApplyFeature(_projectContextMenus, GrowLensFeature.ProjectContextMenus);
            }

            if (!inspectorChanged) {
                ApplyFeature(_assetHeader, GrowLensFeature.AssetHeader);
            }

            if (!toolbarChanged) {
                ApplyFeature(_toolbar, GrowLensFeature.Toolbar);
            }

            ApplyGuideSettings();
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
                if (GUILayout.Button(
                        new GUIContent(
                            GrowLensLocalization.Get("grow.lens.preferences.guideColorReset"),
                            GrowLensLocalization.Get(
                                "grow.lens.preferences.guideColorReset.tooltip")),
                        GUILayout.Width(56f))) {
                    GrowLensSettings.ResetHierarchyGuideColor();
                    _hierarchyGuideColor.colorValue = GrowLensSettings.HierarchyGuideColor;
                }
            }
        }

        private static void ApplyFeature(SerializedProperty property, GrowLensFeature feature) {
            if (property.boolValue != GrowLensSettings.GetFeature(feature)) {
                GrowLensSettings.SetFeature(feature, property.boolValue);
            }
        }

        private void ApplyGuideSettings() {
            if (_hierarchyGuideDashed.boolValue != GrowLensSettings.HierarchyGuideDashed) {
                GrowLensSettings.HierarchyGuideDashed = _hierarchyGuideDashed.boolValue;
            }

            if (_hierarchyGuideColor.colorValue != GrowLensSettings.HierarchyGuideColor) {
                GrowLensSettings.SetHierarchyGuideColor(_hierarchyGuideColor.colorValue);
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

        private void SyncProxy(bool allInspector, bool allProject, bool allHierarchy,
            bool allToolbar) {
            _proxy.Enabled = GrowLensSettings.Enabled;
            _proxy.InspectorGroup = allInspector;
            _proxy.ProjectGroup = allProject;
            _proxy.HierarchyGroup = allHierarchy;
            _proxy.ToolbarGroup = allToolbar;
            _proxy.HierarchyComponentIcons =
                GrowLensSettings.GetFeature(GrowLensFeature.HierarchyComponentIcons);
            _proxy.HierarchySeparators =
                GrowLensSettings.GetFeature(GrowLensFeature.HierarchySeparators);
            _proxy.HierarchyIndentGuides =
                GrowLensSettings.GetFeature(GrowLensFeature.HierarchyIndentGuides);
            _proxy.HierarchyGuideDashed = GrowLensSettings.HierarchyGuideDashed;
            _proxy.HierarchyGuideColor = GrowLensSettings.HierarchyGuideColor;
            _proxy.HierarchyActiveToggle =
                GrowLensSettings.GetFeature(GrowLensFeature.HierarchyActiveToggle);
            _proxy.HierarchyContextMenus =
                GrowLensSettings.GetFeature(GrowLensFeature.HierarchyContextMenus);
            _proxy.ProjectContextMenus =
                GrowLensSettings.GetFeature(GrowLensFeature.ProjectContextMenus);
            _proxy.AssetHeader = GrowLensSettings.GetFeature(GrowLensFeature.AssetHeader);
            _proxy.Toolbar = GrowLensSettings.GetFeature(GrowLensFeature.Toolbar);
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
