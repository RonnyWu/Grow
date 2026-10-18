// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Grow.Store.Format;

namespace Grow.Store.Import {
    /// <summary>Reads the single-file layout when it is present.</summary>
    internal static class StoreSingleFile {
        public static bool TryRead(string path, out Dictionary<string, StoreEntry> entries,
            out string error) {
            entries = new Dictionary<string, StoreEntry>(StringComparer.Ordinal);
            error = null;
            try {
                using var reader = new StreamReader(path, Encoding.UTF8);
                string line;
                while ((line = reader.ReadLine()) != null) {
                    if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) {
                        continue;
                    }

                    if (!TryParseLine(line, out var key, out var entry, out error)) {
                        return false;
                    }

                    if (key.Length == 0) {
                        continue;
                    }

                    entries[key] = entry;
                }

                return true;
            } catch (Exception exception) {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryParseLine(string line, out string key, out StoreEntry entry,
            out string error) {
            key = null;
            entry = default;
            error = null;
            var first = line.IndexOf('\t');
            if (first > 0) {
                var second = line.IndexOf('\t', first + 1);
                if (second > first) {
                    var candidate = StoreValues.Unescape(line.Substring(0, first)).Trim();
                    var token = line.Substring(first + 1, second - first - 1).Trim();
                    var text = StoreValues.Unescape(line.Substring(second + 1));
                    if (!TryParseKind(token, out var kind)) {
                        error = $"unknown single-file kind '{token}'";
                        return false;
                    }

                    if (kind == StoreValueKind.Blob) {
                        try {
                            entry = new StoreEntry(kind, null, Convert.FromBase64String(text));
                        } catch (FormatException exception) {
                            error = exception.Message;
                            return false;
                        }
                    } else {
                        entry = new StoreEntry(kind, text, null);
                    }

                    key = candidate;
                    return true;
                }
            }

            var separator = line.IndexOf('=');
            if (separator <= 0) {
                error = "malformed single-file line";
                return false;
            }

            key = StoreValues.Unescape(line.Substring(0, separator)).Trim();
            entry = new StoreEntry(
                StoreValueKind.String, StoreValues.Unescape(line.Substring(separator + 1)), null);
            return true;
        }

        private static bool TryParseKind(string token, out StoreValueKind kind) {
            switch (token) {
                case "int":
                    kind = StoreValueKind.Int;
                    return true;
                case "long":
                    kind = StoreValueKind.Long;
                    return true;
                case "float":
                    kind = StoreValueKind.Float;
                    return true;
                case "double":
                    kind = StoreValueKind.Double;
                    return true;
                case "bool":
                    kind = StoreValueKind.Bool;
                    return true;
                case "string":
                    kind = StoreValueKind.String;
                    return true;
                case "bytes":
                case "blob":
                    kind = StoreValueKind.Blob;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }
    }
}
