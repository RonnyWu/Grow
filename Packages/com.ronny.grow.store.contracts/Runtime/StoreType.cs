// SPDX-License-Identifier: MIT

namespace Grow.Store.Contracts {
    /// <summary>Where a store lives. The default file paths are listed on each member.</summary>
    /// <remarks>
    /// All implementations keep their data out of the Windows registry: <c>EditorPrefs</c> and
    /// <c>PlayerPrefs</c> use registry-backed persistence on Windows, and repeated projects pollute
    /// the same keys. Store data is file-based, never a registry value.
    /// <para>
    /// <c>default(StoreType)</c> is <see cref="Editor"/>; pass a type explicitly when the default may
    /// be a player build.
    /// </para>
    /// </remarks>
    public enum StoreType {
        /// <summary>
        /// Editor-only data under the project's <c>UserSettings/Grow/Editor</c>. Resolving this type
        /// in a player is unsupported.
        /// </summary>
        Editor,

        /// <summary>
        /// Runtime data. By default the editor uses <c>UserSettings/Grow/Runtime</c>; a player uses
        /// <c>Application.persistentDataPath/Grow/Runtime</c>.
        /// </summary>
        Runtime,

        /// <summary>
        /// User-scoped data. In the editor under <c>UserSettings/Grow/User</c>; in a player under
        /// <c>Application.persistentDataPath/Grow/User</c>.
        /// </summary>
        User
    }
}
