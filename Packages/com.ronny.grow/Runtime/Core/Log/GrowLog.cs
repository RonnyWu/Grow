// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Grow.Core.Log {
    /// <summary>
    /// Static logging facade used by the framework itself.
    /// </summary>
    /// <remarks>
    /// Logging is a kernel <em>mechanism</em> (the framework needs to report problems), not a
    /// wrapped capability: it ships a pass-through facade plus a default sink, and callers replace
    /// the sink through <see cref="SetSink"/>.
    /// <para>
    /// Filtering happens in the facade, in front of the entry, so a call site whose message is
    /// expensive to build guards it:
    /// <c>if (GrowLog.IsEnabled(LogLevel.Debug)) GrowLog.Debug(BuildMessage());</c>.
    /// </para>
    /// <para>
    /// The per-level methods collide with <see cref="UnityEngine.Debug"/> by name. In a file that
    /// imports both this namespace and <c>UnityEngine</c>, qualify Unity's as
    /// <c>UnityEngine.Debug</c>.
    /// </para>
    /// </remarks>
    public static class GrowLog {
        private static ILogSink _sink = new UnityLogSink();

        /// <summary>Entries below this level are dropped. Defaults to <see cref="LogLevel.Info"/>.</summary>
        public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        /// <summary>The current sink.</summary>
        public static ILogSink Sink => _sink;

        /// <summary>Replaces the sink. Must not be <c>null</c>.</summary>
        public static void SetSink(ILogSink sink) {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>True when <paramref name="level"/> passes the current filter.</summary>
        public static bool IsEnabled(LogLevel level) {
            return level >= MinimumLevel;
        }

        /// <summary>Writes an entry if the level passes the filter.</summary>
        /// <remarks>
        /// A sink that throws is contained: the failure goes to <see cref="UnityEngine.Debug"/> and
        /// never reaches the caller. The kernel logs while it transitions, so a broken sink must not
        /// be able to abort a lifecycle entry.
        /// </remarks>
        public static void Write(LogLevel level, string message, string category = null) {
            if (level < MinimumLevel) {
                return;
            }

            // Read the sink once: a swap must not be observed half-way through a write.
            var sink = _sink;
            try {
                sink.Write(new LogEntry(level, message, category, DateTime.UtcNow));
            } catch (Exception exception) {
                UnityEngine.Debug.LogException(exception);
            }
        }

        /// <summary>Logs at <see cref="LogLevel.Trace"/>.</summary>
        public static void Trace(string message, string category = null) {
            Write(LogLevel.Trace, message, category);
        }

        /// <summary>Logs at <see cref="LogLevel.Debug"/>.</summary>
        public static void Debug(string message, string category = null) {
            Write(LogLevel.Debug, message, category);
        }

        /// <summary>Logs at <see cref="LogLevel.Info"/>.</summary>
        public static void Info(string message, string category = null) {
            Write(LogLevel.Info, message, category);
        }

        /// <summary>Logs at <see cref="LogLevel.Warning"/>.</summary>
        public static void Warning(string message, string category = null) {
            Write(LogLevel.Warning, message, category);
        }

        /// <summary>Logs at <see cref="LogLevel.Error"/>.</summary>
        public static void Error(string message, string category = null) {
            Write(LogLevel.Error, message, category);
        }

        /// <summary>Restores the default sink and level.</summary>
        /// <remarks>
        /// Called at the start of every play session by this type's own runtime hook and by
        /// <c>GrowSession</c>, and by tests.
        /// </remarks>
        public static void Reset() {
            _sink = new UnityLogSink();
            MinimumLevel = LogLevel.Info;
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnLoad() {
            Reset();
        }

    }
}
