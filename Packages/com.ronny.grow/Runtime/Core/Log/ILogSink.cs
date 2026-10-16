// SPDX-License-Identifier: MIT

namespace Grow.Core.Log {
    /// <summary>
    /// Destination for entries emitted through <see cref="GrowLog"/>.
    /// </summary>
    /// <remarks>
    /// This is a kernel-owned extension point (see the architecture design, §8.2):
    /// it stays in <c>Grow.Core</c> rather than becoming a <c>Grow.&lt;Cap&gt;.Contracts</c> package.
    /// </remarks>
    public interface ILogSink {
        /// <summary>Writes a single entry.</summary>
        void Write(in LogEntry entry);
    }
}
