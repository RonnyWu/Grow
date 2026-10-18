// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Grow.Store.Format {
    /// <summary>The scope-wide index of stores, buckets and current bucket versions.</summary>
    internal sealed class StoreManifest {
        private const string HeaderPrefix = "// grow-store-manifests 1 ";

        internal struct BucketRecord {
            public BucketRecord(long version, long bytes) {
                Version = version;
                Bytes = bytes;
            }

            public long Version;
            public long Bytes;
        }

        internal sealed class StoreRecord {
            public StoreRecord(int bucketCount) {
                BucketCount = bucketCount;
            }

            public int BucketCount;
            public readonly Dictionary<int, BucketRecord> Buckets =
                new Dictionary<int, BucketRecord>();
        }

        public long Generation { get; set; }

        public readonly Dictionary<string, StoreRecord> Stores =
            new Dictionary<string, StoreRecord>(StringComparer.Ordinal);

        public StoreRecord GetStore(string name) {
            return Stores.TryGetValue(name, out var record) ? record : null;
        }

        public StoreRecord EnsureStore(string name) {
            if (!Stores.TryGetValue(name, out var record)) {
                record = new StoreRecord(1);
                Stores.Add(name, record);
            }

            return record;
        }

        public static bool TryLoad(string path, out StoreManifest manifest, out string error) {
            manifest = null;
            error = null;
            try {
                if (!File.Exists(path)) {
                    error = "missing";
                    return false;
                }

                var text = File.ReadAllText(path, Encoding.UTF8);
                var lineEnd = text.IndexOf('\n');
                if (lineEnd < 0) {
                    error = "missing header";
                    return false;
                }

                var header = text.Substring(0, lineEnd);
                var body = text.Substring(lineEnd + 1);
                if (!TryParseHeader(header, out var generation, out var crc, out error)) {
                    return false;
                }

                var bodyBytes = Encoding.UTF8.GetBytes(body);
                if (StoreHash.Crc32(bodyBytes, 0, bodyBytes.Length) != crc) {
                    error = "body CRC32 mismatch";
                    return false;
                }

                var result = new StoreManifest { Generation = generation };
                StoreRecord current = null;
                var lineStart = 0;
                for (var i = 0; i <= body.Length; i++) {
                    if (i < body.Length && body[i] != '\n') {
                        continue;
                    }

                    if (i > lineStart) {
                        var line = body.Substring(lineStart, i - lineStart);
                        if (!TryParseBodyLine(result, ref current, line, out error)) {
                            return false;
                        }
                    }

                    lineStart = i + 1;
                }

                manifest = result;
                return true;
            } catch (Exception exception) {
                error = exception.Message;
                return false;
            }
        }

        public static StoreManifest LoadOrRebuild(string root, out bool usedBackup) {
            var manifestPath = Path.Combine(root, "manifests");
            if (TryLoad(manifestPath, out var manifest, out _)) {
                usedBackup = false;
                return manifest;
            }

            if (TryLoad(manifestPath + ".bak", out manifest, out _)) {
                usedBackup = true;
                return manifest;
            }

            usedBackup = false;
            return Rebuild(root);
        }

        public static StoreManifest Rebuild(string root) {
            var result = new StoreManifest();
            if (!Directory.Exists(root)) {
                return result;
            }

            var buckets = new Dictionary<string, Dictionary<int, BucketRecord>>(
                StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] files;
            try {
                files = Directory.GetFiles(root, "*.kv", SearchOption.TopDirectoryOnly);
            } catch {
                return result;
            }

            for (var i = 0; i < files.Length; i++) {
                if (!TryParseFileName(Path.GetFileName(files[i]), out var name, out _, out _)) {
                    continue;
                }

                if (!StoreBucketFile.TryRead(files[i], out var data, out _)) {
                    continue;
                }

                if (!string.Equals(name, data.Store, StringComparison.Ordinal)) {
                    continue;
                }

                if (!buckets.TryGetValue(name, out var perStore)) {
                    perStore = new Dictionary<int, BucketRecord>();
                    buckets.Add(name, perStore);
                }

                if (!perStore.TryGetValue(data.Bucket, out var existing) ||
                    data.Version > existing.Version) {
                    perStore[data.Bucket] = new BucketRecord(data.Version, data.Bytes);
                }

                if (!counts.TryGetValue(name, out var count) || data.BucketCount > count) {
                    counts[name] = data.BucketCount;
                }
            }

            foreach (var pair in buckets) {
                var maxBucket = 0;
                foreach (var bucket in pair.Value.Keys) {
                    if (bucket > maxBucket) {
                        maxBucket = bucket;
                    }
                }

                var count = counts.TryGetValue(pair.Key, out var headerCount) ? headerCount : 1;
                while (count <= maxBucket) {
                    count *= 2;
                }

                var record = new StoreRecord(count);
                foreach (var bucket in pair.Value) {
                    record.Buckets[bucket.Key] = bucket.Value;
                }

                result.Stores[pair.Key] = record;
            }

            return result;
        }

        public string ToText() {
            var body = new StringBuilder();
            var names = new List<string>(Stores.Keys);
            names.Sort(StringComparer.Ordinal);
            for (var i = 0; i < names.Count; i++) {
                var name = names[i];
                var record = Stores[name];
                body.Append("store ").Append(name)
                    .Append(" bucketCount=").Append(record.BucketCount.ToString(
                        CultureInfo.InvariantCulture)).Append('\n');
                var buckets = new List<int>(record.Buckets.Keys);
                buckets.Sort();
                for (var b = 0; b < buckets.Count; b++) {
                    var bucket = record.Buckets[buckets[b]];
                    body.Append("  bucket ").Append(buckets[b].ToString(CultureInfo.InvariantCulture))
                        .Append(" version=").Append(bucket.Version.ToString(
                            CultureInfo.InvariantCulture))
                        .Append(" bytes=").Append(bucket.Bytes.ToString(CultureInfo.InvariantCulture))
                        .Append('\n');
                }
            }

            var bodyBytes = Encoding.UTF8.GetBytes(body.ToString());
            var header = string.Concat(
                HeaderPrefix,
                "hash=", StoreHash.HashId,
                " generation=", Generation.ToString(CultureInfo.InvariantCulture),
                " bodyCrc32=", StoreHash.Hex(StoreHash.Crc32(bodyBytes, 0, bodyBytes.Length)));
            return header + "\n" + body;
        }

        public static bool TryParseFileName(string fileName, out string store, out int bucket,
            out long version) {
            store = null;
            bucket = 0;
            version = 0;
            if (!fileName.EndsWith(".kv", StringComparison.Ordinal)) {
                return false;
            }

            var withoutExtension = fileName.Substring(0, fileName.Length - 3);
            var last = withoutExtension.LastIndexOf('.');
            if (last <= 0 || last + 1 >= withoutExtension.Length) {
                return false;
            }

            var previous = withoutExtension.LastIndexOf('.', last - 1);
            if (previous <= 0 || previous + 1 >= last) {
                return false;
            }

            var versionText = withoutExtension.Substring(last + 1);
            var bucketText = withoutExtension.Substring(previous + 1, last - previous - 1);
            store = withoutExtension.Substring(0, previous);
            return IsCanonicalNumber(versionText)
                   && IsCanonicalNumber(bucketText)
                   && int.TryParse(bucketText, NumberStyles.None, CultureInfo.InvariantCulture,
                       out bucket)
                   && long.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture,
                       out version)
                   && bucket >= 0 && version > 0;
        }

        public string BucketFileName(string store, int bucket, long version) {
            return string.Concat(
                store, ".", bucket.ToString(CultureInfo.InvariantCulture), ".",
                version.ToString(CultureInfo.InvariantCulture), ".kv");
        }

        private static bool TryParseHeader(string header, out long generation, out uint crc,
            out string error) {
            generation = 0;
            crc = 0;
            error = null;
            if (!header.StartsWith(HeaderPrefix, StringComparison.Ordinal)) {
                error = "invalid manifest header";
                return false;
            }

            var tokens = header.Substring(HeaderPrefix.Length)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var hash = (string)null;
            var hasCrc = false;
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
                    case "generation":
                        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                                out generation) || generation < 0) {
                            error = "invalid generation";
                            return false;
                        }

                        break;
                    case "bodyCrc32":
                        if (!StoreHash.TryParseHex(value, out crc)) {
                            error = "invalid manifest CRC32";
                            return false;
                        }

                        hasCrc = true;
                        break;
                }
            }

            if (hash != StoreHash.HashId || !hasCrc) {
                error = "missing or invalid manifest fields";
                return false;
            }

            return true;
        }

        private static bool TryParseBodyLine(StoreManifest manifest, ref StoreRecord current,
            string line, out string error) {
            error = null;
            if (line.StartsWith("store ", StringComparison.Ordinal)) {
                var tokens = line.Substring(6).Split(
                    new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 2 || !tokens[1].StartsWith("bucketCount=", StringComparison.Ordinal)
                    || !int.TryParse(tokens[1].Substring(12), NumberStyles.None,
                        CultureInfo.InvariantCulture, out var count) || count <= 0 ||
                    (count & (count - 1)) != 0) {
                    error = "invalid store line";
                    return false;
                }

                try {
                    StoreNames.Normalize(tokens[0]);
                } catch (ArgumentException) {
                    error = "invalid store name";
                    return false;
                }

                if (manifest.Stores.ContainsKey(tokens[0])) {
                    error = "duplicate store";
                    return false;
                }

                current = new StoreRecord(count);
                manifest.Stores.Add(tokens[0], current);
                return true;
            }

            if (line.StartsWith("  bucket ", StringComparison.Ordinal) && current != null) {
                var tokens = line.Substring(9).Split(
                    new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 3 ||
                    !int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture,
                        out var bucket) || bucket < 0 ||
                    !tokens[1].StartsWith("version=", StringComparison.Ordinal) ||
                    !long.TryParse(tokens[1].Substring(8), NumberStyles.None,
                        CultureInfo.InvariantCulture, out var version) || version <= 0 ||
                    !tokens[2].StartsWith("bytes=", StringComparison.Ordinal) ||
                    !long.TryParse(tokens[2].Substring(6), NumberStyles.None,
                        CultureInfo.InvariantCulture, out var bytes) || bytes < 0) {
                    error = "invalid bucket line";
                    return false;
                }

                if (bucket >= current.BucketCount || current.Buckets.ContainsKey(bucket)) {
                    error = "invalid bucket record";
                    return false;
                }

                current.Buckets.Add(bucket, new BucketRecord(version, bytes));
                return true;
            }

            error = "unexpected manifest line";
            return false;
        }

        private static bool IsCanonicalNumber(string text) {
            if (text.Length == 0 || (text.Length > 1 && text[0] == '0')) {
                return false;
            }

            for (var i = 0; i < text.Length; i++) {
                if (text[i] < '0' || text[i] > '9') {
                    return false;
                }
            }

            return true;
        }
    }
}
