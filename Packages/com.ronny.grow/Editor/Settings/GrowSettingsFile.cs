// SPDX-License-Identifier: MIT

using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditorInternal;
using UnityEngine;

namespace Grow.Editor.Settings {
    /// <summary>
    /// JSON persistence for Grow editor settings.
    /// </summary>
    /// <remarks>
    /// The file format is a small JSON object with a fixed format marker and a schema version.
    /// Writes use a temporary file plus <see cref="File.Replace(string, string, string)"/> so a
    /// crash cannot leave a half-written main file, and the previous version is kept as
    /// <c>.bak</c>. Loads never write defaults; a missing file is an in-memory default only.
    /// </remarks>
    internal static class GrowSettingsFile {
        public const string FormatMarker = "com.ronny.grow.settings";

        private const string BackupSuffix = ".bak";

        private const string CorruptPrefix = ".corrupt-";

        [Serializable]
        private sealed class Header {
            [SerializeField]
            private string format;

            [SerializeField]
            private int schemaVersion;

            public string Format => format;

            public int SchemaVersion => schemaVersion;
        }

        /// <summary>Loads a settings file without writing a default file.</summary>
        public static GrowSettingsLoadResult Load<T>(string absolutePath, Func<T> createDefault,
            out T settings) where T : ScriptableObject, IGrowSettings {
            if (createDefault == null) {
                throw new ArgumentNullException(nameof(createDefault));
            }

            settings = createDefault();

            if (!File.Exists(absolutePath)) {
                return new GrowSettingsLoadResult(GrowSettingsLoadStatus.Created);
            }

            if (!TryRead(absolutePath, createDefault, out settings, out var version, out var error)) {
                var quarantine = Quarantine(absolutePath);
                var backupPath = absolutePath + BackupSuffix;

                if (TryRead(backupPath, createDefault, out var backup, out _, out _)) {
                    TryCopy(backupPath, absolutePath);
                    settings = backup;
                    var repaired = settings.Validate();
                    settings.SchemaVersion = settings.CurrentSchema;
                    return new GrowSettingsLoadResult(
                        repaired ? GrowSettingsLoadStatus.Repaired :
                        GrowSettingsLoadStatus.RestoredFromBackup,
                        error, quarantine);
                }

                settings = createDefault();
                return new GrowSettingsLoadResult(
                    GrowSettingsLoadStatus.Corrupted, error, quarantine);
            }

            if (version > settings.CurrentSchema) {
                settings = createDefault();
                return new GrowSettingsLoadResult(
                    GrowSettingsLoadStatus.NewerSchema,
                    $"Settings schema {version} is newer than supported schema " +
                    $"{settings.CurrentSchema}.");
            }

            try {
                if (version < settings.CurrentSchema) {
                    settings.Migrate(version);
                }
            } catch (Exception exception) {
                settings = createDefault();
                return new GrowSettingsLoadResult(
                    GrowSettingsLoadStatus.MigrationFailed, exception.Message);
            }

            var wasRepaired = settings.Validate();
            settings.SchemaVersion = settings.CurrentSchema;

            var status = version < settings.CurrentSchema
                ? GrowSettingsLoadStatus.Migrated
                : wasRepaired
                    ? GrowSettingsLoadStatus.Repaired
                    : GrowSettingsLoadStatus.Loaded;

            return new GrowSettingsLoadResult(status);
        }

        /// <summary>Writes a settings file atomically, keeping the previous version as <c>.bak</c>.</summary>
        public static void Save<T>(string absolutePath, T settings)
            where T : ScriptableObject, IGrowSettings {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.SchemaVersion = settings.CurrentSchema;

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            var json = JsonUtility.ToJson(settings, true);
            var temporaryPath = absolutePath + ".tmp";

            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

            if (!File.Exists(absolutePath)) {
                File.Move(temporaryPath, absolutePath);
                return;
            }

            try {
                File.Replace(
                    temporaryPath, absolutePath, absolutePath + BackupSuffix, true);
            } catch (PlatformNotSupportedException) {
                CopyReplace(temporaryPath, absolutePath);
            } catch (IOException) {
                CopyReplace(temporaryPath, absolutePath);
            }
        }

        /// <summary>Resolves a project-relative path and rejects paths outside the project.</summary>
        public static string ToProjectPath(string relativePath) {
            return CombineUnder(GetProjectRoot(), relativePath);
        }

        /// <summary>Resolves a path under Unity's per-user Preferences folder.</summary>
        public static string ToPreferencesPath(string relativePath) {
            return CombineUnder(InternalEditorUtility.unityPreferencesFolder, relativePath);
        }

        private static string GetProjectRoot() {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string CombineUnder(string root, string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("A settings path is required.", nameof(relativePath));
            }

            if (Path.IsPathRooted(relativePath)) {
                throw new ArgumentException(
                    "A settings path must be relative.", nameof(relativePath));
            }

            var normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison)) {
                throw new ArgumentException(
                    "A settings path must stay under its root.", nameof(relativePath));
            }

            return fullPath;
        }

        private static bool TryRead<T>(string path, Func<T> createDefault, out T settings,
            out int version, out string error) where T : ScriptableObject, IGrowSettings {
            settings = null;
            version = 0;
            error = null;

            try {
                if (!File.Exists(path)) {
                    error = $"Settings file '{path}' does not exist.";
                    return false;
                }

                var json = File.ReadAllText(path);
                var header = JsonUtility.FromJson<Header>(json);

                if (header == null || header.Format != FormatMarker ||
                    header.SchemaVersion <= 0) {
                    error = $"Settings file '{path}' has an invalid header.";
                    return false;
                }

                settings = createDefault();
                JsonUtility.FromJsonOverwrite(json, settings);
                version = header.SchemaVersion;
                return true;
            } catch (Exception exception) {
                error = exception.Message;
                return false;
            }
        }

        private static string Quarantine(string path) {
            var directory = Path.GetDirectoryName(path);
            var name = Path.GetFileName(path);
            var stamp = DateTime.UtcNow.ToString(
                "yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
            var quarantinePath = Path.Combine(directory ?? string.Empty,
                name + CorruptPrefix + stamp);

            try {
                File.Move(path, quarantinePath);
                return quarantinePath;
            } catch (IOException) {
                return null;
            } catch (UnauthorizedAccessException) {
                return null;
            }
        }

        private static void CopyReplace(string temporaryPath, string targetPath) {
            var backupPath = targetPath + BackupSuffix;

            if (File.Exists(targetPath)) {
                File.Copy(targetPath, backupPath, true);
            }

            File.Copy(temporaryPath, targetPath, true);
            File.Delete(temporaryPath);
        }

        private static void TryCopy(string sourcePath, string targetPath) {
            try {
                File.Copy(sourcePath, targetPath, true);
            } catch (IOException) {
                // Keep the loaded backup in memory; the main file will be rewritten on save.
            } catch (UnauthorizedAccessException) {
                // Same as above.
            }
        }
    }
}
