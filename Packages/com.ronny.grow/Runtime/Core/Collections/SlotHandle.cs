// SPDX-License-Identifier: MIT

using System;

namespace Grow.Core.Collections {
    /// <summary>
    /// A stable handle into a <see cref="SlotMap{T}"/>.
    /// </summary>
    /// <remarks>
    /// The handle pairs a slot index with the generation the slot had when the value was added.
    /// Removing the value bumps the generation, so a stale handle cannot reach a newer value that
    /// reuses the same slot until the 32-bit generation wraps. The <c>default</c> handle has
    /// generation 0 and is always invalid.
    /// <para>
    /// A handle is only meaningful for the map that issued it: two maps can produce equal handles for
    /// different values. Keep handles next to the map they belong to, or wrap them in a type that
    /// names the owner. Do not construct handles by hand.
    /// </para>
    /// </remarks>
    public readonly struct SlotHandle : IEquatable<SlotHandle> {
        /// <summary>Creates a handle. Callers get one from <see cref="SlotMap{T}.Add"/>.</summary>
        internal SlotHandle(int index, int generation) {
            Index = index;
            Generation = generation;
        }

        /// <summary>Slot index inside the map.</summary>
        public int Index { get; }

        /// <summary>Generation of the slot when the value was added.</summary>
        public int Generation { get; }

        /// <summary>
        /// True when the handle was never issued (generation 0 or less). This cannot tell whether an
        /// issued handle is still live: the map's <see cref="SlotMap{T}.TryGet"/> is the authority
        /// for staleness.
        /// </summary>
        public bool IsDefault => Generation <= 0;

        /// <inheritdoc />
        public bool Equals(SlotHandle other) {
            return Index == other.Index && Generation == other.Generation;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) {
            return obj is SlotHandle other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode() {
            unchecked {
                return (Index * 397) ^ Generation;
            }
        }

        /// <summary>True when both handles point at the same generation of the same slot.</summary>
        public static bool operator ==(SlotHandle left, SlotHandle right) {
            return left.Equals(right);
        }

        /// <summary>True when the handles differ.</summary>
        public static bool operator !=(SlotHandle left, SlotHandle right) {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public override string ToString() {
            return $"SlotHandle({Index}, {Generation})";
        }
    }
}
