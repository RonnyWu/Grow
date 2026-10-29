// SPDX-License-Identifier: MIT

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Core {
    /// <summary>One independently switchable Grow Lens feature.</summary>
    internal enum GrowLensFeature {
        HierarchyComponentIcons,
        HierarchySeparators,
        HierarchyIndentGuides,
        HierarchyActiveToggle,
        HierarchyContextMenus,
        ProjectContextMenus,
        AssetHeader,
        Toolbar
    }

    /// <summary>
    /// Per-project Grow Lens settings. The file lives under <c>UserSettings/Grow/Lens.json</c>,
    /// outside the <c>Assets</c> folder and outside the package. This repository ignores
    /// <c>UserSettings/</c>; Unity itself does not ignore it.
    /// </summary>
    internal static class GrowLensSettings {
        private const int CurrentSchema = 4;

        [Serializable]
        internal sealed class Data {
            public int SchemaVersion;
            public bool Enabled;
            public bool HierarchyComponentIcons = true;
            public bool HierarchySeparators = true;
            public bool HierarchyIndentGuides = true;
            public bool HierarchyGuideDashed = true;
            public bool HierarchyGuideCustomColor;
            public Color HierarchyGuideColor = new(0.5f, 0.5f, 0.5f, 0.32f);
            public bool HierarchyActiveToggle = true;
            public bool HierarchyContextMenus = true;
            public bool ProjectContextMenus = true;
            public bool AssetHeader = true;
            public bool Toolbar = true;

            /// <summary>Schema 1/2 used one context-menu switch for both windows.</summary>
            public bool ContextMenus = true;

            /// <summary>
            /// Version 1 stored only <see cref="Enabled"/>; version 2 added the flat feature
            /// switches. Missing Hierarchy features meant "on", and the old shared context-menu
            /// switch is copied to the new Project and Hierarchy switches.
            /// </summary>
            internal void Normalize() {
                if (SchemaVersion < 2) {
                    HierarchyComponentIcons = true;
                    HierarchySeparators = true;
                    HierarchyIndentGuides = true;
                    AssetHeader = true;
                    ContextMenus = true;
                    Toolbar = true;
                    SchemaVersion = 2;
                }

                if (SchemaVersion < 3) {
                    ProjectContextMenus = ContextMenus;
                    HierarchyContextMenus = ContextMenus;
                    HierarchyActiveToggle = true;
                    SchemaVersion = 3;
                }

                if (SchemaVersion < 4) {
                    HierarchyGuideDashed = true;
                    HierarchyGuideCustomColor = false;
                    SchemaVersion = 4;
                }
            }
        }

        private static Data _data;
        private static string _pathOverride;

        /// <summary>Absolute path of the settings file.</summary>
        internal static string FilePath {
            get {
                if (!string.IsNullOrEmpty(_pathOverride)) {
                    return _pathOverride;
                }

                var root = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(string.IsNullOrEmpty(root) ? "." : root, "UserSettings", "Grow",
                    "Lens.json");
            }
        }

        /// <summary>Whether Grow Lens is enabled. Defaults to false.</summary>
        internal static bool Enabled {
            get => Load().Enabled;
            set {
                var data = Load();
                if (data.Enabled == value) {
                    return;
                }

                data.Enabled = value;
                Save(data);
                if (value) {
                    GrowLensHost.ClearSessionDisable();
                }

                GrowLensHost.Apply();
                GrowLensHost.RequestRepaint();
            }
        }

        /// <summary>Whether a feature is both configured and allowed by the master switch.</summary>
        internal static bool IsFeatureEnabled(GrowLensFeature feature) {
            return Enabled && GetFeature(feature);
        }

        /// <summary>The saved value of a feature, independent of the master switch.</summary>
        internal static bool GetFeature(GrowLensFeature feature) {
            var data = Load();
            return feature switch {
                GrowLensFeature.HierarchyComponentIcons => data.HierarchyComponentIcons,
                GrowLensFeature.HierarchySeparators => data.HierarchySeparators,
                GrowLensFeature.HierarchyIndentGuides => data.HierarchyIndentGuides,
                GrowLensFeature.HierarchyActiveToggle => data.HierarchyActiveToggle,
                GrowLensFeature.HierarchyContextMenus => data.HierarchyContextMenus,
                GrowLensFeature.ProjectContextMenus => data.ProjectContextMenus,
                GrowLensFeature.AssetHeader => data.AssetHeader,
                GrowLensFeature.Toolbar => data.Toolbar,
                _ => false
            };
        }

        /// <summary>Stores one feature switch and reapplies the host subscriptions.</summary>
        internal static void SetFeature(GrowLensFeature feature, bool value) {
            var data = Load();
            if (GetFeature(feature) == value) {
                return;
            }

            switch (feature) {
                case GrowLensFeature.HierarchyComponentIcons:
                    data.HierarchyComponentIcons = value;
                    break;
                case GrowLensFeature.HierarchySeparators:
                    data.HierarchySeparators = value;
                    break;
                case GrowLensFeature.HierarchyIndentGuides:
                    data.HierarchyIndentGuides = value;
                    break;
                case GrowLensFeature.HierarchyActiveToggle:
                    data.HierarchyActiveToggle = value;
                    break;
                case GrowLensFeature.HierarchyContextMenus:
                    data.HierarchyContextMenus = value;
                    break;
                case GrowLensFeature.ProjectContextMenus:
                    data.ProjectContextMenus = value;
                    break;
                case GrowLensFeature.AssetHeader:
                    data.AssetHeader = value;
                    break;
                case GrowLensFeature.Toolbar:
                    data.Toolbar = value;
                    break;
                default:
                    return;
            }

            Save(data);
            GrowLensHost.Apply();
            GrowLensHost.RequestRepaint();
        }

        /// <summary>True when every Hierarchy feature is enabled.</summary>
        internal static bool AllHierarchyFeatures {
            get => GetFeature(GrowLensFeature.HierarchyComponentIcons) &&
                GetFeature(GrowLensFeature.HierarchySeparators) &&
                GetFeature(GrowLensFeature.HierarchyIndentGuides) &&
                GetFeature(GrowLensFeature.HierarchyActiveToggle) &&
                GetFeature(GrowLensFeature.HierarchyContextMenus);
        }

        /// <summary>True when any Hierarchy feature is enabled.</summary>
        internal static bool AnyHierarchyFeature {
            get => GetFeature(GrowLensFeature.HierarchyComponentIcons) ||
                GetFeature(GrowLensFeature.HierarchySeparators) ||
                GetFeature(GrowLensFeature.HierarchyIndentGuides) ||
                GetFeature(GrowLensFeature.HierarchyActiveToggle) ||
                GetFeature(GrowLensFeature.HierarchyContextMenus);
        }

        /// <summary>True when a Hierarchy row decoration is enabled.</summary>
        internal static bool AnyHierarchyDecoration {
            get => GetFeature(GrowLensFeature.HierarchyComponentIcons) ||
                GetFeature(GrowLensFeature.HierarchySeparators) ||
                GetFeature(GrowLensFeature.HierarchyIndentGuides) ||
                GetFeature(GrowLensFeature.HierarchyActiveToggle);
        }

        /// <summary>
        /// Applies one value to the five Hierarchy switches. This is intentionally one save and
        /// one host update, so a group click cannot leave a partially applied state.
        /// </summary>
        internal static void SetHierarchyFeatures(bool value) {
            var data = Load();
            if (data.HierarchyComponentIcons == value &&
                data.HierarchySeparators == value &&
                data.HierarchyIndentGuides == value &&
                data.HierarchyActiveToggle == value &&
                data.HierarchyContextMenus == value) {
                return;
            }

            data.HierarchyComponentIcons = value;
            data.HierarchySeparators = value;
            data.HierarchyIndentGuides = value;
            data.HierarchyActiveToggle = value;
            data.HierarchyContextMenus = value;
            Save(data);
            GrowLensHost.Apply();
            GrowLensHost.RequestRepaint();
        }

        /// <summary>Whether indent guides use dashed instead of solid lines.</summary>
        internal static bool HierarchyGuideDashed {
            get => Load().HierarchyGuideDashed;
            set => SetGuide(data => data.HierarchyGuideDashed = value);
        }

        /// <summary>Effective indent-guide color: custom when set, otherwise the theme color.</summary>
        internal static Color HierarchyGuideColor {
            get {
                var data = Load();
                if (data.HierarchyGuideCustomColor) {
                    return data.HierarchyGuideColor;
                }

                var color = new Color(0.65f, 0.65f, 0.65f, 0.32f);
                try {
                    var label = EditorStyles.label;
                    if (label != null) {
                        color = label.normal.textColor;
                    }
                } catch (Exception) {
                    // EditorStyles can be unavailable during headless tests.
                }

                color.a = 0.32f;
                return color;
            }
        }

        /// <summary>Stores a custom indent-guide color.</summary>
        internal static void SetHierarchyGuideColor(Color color) {
            var data = Load();
            if (data.HierarchyGuideCustomColor && data.HierarchyGuideColor == color) {
                return;
            }

            data.HierarchyGuideCustomColor = true;
            data.HierarchyGuideColor = color;
            Save(data);
            GrowLensHost.RequestRepaint();
        }

        /// <summary>Restores the theme-derived indent-guide color.</summary>
        internal static void ResetHierarchyGuideColor() {
            var data = Load();
            if (!data.HierarchyGuideCustomColor) {
                return;
            }

            data.HierarchyGuideCustomColor = false;
            Save(data);
            GrowLensHost.RequestRepaint();
        }

        private static void SetGuide(Action<Data> apply) {
            var data = Load();
            apply(data);
            Save(data);
            GrowLensHost.RequestRepaint();
        }

        /// <summary>Turns the in-memory switch off without writing the saved preference.</summary>
        internal static void DisableForSession() {
            Load().Enabled = false;
        }

        private static Data Load() {
            if (_data != null) {
                return _data;
            }

            _data = new Data { SchemaVersion = CurrentSchema };
            try {
                if (!File.Exists(FilePath)) {
                    return _data;
                }

                var json = File.ReadAllText(FilePath);
                var loaded = JsonUtility.FromJson<Data>(json);
                if (loaded != null) {
                    loaded.Normalize();
                    _data = loaded;
                }
            } catch (Exception exception) {
                Debug.LogWarning($"[Grow Lens] Could not read {FilePath}: {exception.Message}");
            }

            return _data;
        }

        private static void Save(Data data) {
            data.SchemaVersion = CurrentSchema;
            var tempPath = FilePath + ".tmp";
            try {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory)) {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath)) {
                    File.Replace(tempPath, FilePath, null);
                } else {
                    File.Move(tempPath, FilePath);
                }
            } catch (Exception exception) {
                Debug.LogError($"[Grow Lens] Could not write {FilePath}: {exception.Message}");
                try {
                    if (File.Exists(tempPath)) {
                        File.Delete(tempPath);
                    }
                } catch {
                    // Best effort only.
                }
            }
        }

        /// <summary>Clears the in-memory cache; tests only.</summary>
        internal static void ReloadForTests() {
            _data = null;
        }

        /// <summary>Redirects the settings file to a temporary path; tests only.</summary>
        internal static void SetPathForTests(string path) {
            _pathOverride = path;
            _data = null;
        }

        /// <summary>Restores the project settings path; tests only.</summary>
        internal static void ResetPathForTests() {
            _pathOverride = null;
            _data = null;
        }
    }
}
