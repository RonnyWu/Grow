// SPDX-License-Identifier: MIT

namespace Grow.Store.Contracts {
    /// <summary>
    /// Maintenance operations that are not part of the everyday store contract.
    /// </summary>
    /// <remarks>
    /// Obtain an implementation from the store module. Call these only at a safe point, never from
    /// <c>Update</c> or <c>OnGUI</c>: both operations can rewrite or scan a store.
    /// </remarks>
    public interface IStoreDiagnostics {
        /// <summary>
        /// Splits a store's buckets until every bucket is at or below its target size, or the
        /// bucket-count limit is reached.
        /// </summary>
        bool TrySplit(string storeName, StoreType storeType, out string error);

        /// <summary>Deletes unreferenced bucket versions and blob files for one store.</summary>
        bool TryGc(string storeName, StoreType storeType, out string error);
    }
}
