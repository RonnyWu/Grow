// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Grow.Store.Format {
    /// <summary>Parses and writes one versioned bucket file.</summary>
    internal static class StoreBucketFile {
        private const string HeaderPrefix = "// grow-store-bucket 1 ";

        internal sealed class BucketData {
            public string Store;
            public int Bucket;
            public long Version;
            public int BucketCount;
            public long Bytes;
            public Dictionary<string, StoreEntry> Entries =
                new Dictionary<string, StoreEntry>(StringComparer.Ordinal);
        }

        public static bool TryRead(string path, out BucketData data, out string error) {
            data = null;
            error = null;
            try {
                var text = File.ReadAllText(path, Encoding.UTF8);
                var lineEnd = text.IndexOf('\n');
                if (lineEnd < 0) {
                    error = "missing header or body";
                    return false;
                }

                var header = text.Substring(0, lineEnd);
                var body = text.Substring(lineEnd + 1);
                if (!TryParseHeader(header, out var store, out var bucket, out var version,
                        out var bucketCount, out var bytes, out var crc, out error)) {
                    return false;
                }

                var bodyBytes = Encoding.UTF8.GetBytes(body);
                if (bytes != bodyBytes.Length) {
                    error = $"body length {bodyBytes.Length} does not match header {bytes}";
                    return false;
                }

                if (StoreHash.Crc32(bodyBytes, 0, bodyBytes.Length) != crc) {
                    error = "body CRC32 mismatch";
                    return false;
                }

                var entries = new Dictionary<string, StoreEntry>(StringComparer.Ordinal);
                var lineStart = 0;
                for (var i = 0; i <= body.Length; i++) {
                    if (i < body.Length && body[i] != '\n') {
                        continue;
                    }

                    if (i > lineStart) {
                        var line = body.Substring(lineStart, i - lineStart);
                        if (!TryParseLine(line, out var key, out var entry, out error)) {
                            return false;
                        }

                        if (entries.ContainsKey(key)) {
                            error = $"duplicate key '{key}'";
                            return false;
                        }

                        entries.Add(key, entry);
                    }

                    lineStart = i + 1;
                }

                data = new BucketData {
                    Store = store,
                    Bucket = bucket,
                    Version = version,
                    BucketCount = bucketCount,
                    Bytes = bytes,
                    Entries = entries
                };
                return true;
            } catch (Exception exception) {
                error = exception.Message;
                return false;
            }
        }

        public static long Write(string path, string store, int bucket, long version,
            int bucketCount, IReadOnlyDictionary<string, StoreEntry> entries, bool flush) {
            var body = new StringBuilder();
            var keys = new List<string>(entries.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (var i = 0; i < keys.Count; i++) {
                var key = keys[i];
                var entry = entries[key];
                if (entry.Data != null || entry.Text == null) {
                    throw new InvalidOperationException("A bucket entry must have resolved text.");
                }

                body.Append(StoreValues.Escape(key));
                body.Append('\t');
                body.Append(StoreValues.KindToken(entry.Kind));
                body.Append('\t');
                body.Append(StoreValues.Escape(entry.Text));
                body.Append('\n');
            }

            var bodyBytes = Encoding.UTF8.GetBytes(body.ToString());
            var header = string.Concat(
                HeaderPrefix,
                "hash=", StoreHash.HashId,
                " store=", store,
                " bucket=", bucket.ToString(CultureInfo.InvariantCulture),
                " version=", version.ToString(CultureInfo.InvariantCulture),
                " bucketCount=", bucketCount.ToString(CultureInfo.InvariantCulture),
                " bytes=", bodyBytes.Length.ToString(CultureInfo.InvariantCulture),
                " dataCrc32=", StoreHash.Hex(StoreHash.Crc32(bodyBytes, 0, bodyBytes.Length)));

            var bytes = new byte[Encoding.UTF8.GetByteCount(header) + 1 + bodyBytes.Length];
            var headerBytes = Encoding.UTF8.GetBytes(header + "\n");
            Array.Copy(headerBytes, 0, bytes, 0, headerBytes.Length);
            Array.Copy(bodyBytes, 0, bytes, headerBytes.Length, bodyBytes.Length);

            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (var stream = new FileStream(
                           temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    stream.Write(bytes, 0, bytes.Length);
                    if (flush) {
                        stream.Flush(true);
                    }
                }

                if (File.Exists(path)) {
                    File.Delete(path);
                }

                File.Move(temporary, path);
                return bodyBytes.Length;
            } catch {
                TryDelete(temporary);
                throw;
            }
        }

        private static bool TryParseHeader(string header, out string store, out int bucket,
            out long version, out int bucketCount, out long bytes, out uint crc, out string error) {
            store = null;
            bucket = 0;
            version = 0;
            bucketCount = 0;
            bytes = 0;
            crc = 0;
            error = null;
            if (!header.StartsWith(HeaderPrefix, StringComparison.Ordinal)) {
                error = "invalid bucket header";
                return false;
            }

            var tokens = header.Substring(HeaderPrefix.Length)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string hash = null;
            for (var i = 0; i < tokens.Length; i++) {
                var token = tokens[i];
                var separator = token.IndexOf('=');
                if (separator <= 0) {
                    continue;
                }

                var key = token.Substring(0, separator);
                var value = token.Substring(separator + 1);
                switch (key) {
                    case "hash":
                        hash = value;
                        break;
                    case "store":
                        store = value;
                        break;
                    case "bucket":
                        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                                out bucket) || bucket < 0) {
                            error = "invalid bucket number";
                            return false;
                        }

                        break;
                    case "version":
                        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                                out version) || version <= 0) {
                            error = "invalid version";
                            return false;
                        }

                        break;
                    case "bucketCount":
                        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                                out bucketCount) || bucketCount <= 0 ||
                            (bucketCount & (bucketCount - 1)) != 0) {
                            error = "invalid bucketCount";
                            return false;
                        }

                        break;
                    case "bytes":
                        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                                out bytes) || bytes < 0) {
                            error = "invalid body length";
                            return false;
                        }

                        break;
                    case "dataCrc32":
                        if (!StoreHash.TryParseHex(value, out crc)) {
                            error = "invalid CRC32";
                            return false;
                        }

                        break;
                }
            }

            if (hash != StoreHash.HashId || string.IsNullOrEmpty(store) || bucket >= bucketCount) {
                error = "missing or invalid header fields";
                return false;
            }

            return true;
        }

        private static bool TryParseLine(string line, out string key, out StoreEntry entry,
            out string error) {
            key = null;
            entry = default;
            error = null;
            var first = line.IndexOf('\t');
            var second = first < 0 ? -1 : line.IndexOf('\t', first + 1);
            if (first <= 0 || second <= first) {
                error = "malformed record";
                return false;
            }

            key = StoreValues.Unescape(line.Substring(0, first));
            if (key.Length == 0) {
                error = "empty key";
                return false;
            }

            if (!StoreValues.TryParseKind(
                    line.Substring(first + 1, second - first - 1), out var kind)) {
                error = $"unknown value kind for '{key}'";
                return false;
            }

            var text = StoreValues.Unescape(line.Substring(second + 1));
            if (kind == StoreValueKind.Blob && !StoreValues.TryParsePointer(text, out _, out _, out _)) {
                error = $"invalid blob pointer for '{key}'";
                return false;
            }

            entry = new StoreEntry(kind, text, null);
            return true;
        }

        private static void TryDelete(string path) {
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            } catch {
                // Best effort; a later GC retries.
            }
        }
    }
}
