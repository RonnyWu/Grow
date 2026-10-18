// SPDX-License-Identifier: MIT

using System;

namespace Grow.Store.Contracts {
    /// <summary>A named set of primitive key/value entries committed through a shared index.</summary>
    /// <remarks>
    /// Supported values are <c>int</c>, <c>long</c>, <c>float</c>, <c>double</c>, <c>bool</c>,
    /// <c>string</c> and <c>byte[]</c>. Any other <c>T</c> is rejected with
    /// <see cref="NotSupportedException"/>.
    /// <para>
    /// Keys are ordinal and case-sensitive. A key cannot be null, empty or contain NUL. A
    /// <c>byte[]</c> value is copied on both sides: <see cref="Set{T}"/> copies the input and
    /// <see cref="Get{T}"/> returns a new array.
    /// </para>
    /// <para>
    /// Mutators return the store so calls can be chained. The cache changes immediately;
    /// <see cref="Save"/> commits and flushes it as one transaction. <see cref="Clear"/> changes only
    /// the cache, and the persisted store changes on the next save. There is no automatic timer.
    /// </para>
    /// <para>
    /// The provider owns the store lifetime; <see cref="IStore"/> is not <see cref="IDisposable"/>.
    /// Calls are main-thread and synchronous; one process may have one writer per scope. Do not call
    /// <see cref="Save"/> from <c>Update</c> or <c>OnGUI</c> every frame.
    /// </para>
    /// <para>
    /// When a file cannot be validated the store becomes <see cref="IsCorrupted"/>: reads return only
    /// data that can be validated and never throw for corruption, writes throw
    /// <see cref="InvalidOperationException"/>, and <see cref="Reset"/> clears and rebuilds it.
    /// </para>
    /// </remarks>
    public interface IStore {
        /// <summary>
        /// Reads a value, returning <paramref name="defaultValue"/> when the key is missing or its
        /// value cannot be read.
        /// </summary>
        /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not supported.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is empty or contains NUL.</exception>
        /// <exception cref="InvalidCastException">
        /// A non-blob value has a different kind. A blob read as anything other than <c>byte[]</c> is
        /// treated as unreadable and returns <paramref name="defaultValue"/>.
        /// </exception>
        T Get<T>(string key, T defaultValue = default);

        /// <summary>
        /// Tries to read a value; false when the key is missing, a different kind, or unreadable.
        /// </summary>
        /// <remarks><paramref name="value"/> is <c>default</c> when this returns <c>false</c>.</remarks>
        /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not supported.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is empty or contains NUL.</exception>
        bool TryGet<T>(string key, out T value);

        /// <summary>Writes a value into the cache; the next save persists it.</summary>
        /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not supported.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> or a <c>string</c>/<c>byte[]</c>
        /// value is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is empty or contains NUL.</exception>
        /// <exception cref="InvalidOperationException">The store is corrupted.</exception>
        IStore Set<T>(string key, T value);

        /// <summary>True when the cache contains <paramref name="key"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is empty or contains NUL.</exception>
        bool ContainsKey(string key);

        /// <summary>Removes a key from the cache; a missing key is ignored.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is empty or contains NUL.</exception>
        /// <exception cref="InvalidOperationException">The store is corrupted.</exception>
        IStore Delete(string key);

        /// <summary>Removes every key from the cache; the next save persists the empty store.</summary>
        /// <exception cref="InvalidOperationException">The store is corrupted.</exception>
        IStore Clear();

        /// <summary>Commits the cache and flushes it to the device; a clean store performs no write.</summary>
        /// <exception cref="System.IO.IOException">The write failed.</exception>
        /// <exception cref="InvalidOperationException">The store is corrupted.</exception>
        IStore Save();

        /// <summary>Tries to commit the cache; returns the error instead of throwing.</summary>
        /// <returns><c>false</c> for a corrupted store or an IO failure; otherwise <c>true</c>.</returns>
        bool TrySave(out string error);

        /// <summary>Logical size of the live data and referenced blobs.</summary>
        StoreSize Size { get; }

        /// <summary>True while the cache differs from the last successful commit.</summary>
        bool IsDirty { get; }

        /// <summary>True when a file could not be validated; writes are refused until reset.</summary>
        bool IsCorrupted { get; }

        /// <summary>Clears the store and rebuilds it empty; returns false when the rebuild fails.</summary>
        /// <remarks>
        /// The in-memory state is cleared, the store's manifest entry is rebuilt, and its blob and
        /// quarantine files are removed on a best-effort basis. A healthy store can be reset too.
        /// </remarks>
        bool Reset();
    }
}
