// SPDX-License-Identifier: MIT

using System;
using Grow.Kernel.Entry;

namespace Grow.Scenes {
    /// <summary>Composition sugar for the scenes capability.</summary>
    /// <remarks>
    /// Capability sugar lives in the capability package, not in the kernel: the builder only knows
    /// <see cref="GrowBuilder.AddModule"/>, so the kernel never learns that scenes exist.
    /// </remarks>
    public static class ScenesBuilderExtensions {
        /// <summary>Adds a <see cref="SceneModule"/> to the composition.</summary>
        /// <param name="builder">The composition builder.</param>
        /// <param name="initialScene">Scene loaded when the module starts; null loads nothing.</param>
        public static GrowBuilder UseScenes(this GrowBuilder builder, string initialScene = null) {
            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddModule(new SceneModule(initialScene));
        }
    }
}
