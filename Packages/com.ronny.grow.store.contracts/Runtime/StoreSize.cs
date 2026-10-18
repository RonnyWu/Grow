// SPDX-License-Identifier: MIT

namespace Grow.Store.Contracts {
    /// <summary>Logical sizes of one store, excluding headers, old versions and temporary files.</summary>
    /// <remarks>
    /// <see cref="DataBytes"/> is the sum of the live key/value records, <see cref="BlobBytes"/> is
    /// the raw size of the blobs those records point at, and <see cref="TotalBytes"/> is their sum.
    /// This is a logical size, not the size on disk.
    /// </remarks>
    public readonly struct StoreSize {
        /// <summary>Creates a logical size; negative values are treated as zero.</summary>
        public StoreSize(long dataBytes, long blobBytes) {
            DataBytes = dataBytes < 0 ? 0 : dataBytes;
            BlobBytes = blobBytes < 0 ? 0 : blobBytes;
        }

        /// <summary>Logical key/value bytes.</summary>
        public long DataBytes { get; }

        /// <summary>Logical blob bytes referenced by the key/value records.</summary>
        public long BlobBytes { get; }

        /// <summary>Logical total; not the size on disk.</summary>
        public long TotalBytes => DataBytes + BlobBytes;
    }
}
