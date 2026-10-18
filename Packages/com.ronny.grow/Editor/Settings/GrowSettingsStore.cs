// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Grow.Editor.Settings {
    /// <summary>
    /// Owns one settings instance, its load result, dirty state and file stamp.
    /// </summary>
    internal sealed class GrowSettingsStore<T> : IGrowSettingsStore
        where T : ScriptableObject, IGrowSettings {
        private readonly string _absolutePath;

        private readonly Func<T> _createDefault;

        private T _instance;

        private GrowSettingsLoadResult _loadResult;

        private GrowSettingsStamp _stamp;

        private bool _dirty;

        private bool _conflict;

        private bool _loaded;

        public GrowSettingsStore(string absolutePath, Func<T> createDefault) {
            _absolutePath = absolutePath;
            _createDefault = createDefault;
        }

        public T Instance {
            get {
                EnsureLoaded();
                return _instance;
            }
        }

        public GrowSettingsLoadResult LoadResult {
            get {
                EnsureLoaded();
                return _loadResult;
            }
        }

        public bool HasUnsavedChanges => _dirty;

        public bool CanSave {
            get {
                EnsureLoaded();
                return !_conflict && _loadResult.CanSave && _instance != null;
            }
        }

        public void MarkDirty() {
            EnsureLoaded();
            if (_instance == null) {
                return;
            }

            _dirty = true;
            GrowSettingsSaveScheduler.MarkDirty(this);
        }

        public bool TrySave(out string error) {
            EnsureLoaded();

            if (_instance == null) {
                error = "The settings instance is unavailable.";
                return false;
            }

            if (!_dirty) {
                error = null;
                return true;
            }

            if (_conflict) {
                error = "The settings file changed on disk; reload it before saving.";
                return false;
            }

            if (!_loadResult.CanSave) {
                error = _loadResult.Message ?? "The settings file is read-only.";
                return false;
            }

            var currentStamp = GrowSettingsStamp.Read(_absolutePath);
            if (currentStamp != _stamp) {
                EnterConflict();
                error = "The settings file changed on disk; reload it before saving.";
                return false;
            }

            try {
                GrowSettingsFile.Save(_absolutePath, _instance);
                _dirty = false;
                _stamp = GrowSettingsStamp.Read(_absolutePath);
                _loadResult = new GrowSettingsLoadResult(GrowSettingsLoadStatus.Loaded);
                error = null;
                return true;
            } catch (Exception exception) {
                error = exception.Message;
                return false;
            }
        }

        public void Flush() {
            if (!_dirty) {
                return;
            }

            if (!TrySave(out var error)) {
                Debug.LogError($"Grow settings save failed: {error}");
            }
        }

        /// <summary>
        /// Reloads when the file changed externally. Unsaved edits enter a conflict state
        /// instead of being overwritten.
        /// </summary>
        public bool ReloadIfChangedOnDisk() {
            EnsureLoaded();

            var currentStamp = GrowSettingsStamp.Read(_absolutePath);
            if (currentStamp == _stamp) {
                return false;
            }

            if (_dirty) {
                EnterConflict();
                return false;
            }

            return ReloadFromDisk();
        }

        /// <summary>Discards in-memory edits and reloads the file from disk.</summary>
        public bool ReloadFromDisk() {
            EnsureLoaded();

            var oldInstance = _instance;
            _loadResult = GrowSettingsFile.Load(_absolutePath, _createDefault, out var settings);

            if (oldInstance != null && oldInstance != settings) {
                UnityEngine.Object.DestroyImmediate(oldInstance);
            }

            _instance = settings;
            _dirty = false;
            _conflict = false;
            _loaded = true;
            _stamp = GrowSettingsStamp.Read(_absolutePath);
            return true;
        }

        /// <summary>Explicit recovery for a corrupt file; refuses to overwrite newer/migrated data.</summary>
        public void ResetToDefaults() {
            EnsureLoaded();

            if (_instance == null ||
                _loadResult.Status == GrowSettingsLoadStatus.NewerSchema ||
                _loadResult.Status == GrowSettingsLoadStatus.MigrationFailed) {
                Debug.LogWarning(
                    "Refusing to reset Grow settings written by a newer version or with failed migration.");
                return;
            }

            _instance.ResetToDefaults();
            _loadResult = new GrowSettingsLoadResult(GrowSettingsLoadStatus.Loaded);
            _conflict = false;
            _dirty = true;
            GrowSettingsSaveScheduler.MarkDirty(this);
        }

        private void EnterConflict() {
            if (_conflict) {
                return;
            }

            _conflict = true;
            var conflictPath = GrowSettingsFile.CreateConflictCopy(_absolutePath);
            _loadResult = new GrowSettingsLoadResult(
                GrowSettingsLoadStatus.Conflict,
                "The settings file changed on disk while there were unsaved edits.",
                conflictPath);
        }

        private void EnsureLoaded() {
            if (_loaded) {
                return;
            }

            _loaded = true;
            _loadResult = GrowSettingsFile.Load(_absolutePath, _createDefault, out var settings);
            _instance = settings;
            _stamp = GrowSettingsStamp.Read(_absolutePath);

            if (_loadResult.CanSave &&
                (_loadResult.Status == GrowSettingsLoadStatus.Migrated ||
                 _loadResult.Status == GrowSettingsLoadStatus.Repaired)) {
                _dirty = true;
                GrowSettingsSaveScheduler.MarkDirty(this);
            }
        }
    }

    /// <summary>Debounces settings writes and flushes them on editor lifecycle events.</summary>
    [InitializeOnLoad]
    internal static class GrowSettingsSaveScheduler {
        private const double DebounceSeconds = 0.5;

        private const double RetrySeconds = 2.0;

        private static readonly HashSet<IGrowSettingsStore> Dirty = new();

        private static double _nextFlush;

        static GrowSettingsSaveScheduler() {
            AssemblyReloadEvents.beforeAssemblyReload += FlushAll;
            EditorApplication.quitting += FlushAll;
            EditorApplication.playModeStateChanged += _ => FlushAll();
            EditorApplication.update += OnUpdate;
        }

        public static void MarkDirty(IGrowSettingsStore store) {
            if (store == null) {
                return;
            }

            Dirty.Add(store);
            _nextFlush = EditorApplication.timeSinceStartup + DebounceSeconds;
        }

        public static void FlushAll() {
            if (Dirty.Count == 0) {
                return;
            }

            var stores = new IGrowSettingsStore[Dirty.Count];
            Dirty.CopyTo(stores);
            Dirty.Clear();

            var failed = false;
            foreach (var store in stores) {
                if (store.TrySave(out var error)) {
                    continue;
                }

                failed = true;
                Dirty.Add(store);
                Debug.LogError($"Grow settings save failed: {error}");
            }

            _nextFlush = EditorApplication.timeSinceStartup +
                (failed ? RetrySeconds : DebounceSeconds);
        }

        private static void OnUpdate() {
            if (Dirty.Count == 0 || EditorApplication.timeSinceStartup < _nextFlush) {
                return;
            }

            FlushAll();
        }
    }
}
