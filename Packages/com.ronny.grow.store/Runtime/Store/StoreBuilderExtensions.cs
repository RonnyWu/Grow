// SPDX-License-Identifier: MIT

using System;
using Grow.Kernel.Entry;

namespace Grow.Store {
    /// <summary>Composition sugar for the store capability.</summary>
    /// <remarks>
    /// Capability sugar lives in the capability package, not in the kernel: the builder only knows
    /// <see cref="GrowBuilder.AddModule"/>, so the kernel never learns that a store exists.
    /// </remarks>
    public static class StoreBuilderExtensions {
        /// <summary>Adds a <see cref="StoreModule"/> with the default store and options.</summary>
        /// <param name="builder">The composition builder.</param>
        /// <param name="options">File backend options; the defaults are used when omitted.</param>
        public static GrowBuilder UseStore(this GrowBuilder builder, StoreOptions options = null) {
            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddModule(new StoreModule(options));
        }
    }
}
