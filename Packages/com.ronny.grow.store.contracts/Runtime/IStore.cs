// SPDX-License-Identifier: MIT

using System;

namespace Grow.Store.Contracts {
    /// <summary>A named set of primitive key/value entries persisted as a whole.</summary>
    /// <remarks>
    /// Supported values are <c>int</c>, <c>long</c>, <c>float</c>, <c>double</c>, <c>bool</c>,
    /// <c>string</c> and <c>byte[]</c>. Any other <c>T</c> is rejected with
    /// <see cref="NotSupportedException"/>.
    /// <para>
    /// Mutators return the store so calls can be chained. The cache changes immediately;
    /// <see cref="Save"/> commits and flushes it as one transaction. <see cref="Clear"/> changes only
    /// the cache, and the persisted store changes on the next save.
    /// </para>
    /// <para>
    /// Keys are non-empty and round-trip through the file format. A key cannot contain NUL. Reading a
    /// key with a different <c>T</c> is a miss for <see cref="TryGet{T}"/> and throws
    /// <see cref="InvalidCastException"/> for <see cref="Get{T}"/>.
    /// </para>
    /// <para>
    /// The provider owns the store lifetime. When a file cannot be validated the store becomes
    /// <see cref="IsCorrupted"/>: reads return only data that can be validated, writes throw
    /// <see cref="InvalidOperationException"/>, and <see cref="Reset"/> clears and rebuilds it.
    /// </para>
    /// </remarks>
    public interface IStore {
        /// <summary>Reads a value, returning <paramref name="defaultValue"/> when the key is missing.</summary>
        /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not supported.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is empty or contains NUL.</exception>
        /// <exception cref="InvalidCastException">The stored value has a different kind.</exception>
        /// <exception cref="InvalidOperationException">The store is corrupted.</exception>
        T Get<T>(string key, T defaultValue = default);

        /// <summary>Tries to read a value; false when missing, a different kind, or unreadable.</summary>
        bool TryGet<T>(string key, out T value);

        /// <summary>Writes a value into the cache; the next save persists it.</summary>
        IStore Set<T>(string key, T value);

        /// <summary>True when the cache contains <paramref name="key"/>.</summary>
        bool ContainsKey(string key);

        /// <summary>Removes a key from the cache; a missing key is ignored.</summary>
        IStore Delete(string key);

        /// <summary>Removes every key from the cache; the next save persists the empty store.</summary>
        IStore Clear();

        /// <summary>Commits the cache and flushes it to the device; a clean store performs no IO.</summary>
        IStore Save();

        /// <summary>Tries to commit the cache; returns the error instead of throwing.</summary>
        bool TrySave(out string error);

        /// <summary>Logical size of the live data and referenced blobs.</summary>
        StoreSize Size { get; }

        /// <summary>True while the cache differs from the last successful commit.</summary>
        bool IsDirty { get; }

        /// <summary>True when a file could not be validated; writes are refused until reset.</summary>
        bool IsCorrupted { get; }

        /// <summary>Clears the store and rebuilds it empty; returns false when the rebuild fails.</summary>
        bool Reset();
    }
}
