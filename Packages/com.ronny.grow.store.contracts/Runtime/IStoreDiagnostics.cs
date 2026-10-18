// SPDX-License-Identifier: MIT

namespace Grow.Store.Contracts {
    /// <summary>
    /// Maintenance operations that are not part of the everyday store contract.
    /// </summary>
    /// <remarks>
    /// Obtain an implementation from the store module (the default file implementation exposes
    /// <c>StoreModule.Diagnostics</c>; a custom provider may return <c>null</c>). Call these only at a
    /// safe point, never from <c>Update</c> or <c>OnGUI</c>: both operations rewrite or scan a store.
    /// <para>
    /// Both operations require the store to be open, persisted and clean. With uncommitted changes
    /// they return <c>false</c> and touch no file, because the blobs and bucket versions they reclaim
    /// are only known once the change is committed.
    /// </para>
    /// </remarks>
    public interface IStoreDiagnostics {
        /// <summary>
        /// Doubles the bucket count while a bucket exceeds its target, stopping at the bucket-count
        /// limit or when doubling would produce more buckets than keys.
        /// </summary>
        /// <returns>
        /// <c>true</c> when the pass completes, even if a single value still exceeds the target;
        /// <c>false</c> when the store is not open, has no persisted data, is dirty, or the rewrite
        /// fails. <paramref name="error"/> is <c>null</c> on success and a readable message otherwise.
        /// </returns>
        bool TrySplit(string storeName, StoreType storeType, out string error);

        /// <summary>Deletes unreferenced bucket versions, blob files and temporary files.</summary>
        /// <returns>
        /// <c>true</c> on success; <c>false</c> when the store is not open, is dirty, or the pass
        /// fails. <paramref name="error"/> is <c>null</c> on success.
        /// </returns>
        bool TryGc(string storeName, StoreType storeType, out string error);
    }
}
