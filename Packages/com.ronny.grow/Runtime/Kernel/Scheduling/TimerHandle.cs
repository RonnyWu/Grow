// SPDX-License-Identifier: MIT

using System;
using Grow.Core.Collections;

namespace Grow.Kernel.Scheduling {
    /// <summary>A reference to a scheduled timer, accepted by <see cref="GrowScheduler.Cancel"/>.</summary>
    /// <remarks>
    /// The handle pairs a slot index with the slot's generation, so cancelling it after the timer
    /// fired — or after the slot was reused — is a no-op instead of cancelling an unrelated timer.
    /// The default value is <see cref="None"/> and is never valid.
    /// </remarks>
    public readonly struct TimerHandle : IEquatable<TimerHandle> {
        private readonly SlotHandle _slot;

        internal TimerHandle(SlotHandle slot) {
            _slot = slot;
        }

        /// <summary>A handle that refers to no timer.</summary>
        public static TimerHandle None => default;

        /// <summary>
        /// True when the handle was issued by <see cref="GrowScheduler"/>. It does not say whether
        /// the timer is still live; <see cref="GrowScheduler.Cancel"/> is the authority for that.
        /// </summary>
        public bool IsValid => !_slot.IsDefault;

        internal SlotHandle Slot => _slot;

        /// <inheritdoc />
        public bool Equals(TimerHandle other) {
            return _slot.Equals(other._slot);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) {
            return obj is TimerHandle other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode() {
            return _slot.GetHashCode();
        }

        /// <summary>True when both handles refer to the same timer.</summary>
        public static bool operator ==(TimerHandle left, TimerHandle right) {
            return left.Equals(right);
        }

        /// <summary>True when the handles differ.</summary>
        public static bool operator !=(TimerHandle left, TimerHandle right) {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public override string ToString() {
            return _slot.IsDefault ? "TimerHandle(None)" : _slot.ToString();
        }
    }
}
