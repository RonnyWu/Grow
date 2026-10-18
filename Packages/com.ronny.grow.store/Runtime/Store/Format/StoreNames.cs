// SPDX-License-Identifier: MIT

using System;

namespace Grow.Store.Format {
    /// <summary>Validation for store names that become file-name prefixes.</summary>
    internal static class StoreNames {
        private static readonly string[] Reserved = {
            "manifests",
            "manifests.bak",
            "manifests.tmp",
            "quarantine"
        };

        private static readonly string[] DeviceNames = {
            "con", "prn", "aux", "nul",
            "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
        };

        public static string Normalize(string storeName) {
            if (storeName == null) {
                throw new ArgumentNullException(nameof(storeName));
            }

            var name = storeName.ToLowerInvariant();
            if (name.Length == 0 || name.Length > 64) {
                throw new ArgumentException("A store name must be 1-64 characters.", nameof(storeName));
            }

            if (name[0] == '.' || name[name.Length - 1] == '.' ||
                !IsAlphaNumeric(name[0]) || !IsAlphaNumeric(name[name.Length - 1])) {
                throw new ArgumentException(
                    "A store name must start and end with a letter or digit.",
                    nameof(storeName));
            }

            for (var i = 0; i < name.Length; i++) {
                var character = name[i];
                if (!IsAllowed(character)) {
                    throw new ArgumentException(
                        "A store name may contain only a-z, 0-9, '.', '_' and '-'.",
                        nameof(storeName));
                }
            }

            if (name.Contains("..", StringComparison.Ordinal)) {
                throw new ArgumentException("A store name cannot contain '..'.", nameof(storeName));
            }

            if (name.EndsWith(".blobs", StringComparison.Ordinal)) {
                throw new ArgumentException(
                    "A store name cannot end with '.blobs'.", nameof(storeName));
            }

            for (var i = 0; i < Reserved.Length; i++) {
                if (string.Equals(name, Reserved[i], StringComparison.Ordinal)) {
                    throw new ArgumentException(
                        $"'{name}' is reserved by Grow Store.", nameof(storeName));
                }
            }

            for (var i = 0; i < DeviceNames.Length; i++) {
                if (string.Equals(name, DeviceNames[i], StringComparison.Ordinal)) {
                    throw new ArgumentException(
                        $"'{name}' is a reserved Windows device name.", nameof(storeName));
                }
            }

            return name;
        }

        private static bool IsAllowed(char character) {
            return (character >= 'a' && character <= 'z')
                   || (character >= '0' && character <= '9')
                   || character == '.' || character == '_' || character == '-';
        }

        private static bool IsAlphaNumeric(char character) {
            return (character >= 'a' && character <= 'z')
                   || (character >= '0' && character <= '9');
        }
    }
}
