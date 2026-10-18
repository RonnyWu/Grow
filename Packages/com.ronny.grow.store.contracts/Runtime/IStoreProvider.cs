// SPDX-License-Identifier: MIT

using System;

namespace Grow.Store.Contracts {
    /// <summary>Opens, closes, saves and deletes named stores.</summary>
    /// <remarks>
    /// A store is identified by the (name, <see cref="StoreType"/>) pair; asking for the same pair
    /// twice returns the same open store. One provider owns one scope directory: a second provider
    /// opening the same scope fails instead of overwriting the shared index. The provider owns every
    /// store it opens.
    /// </remarks>
    public interface IStoreProvider : IDisposable {
        /// <summary>Returns the cached store for the pair, opening it on first use.</summary>
        IStore GetStore(string storeName, StoreType storeType);

        /// <summary>Returns an already-open store without opening or creating one.</summary>
        bool TryGetStore(string storeName, StoreType storeType, out IStore store);

        /// <summary>Saves a dirty, healthy store and releases it; false when the save fails.</summary>
        bool Close(string storeName, StoreType storeType);

        /// <summary>Saves every healthy dirty store and commits the scope index once.</summary>
        bool TrySaveAll(out string error);

        /// <summary>Removes the store index entry, then deletes its files.</summary>
        bool DeleteStore(string storeName, StoreType storeType);

        /// <summary>True when <paramref name="storeType"/> can be opened on this platform.</summary>
        bool IsSupported(StoreType storeType);
    }
}
