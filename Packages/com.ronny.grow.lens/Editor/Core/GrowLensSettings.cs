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

    /// <summary>State of the settings file behind the in-memory copy.</summary>
    internal enum GrowLensSettingsStatus {
        /// <summary>The file is readable and current.</summary>
        Ok,

        /// <summary>No file exists yet; defaults are in memory and the first change creates it.</summary>
        Missing,

        /// <summary>An older schema was loaded and normalized in memory; the next change rewrites it.</summary>
        Migrated,

        /// <summary>The file cannot be parsed; defaults are shown and the file is never overwritten.</summary>
        Corrupted,

        /// <summary>The file was written by a newer version and is treated as read-only.</summary>
        NewerSchema,

        /// <summary>The file is read-only on disk; the page cannot save.</summary>
        ReadOnly,

        /// <summary>The last write failed; the page reports it and keeps the previous values.</summary>
        WriteFailed
    }

    /// <summary>
    /// Per-project Grow Lens settings. The file lives under <c>UserSettings/Grow/Lens.json</c>,
    /// outside the <c>Assets</c> folder and outside the package. This repository ignores
    /// <c>UserSettings/</c>; Unity itself does not ignore it.
    /// </summary>
    /// <remarks>
    /// The in-memory copy is replaced only after a successful write, so a failed save leaves the
    /// running editor on the last committed values instead of a state the file never recorded.
    /// </remarks>
    internal static class GrowLensSettings {
        private const int CurrentSchema = 5;

        private const float DefaultGuideAlpha = 0.32f;

        [Serializable]
        internal sealed class Data {
            public int SchemaVersion;
            public bool Enabled;
            public bool HierarchyComponentIcons = true;
            public bool HierarchyComponentInspector;
            public bool HierarchySeparators = true;
            public bool HierarchySeparatorMenu = true;
            public bool HierarchyIndentGuides = true;
            public bool HierarchyGuideDashed = true;
            public bool HierarchyGuideCustomColor;
            public Color HierarchyGuideColor = new(0.5f, 0.5f, 0.5f, DefaultGuideAlpha);
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

                if (SchemaVersion < 5) {
                    HierarchyComponentInspector = false;
                    HierarchySeparatorMenu = true;
                    SchemaVersion = 5;
                }
            }

            /// <summary>Copies every persisted field, so a candidate can be edited before saving.</summary>
            internal Data Clone() {
                return new Data {
                    SchemaVersion = SchemaVersion,
                    Enabled = Enabled,
                    HierarchyComponentIcons = HierarchyComponentIcons,
                    HierarchyComponentInspector = HierarchyComponentInspector,
                    HierarchySeparators = HierarchySeparators,
                    HierarchySeparatorMenu = HierarchySeparatorMenu,
                    HierarchyIndentGuides = HierarchyIndentGuides,
                    HierarchyGuideDashed = HierarchyGuideDashed,
                    HierarchyGuideCustomColor = HierarchyGuideCustomColor,
                    HierarchyGuideColor = HierarchyGuideColor,
                    HierarchyActiveToggle = HierarchyActiveToggle,
                    HierarchyContextMenus = HierarchyContextMenus,
                    ProjectContextMenus = ProjectContextMenus,
                    AssetHeader = AssetHeader,
                    Toolbar = Toolbar,
                    ContextMenus = ContextMenus
                };
            }

            /// <summary>Copies every persisted field from another copy.</summary>
            internal void CopyFrom(Data source) {
                SchemaVersion = source.SchemaVersion;
                Enabled = source.Enabled;
                HierarchyComponentIcons = source.HierarchyComponentIcons;
                HierarchyComponentInspector = source.HierarchyComponentInspector;
                HierarchySeparators = source.HierarchySeparators;
                HierarchySeparatorMenu = source.HierarchySeparatorMenu;
                HierarchyIndentGuides = source.HierarchyIndentGuides;
                HierarchyGuideDashed = source.HierarchyGuideDashed;
                HierarchyGuideCustomColor = source.HierarchyGuideCustomColor;
                HierarchyGuideColor = source.HierarchyGuideColor;
                HierarchyActiveToggle = source.HierarchyActiveToggle;
                HierarchyContextMenus = source.HierarchyContextMenus;
                ProjectContextMenus = source.ProjectContextMenus;
                AssetHeader = source.AssetHeader;
                Toolbar = source.Toolbar;
                ContextMenus = source.ContextMenus;
            }

            /// <summary>True when both copies hold the same persisted values.</summary>
            internal bool Matches(Data other) {
                return other != null &&
                    SchemaVersion == other.SchemaVersion &&
                    Enabled == other.Enabled &&
                    HierarchyComponentIcons == other.HierarchyComponentIcons &&
                    HierarchyComponentInspector == other.HierarchyComponentInspector &&
                    HierarchySeparators == other.HierarchySeparators &&
                    HierarchySeparatorMenu == other.HierarchySeparatorMenu &&
                    HierarchyIndentGuides == other.HierarchyIndentGuides &&
                    HierarchyGuideDashed == other.HierarchyGuideDashed &&
                    HierarchyGuideCustomColor == other.HierarchyGuideCustomColor &&
                    HierarchyGuideColor == other.HierarchyGuideColor &&
                    HierarchyActiveToggle == other.HierarchyActiveToggle &&
                    HierarchyContextMenus == other.HierarchyContextMenus &&
                    ProjectContextMenus == other.ProjectContextMenus &&
                    AssetHeader == other.AssetHeader &&
                    Toolbar == other.Toolbar;
            }

            /// <summary>
            /// True when the change affects the host subscriptions. Guide style and color only
            /// change drawing, so they must not trigger a host update.
            /// </summary>
            internal bool AffectsHost(Data other) {
                return other == null ||
                    Enabled != other.Enabled ||
                    HierarchyComponentIcons != other.HierarchyComponentIcons ||
                    HierarchySeparators != other.HierarchySeparators ||
                    HierarchyIndentGuides != other.HierarchyIndentGuides ||
                    HierarchyActiveToggle != other.HierarchyActiveToggle ||
                    HierarchyContextMenus != other.HierarchyContextMenus ||
                    ProjectContextMenus != other.ProjectContextMenus ||
                    AssetHeader != other.AssetHeader ||
                    Toolbar != other.Toolbar;
            }
        }

        private static Data _data;
        private static GrowLensSettingsStatus _status = GrowLensSettingsStatus.Missing;
        private static string _pathOverride;
        private static int _saveCount;
        private static bool _sessionDisabled;

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

        /// <summary>State of the file behind the current values.</summary>
        internal static GrowLensSettingsStatus Status => Load().Status;

        /// <summary>
        /// Whether the page may write. A corrupted, newer or read-only file is read-only; a
        /// failed write stays writable so the user can retry after fixing the cause.
        /// </summary>
        internal static bool CanSave {
            get {
                var status = Load().Status;
                return status != GrowLensSettingsStatus.Corrupted &&
                    status != GrowLensSettingsStatus.NewerSchema &&
                    status != GrowLensSettingsStatus.ReadOnly;
            }
        }

        /// <summary>Copy of the current values; the page compares its proxy against this.</summary>
        internal static Data Snapshot => Load().Values.Clone();

        /// <summary>Whether Grow Lens is enabled. Defaults to false.</summary>
        internal static bool Enabled {
            get => Load().Values.Enabled && !_sessionDisabled;
            set {
                if (value) {
                    _sessionDisabled = false;
                    GrowLensHost.ClearSessionDisable();
                }

                if (TryUpdate(data => data.Enabled = value) && value) {
                    // Re-enabling after a session-level disable may not change the file, so the
                    // commit path does not necessarily apply the host on its own.
                    GrowLensHost.Apply();
                }
            }
        }

        /// <summary>Whether a feature is both configured and allowed by the master switch.</summary>
        internal static bool IsFeatureEnabled(GrowLensFeature feature) {
            return Enabled && GetFeature(feature);
        }

        /// <summary>The saved value of a feature, independent of the master switch.</summary>
        internal static bool GetFeature(GrowLensFeature feature) {
            return ReadFeature(Load().Values, feature);
        }

        /// <summary>Stores one feature switch and reapplies the host subscriptions.</summary>
        internal static void SetFeature(GrowLensFeature feature, bool value) {
            TryUpdate(data => WriteFeature(data, feature, value));
        }

        /// <summary>Whether clicking a component icon opens its focused Inspector.</summary>
        internal static bool HierarchyComponentInspector =>
            Load().Values.HierarchyComponentInspector;

        /// <summary>Whether the Create Separator menu entries are available.</summary>
        internal static bool HierarchySeparatorMenuEnabled =>
            IsFeatureEnabled(GrowLensFeature.HierarchySeparators) &&
            Load().Values.HierarchySeparatorMenu;

        /// <summary>True when every regular-item Hierarchy feature is enabled.</summary>
        internal static bool AllHierarchyFeatures => AllHierarchy(Load().Values);

        /// <summary>True when any regular-item Hierarchy feature is enabled.</summary>
        internal static bool AnyHierarchyFeature => AnyHierarchy(Load().Values);

        /// <summary>True when a Hierarchy row decoration is enabled.</summary>
        internal static bool AnyHierarchyDecoration {
            get {
                var data = Load().Values;
                return data.HierarchyComponentIcons ||
                    data.HierarchySeparators ||
                    data.HierarchyIndentGuides ||
                    data.HierarchyActiveToggle;
            }
        }

        /// <summary>
        /// Applies one value to the four regular-item Hierarchy switches. This is one candidate,
        /// one save and at most one host update, so a group click cannot leave a partial state.
        /// </summary>
        internal static void SetHierarchyFeatures(bool value) {
            TryUpdate(data => SetHierarchyFeatures(data, value));
        }

        /// <summary>Writes all regular-item Hierarchy switches into a data copy before it is committed.</summary>
        internal static void SetHierarchyFeatures(Data data, bool value) {
            data.HierarchyComponentIcons = value;
            data.HierarchyIndentGuides = value;
            data.HierarchyActiveToggle = value;
            data.HierarchyContextMenus = value;
        }

        /// <summary>Whether indent guides use dashed instead of solid lines.</summary>
        internal static bool HierarchyGuideDashed {
            get => Load().Values.HierarchyGuideDashed;
            set => TryUpdate(data => data.HierarchyGuideDashed = value);
        }

        /// <summary>Whether the guide color was customized instead of derived from the theme.</summary>
        internal static bool HierarchyGuideColorCustomized {
            get => Load().Values.HierarchyGuideCustomColor;
        }

        /// <summary>Effective indent-guide color: custom when set, otherwise the theme text color.</summary>
        internal static Color HierarchyGuideColor {
            get {
                var data = Load().Values;
                if (!data.HierarchyGuideCustomColor) {
                    return ThemeGuideColor();
                }

                var color = data.HierarchyGuideColor;
                color.a = Mathf.Clamp01(color.a);
                return color;
            }
        }

        /// <summary>Stores a custom indent-guide color.</summary>
        internal static void SetHierarchyGuideColor(Color color) {
            TryUpdate(data => {
                data.HierarchyGuideCustomColor = true;
                data.HierarchyGuideColor = color;
            });
        }

        /// <summary>Restores the theme-derived indent-guide color.</summary>
        internal static void ResetHierarchyGuideColor() {
            TryUpdate(data => data.HierarchyGuideCustomColor = false);
        }

        /// <summary>Applies the native settings page's data copy with one atomic commit.</summary>
        internal static void Apply(Data values) {
            if (values.Enabled && !Load().Values.Enabled) {
                _sessionDisabled = false;
                GrowLensHost.ClearSessionDisable();
            }

            TryUpdate(data => data.CopyFrom(values));
        }

        /// <summary>Restores every persisted value to its default; the page confirms first.</summary>
        internal static void ResetToDefaults() {
            TryUpdate(data => data.CopyFrom(new Data { SchemaVersion = CurrentSchema }));
        }

        /// <summary>Turns the in-memory switch off without writing the saved preference.</summary>
        internal static void DisableForSession() {
            _sessionDisabled = true;
        }

        /// <summary>Clears the session-only switch; the saved preference is untouched.</summary>
        internal static void ResetSessionDisable() {
            _sessionDisabled = false;
        }

        private static bool TryUpdate(Action<Data> mutate) {
            var current = Load();
            if (!CanSave) {
                return false;
            }

            var candidate = current.Values.Clone();
            mutate(candidate);
            return Commit(current.Values, candidate);
        }

        private static bool Commit(Data current, Data candidate) {
            if (candidate.Matches(current)) {
                return true;
            }

            if (!Save(candidate)) {
                _status = GrowLensSettingsStatus.WriteFailed;
                GrowLensHost.RequestRepaint();
                return false;
            }

            _data = candidate;
            _status = GrowLensSettingsStatus.Ok;
            if (candidate.AffectsHost(current)) {
                GrowLensHost.Apply();
            }

            GrowLensHost.RequestRepaint();
            return true;
        }

        private static bool Save(Data data) {
            data.SchemaVersion = CurrentSchema;
            _saveCount++;
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

                return true;
            } catch (Exception exception) {
                Debug.LogError($"[Grow Lens] Could not write {FilePath}: {exception.Message}");
                try {
                    if (File.Exists(tempPath)) {
                        File.Delete(tempPath);
                    }
                } catch {
                    // Best effort only.
                }

                return false;
            }
        }

        /// <summary>Reloads the file; the cached values and the status are replaced together.</summary>
        private static (Data Values, GrowLensSettingsStatus Status) Load() {
            if (_data == null) {
                Reload();
            }

            return (_data, _status);
        }

        private static void Reload() {
            var values = new Data { SchemaVersion = CurrentSchema };
            _status = GrowLensSettingsStatus.Missing;
            try {
                if (File.Exists(FilePath)) {
                    var loaded = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
                    if (loaded == null) {
                        _status = GrowLensSettingsStatus.Corrupted;
                    } else if (loaded.SchemaVersion > CurrentSchema) {
                        // Keep the values visible, but never write this file back.
                        values = loaded;
                        _status = GrowLensSettingsStatus.NewerSchema;
                    } else {
                        var migrated = loaded.SchemaVersion < CurrentSchema;
                        loaded.Normalize();
                        values = loaded;
                        _status = migrated
                            ? GrowLensSettingsStatus.Migrated
                            : GrowLensSettingsStatus.Ok;
                    }

                    if (_status != GrowLensSettingsStatus.NewerSchema &&
                        IsReadOnlyFile(FilePath)) {
                        _status = GrowLensSettingsStatus.ReadOnly;
                    }
                }
            } catch (Exception exception) {
                Debug.LogWarning($"[Grow Lens] Could not read {FilePath}: {exception.Message}");
                _status = GrowLensSettingsStatus.Corrupted;
            }

            _data = values;
        }

        private static bool IsReadOnlyFile(string path) {
            try {
                return (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
            } catch (Exception) {
                return false;
            }
        }

        /// <summary>
        /// The current editor theme's text color. Headless test runs have no GUISkin, so this
        /// falls back to a neutral gray instead of throwing.
        /// </summary>
        internal static Color EditorTextColor {
            get {
                try {
                    var label = EditorStyles.label;
                    if (label != null) {
                        return label.normal.textColor;
                    }
                } catch (Exception) {
                    // EditorStyles can be unavailable during headless tests.
                }

                return new Color(0.65f, 0.65f, 0.65f);
            }
        }

        /// <summary>
        /// The current editor theme's text color with the guide alpha. Reading the active style
        /// keeps the guides legible in both the light and dark editor skins.
        /// </summary>
        private static Color ThemeGuideColor() {
            var color = EditorTextColor;
            color.a = DefaultGuideAlpha;
            return color;
        }

        /// <summary>Reads one feature from a data copy; used by the page to diff its proxy.</summary>
        internal static bool ReadFeature(Data data, GrowLensFeature feature) {
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
        private static void WriteFeature(Data data, GrowLensFeature feature, bool value) {
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
            }
        }

        /// <summary>True when a data copy has every regular-item Hierarchy feature enabled.</summary>
        internal static bool AllHierarchy(Data data) {
            return data.HierarchyComponentIcons &&
                data.HierarchyIndentGuides &&
                data.HierarchyActiveToggle &&
                data.HierarchyContextMenus;
        }

        /// <summary>True when a data copy has any regular-item Hierarchy feature enabled.</summary>
        internal static bool AnyHierarchy(Data data) {
            return data.HierarchyComponentIcons ||
                data.HierarchyIndentGuides ||
                data.HierarchyActiveToggle ||
                data.HierarchyContextMenus;
        }

        /// <summary>Clears the in-memory cache; tests only.</summary>
        internal static void ReloadForTests() {
            _data = null;
            _saveCount = 0;
            _sessionDisabled = false;
        }

        /// <summary>Drops the cached values so the next read reloads the file and its status.</summary>
        internal static void ReloadFromDisk() {
            _data = null;
        }

        /// <summary>Redirects the settings file to a temporary path; tests only.</summary>
        internal static void SetPathForTests(string path) {
            _pathOverride = path;
            _data = null;
            _saveCount = 0;
        }

        /// <summary>Number of file writes since the last test reset; tests only.</summary>
        internal static int SaveCountForTests => _saveCount;

        /// <summary>Restores the project settings path; tests only.</summary>
        internal static void ResetPathForTests() {
            _pathOverride = null;
            _data = null;
        }
    }
}
