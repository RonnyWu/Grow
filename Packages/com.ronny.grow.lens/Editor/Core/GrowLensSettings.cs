// SPDX-License-Identifier: MIT

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Grow.Lens.Editor.Core {
    /// <summary>
    /// Per-project Grow Lens settings. The file lives under <c>UserSettings/Grow/Lens.json</c>,
    /// outside the <c>Assets</c> folder and outside the package. This repository ignores
    /// <c>UserSettings/</c>; Unity itself does not ignore it.
    /// </summary>
    internal static class GrowLensSettings {
        [Serializable]
        internal sealed class Data {
            public bool Enabled;
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

        /// <summary>Whether the optional editor enhancements are enabled. Defaults to false.</summary>
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
            }
        }

        /// <summary>Turns the in-memory switch off without writing the saved preference.</summary>
        internal static void DisableForSession() {
            Load().Enabled = false;
        }

        private static Data Load() {
            if (_data != null) {
                return _data;
            }

            _data = new Data();
            try {
                if (!File.Exists(FilePath)) {
                    return _data;
                }

                var json = File.ReadAllText(FilePath);
                var loaded = JsonUtility.FromJson<Data>(json);
                if (loaded != null) {
                    _data = loaded;
                }
            } catch (Exception exception) {
                Debug.LogWarning($"[Grow Lens] Could not read {FilePath}: {exception.Message}");
            }

            return _data;
        }

        private static void Save(Data data) {
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
