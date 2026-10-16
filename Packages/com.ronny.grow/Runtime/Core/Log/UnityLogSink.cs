// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Grow.Core.Log {
    /// <summary>
    /// Default <see cref="ILogSink"/>: forwards entries to <see cref="UnityEngine.Debug"/>, so they
    /// land in the Unity console like any other log.
    /// </summary>
    public sealed class UnityLogSink : ILogSink {
        /// <inheritdoc />
        public void Write(in LogEntry entry) {
            var message = Format(entry);
            switch (entry.Level) {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(message);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case LogLevel.Error:
                    Debug.LogError(message);
                    break;
                default:
                    // Never downgrade silently: an unmapped level means this mapping is incomplete.
                    Debug.LogError($"Unmapped {nameof(LogLevel)} '{entry.Level}': {message}");
                    break;
            }
        }

        private static string Format(in LogEntry entry) {
            return entry.Category == null
                ? $"[{entry.Level}] {entry.Message}"
                : $"[{entry.Level}][{entry.Category}] {entry.Message}";
        }
    }
}
