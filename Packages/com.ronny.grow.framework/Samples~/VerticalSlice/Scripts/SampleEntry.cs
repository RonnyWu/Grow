// SPDX-License-Identifier: MIT

using Grow.Framework;
using Grow.Kernel.Entry;
using UnityEngine;

namespace Grow.Samples.VerticalSlice {
    /// <summary>
    /// Starts the sample composition once, after the kernel's own session reset.
    /// </summary>
    /// <remarks>
    /// The sample owns its entry point on purpose: a game replaces this class with its own startup,
    /// which is the only line where the choice of capabilities is made.
    /// </remarks>
    public static class SampleEntry {
        private static GrowHost _host;

        /// <summary>The running composition, or null before startup.</summary>
        public static GrowHost Host => _host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Start() {
            _host = GrowFramework.Create()
                .AddModule(new GameModule())
                .AddModule(new SessionModule("sample"))
                .Build();
        }
    }
}
