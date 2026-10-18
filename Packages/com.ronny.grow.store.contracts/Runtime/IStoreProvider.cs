// SPDX-License-Identifier: MIT

using System;

namespace Grow.Store.Contracts {
    /// <summary>Opens, closes, saves and deletes named stores.</summary>
    /// <remarks>
    /// A store is identified by the (name, <see cref="StoreType"/>) pair; asking for the same pair
    /// twice returns the same open store. One provider serves all three scope directories, and each
    /// directory has exactly one writer: the store module holds that lease, and a second module that
    /// opens an already-held scope throws <see cref="InvalidOperationException"/> at startup, not from
    /// this interface. The provider owns every store it opens.
    /// <para>
    /// A store name is 1–64 characters of <c>a-z</c>, <c>0-9</c>, <c>.</c>, <c>_</c> and <c>-</c>,
    /// must start and end with a letter or digit, and may not contain <c>..</c>, a path separator, a
    /// control or whitespace character, a Windows device name, <c>.blobs</c>, <c>manifests</c> or
    /// <c>quarantine</c>. Names are folded to lower case. <see cref="GetStore"/> throws
    /// <see cref="ArgumentException"/> for an invalid name; <see cref="GetStore"/> also throws
    /// <see cref="NotSupportedException"/> when <see cref="IsSupported"/> rejects the store type.
    /// </para>
    /// <para>
    /// <see cref="IDisposable.Dispose"/> releases every open store without saving: a dirty store is
    /// discarded, so call <see cref="TrySaveAll"/> first. After disposal this interface and every
    /// <see cref="IStore"/> obtained from it throw <see cref="ObjectDisposedException"/>.
    /// </para>
    /// </remarks>
    public interface IStoreProvider : IDisposable {
        /// <summary>
        /// Returns the cached store for the pair, opening it empty on first use; nothing is written
        /// until the first <see cref="IStore.Save"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="storeName"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="storeName"/> is empty or invalid.</exception>
        /// <exception cref="NotSupportedException"><paramref name="storeType"/> is not available.</exception>
        IStore GetStore(string storeName, StoreType storeType);

        /// <summary>Returns an already-open store without opening or creating one.</summary>
        /// <returns><c>false</c> when the pair is not open; <paramref name="store"/> is then <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="storeName"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="storeName"/> is empty or invalid.</exception>
        bool TryGetStore(string storeName, StoreType storeType, out IStore store);

        /// <summary>Releases a store, saving it first when it is dirty and healthy.</summary>
        /// <returns>
        /// <c>true</c> when the store was released. <c>false</c> when the store is not open, or when a
        /// dirty store failed to save; a failed close keeps the store open and dirty. A corrupted
        /// store is released without writing and still returns <c>true</c>. A released handle becomes
        /// invalid.
        /// </returns>
        bool Close(string storeName, StoreType storeType);

        /// <summary>Saves every healthy dirty store and commits each scope index once.</summary>
        /// <param name="error">The first failure, or <c>null</c> when the call succeeds.</param>
        /// <returns>
        /// <c>false</c> when a healthy store fails to save, or when a corrupted dirty store was
        /// skipped; <c>true</c> means every dirty healthy store is committed.
        /// </returns>
        bool TrySaveAll(out string error);

        /// <summary>
        /// Deletes a store: closes any open instance without saving, commits the removal of its index
        /// entry, then deletes its files.
        /// </summary>
        /// <remarks>
        /// Uncommitted changes are discarded. The index entry is committed as removed before the files
        /// are deleted, so a <c>false</c> returned after that point still leaves the store logically
        /// deleted; leftover files are reclaimed by a later GC.
        /// </remarks>
        bool DeleteStore(string storeName, StoreType storeType);

        /// <summary>True when <paramref name="storeType"/> can be opened on this platform.</summary>
        /// <remarks><see cref="StoreType.Editor"/> is <c>false</c> in a player.</remarks>
        bool IsSupported(StoreType storeType);
    }
}
