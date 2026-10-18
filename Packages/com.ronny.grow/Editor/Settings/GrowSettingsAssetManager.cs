// SPDX-License-Identifier: MIT

using System;
using System.IO;
using UnityEditorInternal;
using UnityEngine;

namespace Grow.Editor.Settings {
    /// <summary>
    /// Loads, saves and deletes <see cref="ScriptableObject"/> settings assets outside
    /// <c>Assets/</c>.
    /// </summary>
    /// <remarks>
    /// Settings live under <c>ProjectSettings/</c> (team-shared) or <c>UserSettings/</c>
    /// (local-only), so they are not imported by <see cref="AssetDatabase"/>. The helper uses
    /// <see cref="InternalEditorUtility"/> to serialize them as YAML text.
    /// </remarks>
    internal static class GrowSettingsAssetManager {
        /// <summary>
        /// Loads <typeparamref name="T"/> from <paramref name="assetPath"/>, or creates a default
        /// instance and writes it when the asset does not exist.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The asset exists but cannot be loaded as <typeparamref name="T"/>.
        /// </exception>
        public static T GetOrCreate<T>(string assetPath) where T : ScriptableObject {
            if (TryLoad(assetPath, out T existing)) {
                return existing;
            }

            if (Exists(assetPath)) {
                throw new InvalidOperationException(
                    $"The settings asset '{assetPath}' exists but could not be loaded as " +
                    $"'{typeof(T).Name}'.");
            }

            var instance = ScriptableObject.CreateInstance<T>();
            if (instance == null) {
                throw new InvalidOperationException(
                    $"Could not create a settings instance of '{typeof(T).Name}'.");
            }

            Save(instance, assetPath);
            return instance;
        }

        /// <summary>
        /// Loads <typeparamref name="T"/> from <paramref name="assetPath"/>.
        /// </summary>
        /// <returns><c>true</c> when the asset exists and contains <typeparamref name="T"/>.</returns>
        public static bool TryLoad<T>(string assetPath, out T settings) where T : ScriptableObject {
            ValidatePath(assetPath);
            settings = null;

            if (!Exists(assetPath)) {
                return false;
            }

            var loaded = InternalEditorUtility.LoadSerializedFileAndForget(assetPath);
            if (loaded == null || loaded.Length == 0 || loaded[0] is not T existing) {
                return false;
            }

            settings = existing;
            return true;
        }

        /// <summary>Writes <paramref name="settings"/> to <paramref name="assetPath"/> as YAML.</summary>
        public static void Save(ScriptableObject settings, string assetPath) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            ValidatePath(assetPath);

            var absolutePath = ToAbsolutePath(assetPath);
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            InternalEditorUtility.SaveToSerializedFileAndForget(
                new UnityEngine.Object[] { settings }, assetPath, true);
        }

        /// <summary>Deletes the asset and its <c>.meta</c> file when they exist.</summary>
        public static void DeleteAsset(string assetPath) {
            ValidatePath(assetPath);

            var absolutePath = ToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath)) {
                return;
            }

            File.Delete(absolutePath);

            var metaPath = absolutePath + ".meta";
            if (File.Exists(metaPath)) {
                File.Delete(metaPath);
            }
        }

        /// <summary>True when a file exists at <paramref name="assetPath"/>.</summary>
        public static bool Exists(string assetPath) {
            ValidatePath(assetPath);
            return File.Exists(ToAbsolutePath(assetPath));
        }

        private static void ValidatePath(string assetPath) {
            if (string.IsNullOrWhiteSpace(assetPath)) {
                throw new ArgumentException("An asset path is required.", nameof(assetPath));
            }
        }

        private static string ToAbsolutePath(string assetPath) {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }
    }
}
