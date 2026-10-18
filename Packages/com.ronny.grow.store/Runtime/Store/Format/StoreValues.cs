// SPDX-License-Identifier: MIT

using System;
using System.Globalization;
using System.Text;

namespace Grow.Store.Format {
    internal enum StoreValueKind {
        Int,
        Long,
        Float,
        Double,
        Bool,
        String,
        Blob
    }

    /// <summary>One cached value: inline text, or pending blob bytes before a commit.</summary>
    internal readonly struct StoreEntry {
        public StoreEntry(StoreValueKind kind, string text, byte[] data) {
            Kind = kind;
            Text = text;
            Data = data;
        }

        public StoreValueKind Kind { get; }

        /// <summary>Inline text or the blob pointer. Null while blob bytes are pending.</summary>
        public string Text { get; }

        /// <summary>Pending blob bytes; null for inline values and loaded blobs.</summary>
        public byte[] Data { get; }

        public StoreEntry WithText(string text) {
            return new StoreEntry(Kind, text, null);
        }
    }

    internal static class StoreValues {
        public static string NormalizeKey(string key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length == 0) {
                throw new ArgumentException("A store key cannot be empty.", nameof(key));
            }

            if (key.IndexOf('\0') >= 0) {
                throw new ArgumentException("A store key cannot contain NUL.", nameof(key));
            }

            return key;
        }

