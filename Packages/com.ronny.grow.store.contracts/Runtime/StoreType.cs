// SPDX-License-Identifier: MIT

namespace Grow.Store.Contracts {
    /// <summary>Where a store lives. The implementation owns the actual paths.</summary>
    /// <remarks>
    /// All implementations keep their data out of the Windows registry: <c>EditorPrefs</c> and
    /// <c>PlayerPrefs</c> write there, and repeated projects pollute the same keys. A store is a file
    /// (or an equivalent platform file API), never a registry value.
    /// </remarks>
    public enum StoreType {
        /// <summary>
        /// Editor-only data under the project's <c>UserSettings/Grow/Editor</c>. Resolving this type
        /// in a player is unsupported.
        /// </summary>
        Editor,

        /// <summary>
        /// Runtime data. In the editor it lives under <c>UserSettings/Grow/Runtime</c>; in a player
        /// under <c>Application.persistentDataPath/Grow/Runtime</c>.
        /// </summary>
        Runtime,

        /// <summary>
        /// User-scoped data. In the editor under <c>UserSettings/Grow/User</c>; in a player under
        /// <c>Application.persistentDataPath/Grow/User</c>.
        /// </summary>
        User
    }
}
