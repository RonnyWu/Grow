// SPDX-License-Identifier: MIT

using System;
using System.IO;
using UnityEngine;

namespace Grow.Editor.Settings {
    /// <summary>Outcome of loading a Grow settings file.</summary>
    internal enum GrowSettingsLoadStatus {
        /// <summary>The file did not exist; the in-memory defaults were used and nothing was written.</summary>
        Created,

        /// <summary>The file was loaded successfully.</summary>
        Loaded,

        /// <summary>An older schema was migrated in memory.</summary>
        Migrated,

        /// <summary>Invalid values were repaired in memory.</summary>
        Repaired,

        /// <summary>The main file was corrupt and was restored from its backup.</summary>
        RestoredFromBackup,

        /// <summary>The file was corrupt and no usable backup existed.</summary>
        Corrupted,

        /// <summary>The file was written by a newer Grow version; it is read-only.</summary>
        NewerSchema,

        /// <summary>Migration threw; the file is read-only.</summary>
        MigrationFailed,

        /// <summary>The file changed on disk while there were unsaved edits; saving is blocked.</summary>
        Conflict
    }

    /// <summary>Load result shared by the settings file and store layers.</summary>
    internal readonly struct GrowSettingsLoadResult {
        public GrowSettingsLoadResult(GrowSettingsLoadStatus status, string message = null,
            string quarantinePath = null) {
            Status = status;
            Message = message;
            QuarantinePath = quarantinePath;
        }

        public GrowSettingsLoadStatus Status { get; }

        public string Message { get; }

        public string QuarantinePath { get; }

        /// <summary>True when the loaded settings may be written back.</summary>
        public bool CanSave =>
            Status != GrowSettingsLoadStatus.Corrupted &&
            Status != GrowSettingsLoadStatus.NewerSchema &&
            Status != GrowSettingsLoadStatus.MigrationFailed &&
            Status != GrowSettingsLoadStatus.Conflict;
    }

    /// <summary>
    /// Contract for a JSON-backed settings payload. Implementations own their defaults, schema
    /// version and migration rules; the IO layer owns file format and integrity.
    /// </summary>
    internal interface IGrowSettings {
        int SchemaVersion { get; set; }

        int CurrentSchema { get; }

        void ResetToDefaults();

        void Migrate(int fromVersion);

        /// <summary>Repairs invalid values; returns true when anything changed.</summary>
        bool Validate();
    }

    /// <summary>Minimal interface used by the save scheduler.</summary>
    internal interface IGrowSettingsStore {
        bool TrySave(out string error);
    }

    /// <summary>Cheap file identity used to detect external edits.</summary>
    internal readonly struct GrowSettingsStamp : IEquatable<GrowSettingsStamp> {
        public GrowSettingsStamp(bool exists, long length, DateTime lastWriteUtc) {
            Exists = exists;
            Length = length;
            LastWriteUtc = lastWriteUtc;
        }

        public bool Exists { get; }

        public long Length { get; }

        public DateTime LastWriteUtc { get; }

        public static GrowSettingsStamp Read(string path) {
            if (!File.Exists(path)) {
                return default;
            }

            var info = new FileInfo(path);
            return new GrowSettingsStamp(true, info.Length, info.LastWriteTimeUtc);
        }

        public bool Equals(GrowSettingsStamp other) {
            return Exists == other.Exists &&
                Length == other.Length &&
                LastWriteUtc == other.LastWriteUtc;
        }

        public override bool Equals(object obj) {
            return obj is GrowSettingsStamp other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                var hash = Exists.GetHashCode();
                hash = (hash * 397) ^ Length.GetHashCode();
                hash = (hash * 397) ^ LastWriteUtc.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(GrowSettingsStamp left, GrowSettingsStamp right) {
            return left.Equals(right);
        }

        public static bool operator !=(GrowSettingsStamp left, GrowSettingsStamp right) {
            return !left.Equals(right);
        }
    }
}
