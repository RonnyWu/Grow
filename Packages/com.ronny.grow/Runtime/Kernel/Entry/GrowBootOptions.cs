// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Entry {
    /// <summary>Startup switches applied by <see cref="GrowBuilder.Build"/>.</summary>
    /// <remarks>
    /// The defaults are the ones the builder documents: start immediately and take over automatic
    /// initialization. Both are per build, not process-wide settings.
    /// </remarks>
    public sealed class GrowBootOptions {
        /// <summary>
        /// When true (the default), <see cref="GrowBuilder.Build"/> starts the host before it
        /// returns.
        /// </summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>
        /// When true (the default), the builder turns <see cref="GrowApp.AutoInitialize"/> off while
        /// the host lives — so the automatic <c>BeforeSceneLoad</c> start cannot race it — and
        /// restores it when the host is disposed. A flag the caller had already turned off is left
        /// alone.
        /// </summary>
        public bool CaptureAutoInitialize { get; set; } = true;
    }
}
