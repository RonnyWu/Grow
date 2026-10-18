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
    /// <c>.bak</c>. Loads do not write default files; a missing file is an in-memory default only.
    /// </remarks>
    internal static class GrowSettingsFile {
        public const string FormatMarker = "com.ronny.grow.settings";

        private const string BackupSuffix = ".bak";

        private const string CorruptPrefix = ".corrupt-";

        private const string ConflictPrefix = ".conflict-";

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

            settings = null;

            if (!TryCreateDefault(createDefault, out settings)) {
                return new GrowSettingsLoadResult(
                    GrowSettingsLoadStatus.MigrationFailed,
                    "Could not create the default settings instance.");
            }

            if (!File.Exists(absolutePath)) {
                return new GrowSettingsLoadResult(GrowSettingsLoadStatus.Created);
            }

            if (!TryRead(absolutePath, settings, out var version, out var error)) {
                ResetQuietly(settings);
                var quarantine = Quarantine(absolutePath);
                if (quarantine == null) {
                    return new GrowSettingsLoadResult(
                        GrowSettingsLoadStatus.Corrupted,
                        $"{error} The file could not be quarantined; it was left untouched.");
                }

                var backupPath = absolutePath + BackupSuffix;
                if (TryRead(backupPath, settings, out var backupVersion, out _)) {
                    var applied = Apply(settings, backupVersion);
                    if (applied.Status == GrowSettingsLoadStatus.NewerSchema ||
                        applied.Status == GrowSettingsLoadStatus.MigrationFailed) {
                        ResetQuietly(settings);
                        return new GrowSettingsLoadResult(
                            applied.Status, applied.Message, quarantine);
                    }

                    if (applied.Status == GrowSettingsLoadStatus.Migrated ||
                        applied.Status == GrowSettingsLoadStatus.Repaired) {
                        try {
                            Save(absolutePath, settings);
                        } catch (IOException) {
                            // Keep the recovered settings in memory; the store will retry saving.
                        } catch (UnauthorizedAccessException) {
                            // Same as above.
                        }
                    } else {
                        TryCopy(backupPath, absolutePath);
                    }

                    return new GrowSettingsLoadResult(
                        applied.Status == GrowSettingsLoadStatus.Loaded
                            ? GrowSettingsLoadStatus.RestoredFromBackup
                            : applied.Status,
                        applied.Message, quarantine);
                }

                ResetQuietly(settings);
                return new GrowSettingsLoadResult(
                    GrowSettingsLoadStatus.Corrupted, error, quarantine);
            }

            var result = Apply(settings, version);
            if (result.Status == GrowSettingsLoadStatus.NewerSchema ||
                result.Status == GrowSettingsLoadStatus.MigrationFailed) {
                ResetQuietly(settings);
            }

            return result;
        }

        /// <summary>Creates a copy of the current file for conflict resolution.</summary>
        public static string CreateConflictCopy(string absolutePath) {
            if (!File.Exists(absolutePath)) {
                return null;
            }

            var conflictPath = absolutePath + ConflictPrefix + Timestamp();
            try {
                File.Copy(absolutePath, conflictPath, false);
                return conflictPath;
            } catch (IOException) {
                return null;
            } catch (UnauthorizedAccessException) {
                return null;
            }
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

            var temporaryPath = absolutePath + ".tmp";

            try {
                var json = JsonUtility.ToJson(settings, true);
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
                } catch (UnauthorizedAccessException) {
                    CopyReplace(temporaryPath, absolutePath);
                }
            } finally {
                if (File.Exists(temporaryPath)) {
                    try {
                        File.Delete(temporaryPath);
                    } catch (IOException) {
                        // A later save will overwrite the stale temporary file.
                    }
                }
            }
        }

        /// <summary>Resolves a project-relative path and rejects paths outside the project.</summary>
        public static string ToProjectPath(string relativePath) {
            return CombineUnder(GetProjectRoot(), relativePath);
        }

        /// <summary>Resolves a path under Unity's per-user Preferences folder.</summary>
        public static string ToPreferencesPath(string relativePath) {
            var root = InternalEditorUtility.unityPreferencesFolder;
            if (string.IsNullOrEmpty(root)) {
                root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Unity", "Editor-5.x", "Preferences");
            }

            return CombineUnder(root, relativePath);
        }

        private static GrowSettingsLoadResult Apply<T>(T settings, int version)
            where T : ScriptableObject, IGrowSettings {
            if (version > settings.CurrentSchema) {
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
                return new GrowSettingsLoadResult(
                    GrowSettingsLoadStatus.MigrationFailed, exception.Message);
            }

            bool repaired;
            try {
                repaired = settings.Validate();
            } catch (Exception exception) {
                return new GrowSettingsLoadResult(
                    GrowSettingsLoadStatus.MigrationFailed, exception.Message);
            }

            settings.SchemaVersion = settings.CurrentSchema;

            var status = version < settings.CurrentSchema
                ? GrowSettingsLoadStatus.Migrated
                : repaired
                    ? GrowSettingsLoadStatus.Repaired
                    : GrowSettingsLoadStatus.Loaded;

            return new GrowSettingsLoadResult(status);
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

        private static bool TryCreateDefault<T>(Func<T> createDefault, out T settings)
            where T : ScriptableObject, IGrowSettings {
            try {
                settings = createDefault();
                return settings != null;
            } catch (Exception) {
                settings = null;
                return false;
            }
        }

        private static bool TryRead<T>(string path, T settings, out int version, out string error)
            where T : ScriptableObject, IGrowSettings {
            version = 0;
            error = null;

            try {
                if (!File.Exists(path)) {
                    error = $"Settings file '{path}' does not exist.";
                    return false;
                }

                var json = File.ReadAllText(path);
                var header = JsonUtility.FromJson<Header>(json);

                if (header == null || header.Format != FormatMarker || header.SchemaVersion <= 0) {
                    error = $"Settings file '{path}' has an invalid header.";
                    return false;
                }

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
            var quarantinePath = Path.Combine(directory ?? string.Empty,
                name + CorruptPrefix + Timestamp());

            try {
                File.Move(path, quarantinePath);
                return quarantinePath;
            } catch (IOException) {
                return null;
            } catch (UnauthorizedAccessException) {
                return null;
            }
        }

        private static string Timestamp() {
            return DateTime.UtcNow.ToString(
                "yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
        }

        private static void CopyReplace(string temporaryPath, string targetPath) {
            var backupPath = targetPath + BackupSuffix;

            if (File.Exists(targetPath)) {
                File.Copy(targetPath, backupPath, true);
            }

            File.Copy(temporaryPath, targetPath, true);
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

        private static void ResetQuietly<T>(T settings)
            where T : ScriptableObject, IGrowSettings {
            try {
                settings.ResetToDefaults();
            } catch (Exception) {
                // A broken default is reported by the load status; never mask the original error.
            }
        }
    }
}
