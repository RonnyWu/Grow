// SPDX-License-Identifier: MIT

using System;
using System.Globalization;
using System.Text;

namespace Grow.Store.Format {
    /// <summary>Stable bucket hash and CRC32 used by the file format.</summary>
    internal static class StoreHash {
        public const string HashId = "fnv1a32-mix1";

        private static readonly uint[] CrcTable = BuildCrcTable();

        /// <summary>FNV-1a 32 over the UTF-8 key, followed by a 32-bit mixing step.</summary>
        public static uint Hash(string key) {
            var bytes = Encoding.UTF8.GetBytes(key);
            uint hash = 2166136261;
            for (var i = 0; i < bytes.Length; i++) {
                hash ^= bytes[i];
                hash *= 16777619;
            }

            hash ^= hash >> 16;
            hash *= 0x7feb352d;
            hash ^= hash >> 15;
            hash *= 0x846ca68b;
            hash ^= hash >> 16;
            return hash;
        }

        /// <summary>CRC32 of a UTF-8 string.</summary>
        public static uint Crc32(string value) {
            var bytes = Encoding.UTF8.GetBytes(value);
            return Crc32(bytes, 0, bytes.Length);
        }

        /// <summary>CRC32 of a byte range.</summary>
        public static uint Crc32(byte[] bytes, int offset, int count) {
            uint crc = 0xFFFFFFFF;
            for (var i = 0; i < count; i++) {
                crc = CrcTable[(crc ^ bytes[offset + i]) & 0xFF] ^ (crc >> 8);
            }

            return ~crc;
        }

        public static string Hex(uint value) {
            return value.ToString("x8", CultureInfo.InvariantCulture);
        }

        public static bool TryParseHex(string text, out uint value) {
            return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        private static uint[] BuildCrcTable() {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++) {
                var value = i;
                for (var bit = 0; bit < 8; bit++) {
                    value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }
    }
}
