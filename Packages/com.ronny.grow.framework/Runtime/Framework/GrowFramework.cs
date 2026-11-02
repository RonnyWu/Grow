// SPDX-License-Identifier: MIT

using Grow.Kernel.Entry;
using Grow.Scenes;
using Grow.Store;

namespace Grow.Framework {
    /// <summary>
    /// The Grow meta package: one call that composes the default capability set.
    /// </summary>
    /// <remarks>
    /// This is not a new layer and not a new abstraction. It is the same <see cref="GrowBuilder"/> a
    /// game would write by hand, with the recommended capabilities already added, so a game starts
    /// at <c>Create().AddModule(...)</c> instead of remembering which packages to install and which
    /// extension method adds them.
    /// <para>
    /// The default set is the store (<c>com.ronny.grow.store</c>) and the scene flow
    /// (<c>com.ronny.grow.scenes</c>). Both are added through their own capability packages; the
    /// kernel never learns that they exist, and replacing a capability means not calling this
    /// factory — the builder below it stays public.
    /// </para>
    /// <para>
    /// `Create` does not scan assemblies, discover modules or apply hidden configuration. The
    /// optional arguments are the two decisions the default set cannot guess: where the store writes
    /// and which scene to load first.
    /// </para>
    /// </remarks>
    public static class GrowFramework {
        /// <summary>
        /// Returns a builder with the default capabilities added; the caller adds its own modules and
        /// calls <see cref="GrowBuilder.Build"/>.
        /// </summary>
        /// <param name="store">File backend options for the store; the defaults are used when null.</param>
        /// <param name="initialScene">Scene loaded when the scene module starts; null loads nothing.</param>
        public static GrowBuilder Create(StoreOptions store = null, string initialScene = null) {
            return new GrowBuilder()
                .UseStore(store)
                .UseScenes(initialScene);
        }
    }
}
