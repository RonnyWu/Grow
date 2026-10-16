// SPDX-License-Identifier: MIT

using System;

namespace Grow.Core.Log {
    /// <summary>
    /// One log record passed to an <see cref="ILogSink"/>.
    /// </summary>
    public readonly struct LogEntry {
        /// <summary>Creates a log entry.</summary>
        public LogEntry(LogLevel level, string message, string category, DateTime timestamp) {
            Level = level;
            Message = message ?? string.Empty;
            Category = category;
            Timestamp = timestamp;
        }

        /// <summary>Severity of the entry.</summary>
        public LogLevel Level { get; }

        /// <summary>Message text (never null).</summary>
        public string Message { get; }

        /// <summary>Optional category; <c>null</c> when unspecified.</summary>
        public string Category { get; }

        /// <summary>UTC timestamp of the entry.</summary>
        public DateTime Timestamp { get; }
    }
}
