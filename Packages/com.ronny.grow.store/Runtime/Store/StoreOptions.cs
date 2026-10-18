// SPDX-License-Identifier: MIT

namespace Grow.Store {
    /// <summary>Configures the file backend and its lifecycle budget.</summary>
    /// <remarks>
    /// These values are implementation settings, not part of the store contract. Capacity values are
    /// targets or warning thresholds; only disk and IO failures stop a save.
    /// </remarks>
    public sealed class StoreOptions {
        /// <summary>Startup split target for one bucket. Not a hard limit.</summary>
        public long TargetBucketBytes = 2L * 1024 * 1024;

        /// <summary>Soft warning threshold for one blob. Not a hard limit.</summary>
        public long BlobWarningBytes = 64L * 1024 * 1024;

        /// <summary>Maximum bucket count reached by startup splitting.</summary>
        public int BucketCountLimit = 4096;

        /// <summary>Use the player path in the editor for <see cref="Contracts.StoreType.Runtime"/>.</summary>
        public bool UsePlayerPathInEditor;

        /// <summary>Maximum pending logical bytes saved from a lifecycle callback.</summary>
        public long LifecycleMaxBytes = 256L * 1024;

        /// <summary>Maximum number of stores saved from one lifecycle callback.</summary>
        public int LifecycleMaxStores = 8;

        /// <summary>Maximum number of store fsyncs from one lifecycle callback.</summary>
        public int LifecycleMaxFsyncs = 8;

        /// <summary>Stores with a pending blob larger than this are skipped by a lifecycle save.</summary>
        public long LifecycleMaxBlobBytes = 1L * 1024 * 1024;

        internal void Validate() {
            if (TargetBucketBytes <= 0) {
                throw new System.ArgumentOutOfRangeException(nameof(TargetBucketBytes));
            }

            if (BlobWarningBytes < 0) {
                throw new System.ArgumentOutOfRangeException(nameof(BlobWarningBytes));
            }

            if (BucketCountLimit <= 0 || !IsPowerOfTwo(BucketCountLimit)) {
                throw new System.ArgumentOutOfRangeException(nameof(BucketCountLimit));
            }

            if (LifecycleMaxBytes < 0 || LifecycleMaxStores < 0 || LifecycleMaxFsyncs < 0 ||
                LifecycleMaxBlobBytes < 0) {
                throw new System.ArgumentOutOfRangeException("Lifecycle budget cannot be negative.");
            }
        }

        private static bool IsPowerOfTwo(int value) {
            return value > 0 && (value & (value - 1)) == 0;
        }
    }
}
