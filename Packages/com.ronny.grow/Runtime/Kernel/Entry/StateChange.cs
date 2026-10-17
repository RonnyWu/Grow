// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Entry {
    /// <summary>
    /// Payload of a kernel lifecycle transition: the state before and after the change.
    /// </summary>
    public readonly struct StateChange {
        /// <summary>Creates a transition payload.</summary>
        public StateChange(GrowState previous, GrowState next) {
            Previous = previous;
            Next = next;
        }

        /// <summary>The state before the transition.</summary>
        public GrowState Previous { get; }

        /// <summary>The state after the transition.</summary>
        public GrowState Next { get; }

        /// <summary>Formats the transition as <c>Previous&gt;Next</c>.</summary>
        public override string ToString() {
            return $"{Previous}>{Next}";
        }
    }
}