        public static bool IsSupported(Type type) {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) ||
                   type == typeof(double) || type == typeof(bool) || type == typeof(string) ||
                   type == typeof(byte[]);
        }

        public static StoreEntry Create<T>(T value) {
            if (ReferenceEquals(value, null)) {
                throw new ArgumentNullException(nameof(value), "Use Delete for a null value.");
            }

            if (value is string text) {
                return new StoreEntry(StoreValueKind.String, text, null);
            }

            if (value is byte[] bytes) {
                var copy = new byte[bytes.Length];
                Array.Copy(bytes, copy, bytes.Length);
                return new StoreEntry(StoreValueKind.Blob, null, copy);
            }

            if (value is bool flag) {
                return new StoreEntry(StoreValueKind.Bool, flag ? "1" : "0", null);
            }

            if (value is int intValue) {
                return new StoreEntry(
                    StoreValueKind.Int, intValue.ToString(CultureInfo.InvariantCulture), null);
            }

            if (value is long longValue) {
                return new StoreEntry(
                    StoreValueKind.Long, longValue.ToString(CultureInfo.InvariantCulture), null);
            }

            if (value is float floatValue) {
                return new StoreEntry(
                    StoreValueKind.Float, floatValue.ToString("R", CultureInfo.InvariantCulture),
                    null);
            }

            if (value is double doubleValue) {
                return new StoreEntry(
                    StoreValueKind.Double, doubleValue.ToString("R", CultureInfo.InvariantCulture),
                    null);
            }

            throw Unsupported<T>();
        }

        public static bool TryGet<T>(StoreEntry entry, out T value) {
            if (!IsSupported(typeof(T)) || entry.Kind == StoreValueKind.Blob) {
                value = default;
                return false;
            }

            var expected = KindOf(typeof(T));
            if (entry.Kind != expected) {
                value = default;
                return false;
            }

            if (expected == StoreValueKind.String) {
                value = (T)(object)entry.Text;
                return true;
            }

            if (expected == StoreValueKind.Int) {
                if (int.TryParse(entry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var intValue)) {
                    value = (T)(object)intValue;
                    return true;
                }
            } else if (expected == StoreValueKind.Long) {
                if (long.TryParse(entry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var longValue)) {
                    value = (T)(object)longValue;
                    return true;
                }
            } else if (expected == StoreValueKind.Float) {
                if (float.TryParse(entry.Text, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var floatValue)) {
                    value = (T)(object)floatValue;
                    return true;
                }
            } else if (expected == StoreValueKind.Double) {
                if (double.TryParse(entry.Text, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var doubleValue)) {
                    value = (T)(object)doubleValue;
                    return true;
                }
            } else if (expected == StoreValueKind.Bool) {
                if (entry.Text == "1") {
                    value = (T)(object)true;
                    return true;
                }

                if (entry.Text == "0") {
                    value = (T)(object)false;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static string KindToken(StoreValueKind kind) {
            switch (kind) {
                case StoreValueKind.Int:
                    return "int";
                case StoreValueKind.Long:
                    return "long";
                case StoreValueKind.Float:
                    return "float";
                case StoreValueKind.Double:
                    return "double";
                case StoreValueKind.Bool:
                    return "bool";
                case StoreValueKind.String:
                    return "string";
                case StoreValueKind.Blob:
                    return "blob";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public static bool TryParseKind(string token, out StoreValueKind kind) {
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
                case "blob":
                    kind = StoreValueKind.Blob;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        public static string Escape(string value) {
            if (value.IndexOf('\\') < 0 && value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0
                && value.IndexOf('\t') < 0 && value.IndexOf('\0') < 0) {
                return value;
            }

            var builder = new StringBuilder(value.Length + 8);
            for (var i = 0; i < value.Length; i++) {
                switch (value[i]) {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\0':
                        builder.Append("\\0");
                        break;
                    default:
                        builder.Append(value[i]);
                        break;
                }
            }

            return builder.ToString();
        }

        public static string Unescape(string value) {
            if (value.IndexOf('\\') < 0) {
                return value;
            }

            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++) {
                if (value[i] != '\\' || i + 1 >= value.Length) {
                    builder.Append(value[i]);
                    continue;
                }

                i++;
                switch (value[i]) {
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case '0':
                        builder.Append('\0');
                        break;
                    case '\\':
                        builder.Append('\\');
                        break;
                    default:
                        builder.Append('\\').Append(value[i]);
                        break;
                }
            }

            return builder.ToString();
        }

        public static string FormatPointer(string blobId, long length, uint crc) {
            return string.Concat(
                blobId, ":", length.ToString(CultureInfo.InvariantCulture), ":", StoreHash.Hex(crc));
        }

        public static bool TryParsePointer(
            string text, out string blobId, out long length, out uint crc) {
            blobId = null;
            length = 0;
            crc = 0;
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            var first = text.IndexOf(':');
            var second = first < 0 ? -1 : text.IndexOf(':', first + 1);
            if (first != 32 || second <= first) {
                return false;
            }

            blobId = text.Substring(0, 32);
            for (var i = 0; i < blobId.Length; i++) {
                var character = blobId[i];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f'))) {
                    return false;
                }
            }

            if (!long.TryParse(
                    text.Substring(first + 1, second - first - 1), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out length) || length < 0) {
                return false;
            }

            return StoreHash.TryParseHex(text.Substring(second + 1), out crc);
        }

        public static NotSupportedException Unsupported<T>() {
            return new NotSupportedException(
                $"Store values support int, long, float, double, bool, string and byte[]; " +
                $"'{typeof(T)}' is not supported.");
        }

        public static StoreValueKind KindOf(Type type) {
            if (type == typeof(int)) {
                return StoreValueKind.Int;
            }

            if (type == typeof(long)) {
                return StoreValueKind.Long;
            }

            if (type == typeof(float)) {
                return StoreValueKind.Float;
            }

            if (type == typeof(double)) {
                return StoreValueKind.Double;
            }

            if (type == typeof(bool)) {
                return StoreValueKind.Bool;
            }

            if (type == typeof(string)) {
                return StoreValueKind.String;
            }

            if (type == typeof(byte[])) {
                return StoreValueKind.Blob;
            }

            throw new NotSupportedException($"No store kind for '{type}'.");
        }

        public static long EstimateBytes(string key, StoreEntry entry) {
            var textBytes = Encoding.UTF8.GetByteCount(entry.Text ?? string.Empty);
            var dataBytes = entry.Data == null ? 0 : entry.Data.Length;
            return Encoding.UTF8.GetByteCount(key) + textBytes + 16 + dataBytes;
        }

        public static bool TryGetPointer(StoreEntry entry, out string blobId, out long length,
            out uint crc) {
            if (entry.Kind == StoreValueKind.Blob) {
                return TryParsePointer(entry.Text, out blobId, out length, out crc);
            }

            blobId = null;
            length = 0;
            crc = 0;
            return false;
        }
    }
}
