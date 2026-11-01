// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Scheduling {
    /// <summary>Which clock a time-based delay or repeat follows.</summary>
    /// <remarks>
    /// <see cref="Scaled"/> follows <c>Time.timeScale</c> and therefore stops while the game is
    /// paused; <see cref="Unscaled"/> keeps running. Frame-based entries ignore this setting.
    /// </remarks>
    public enum TimeKind {
        /// <summary>Delta time scaled by <c>Time.timeScale</c>.</summary>
        Scaled = 0,

        /// <summary>Delta time that ignores <c>Time.timeScale</c>.</summary>
        Unscaled = 1
    }
}
